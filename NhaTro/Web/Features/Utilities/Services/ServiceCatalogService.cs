using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Utilities.Services;

public sealed class ServiceCatalogService : IServiceCatalogService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;

    public ServiceCatalogService(ApplicationDbContext context, TimeProvider? timeProvider = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<ServiceCatalogDto>> GetServiceCatalogsAsync(PropertyCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        EnsureCanView(caller.Role);

        IQueryable<int> allowedPropertyIds;
        if (caller.IsKhachThue || string.Equals(caller.Role, "Tenant", StringComparison.OrdinalIgnoreCase))
        {
            var now = _timeProvider.UtcNow();
            allowedPropertyIds = (from member in _context.ContractMembers
                                  join tenant in _context.Tenants on member.TenantId equals tenant.Id
                                  join contract in _context.Contracts on member.ContractId equals contract.ContractId
                                  join room in _context.Rooms on contract.RoomId equals room.Id
                                  where tenant.UserId == caller.UserId
                                        && tenant.IsActive
                                        && contract.Status == ContractStatus.Approved
                                        && contract.StartDate <= now
                                        && contract.EndDate >= now
                                  select room.PropertyId).Distinct();
        }
        else
        {
            allowedPropertyIds = PropertyScope.Apply(_context.Properties, _context.PropertyManagers, caller)
                .Select(p => p.Id);
        }

        var today = _timeProvider.TodayVietnam();
        var catalogs = await _context.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
            .Where(catalog => catalog.PropertyId == null || allowedPropertyIds.Contains(catalog.PropertyId.Value))
            .OrderBy(catalog => catalog.PropertyId)
            .ThenBy(catalog => catalog.Id)
            .ToListAsync();

        return catalogs
            .Where(catalog => catalog.PriceHistory.Count > 0)
            .Select(catalog => ToDto(catalog, today))
            .ToArray();
    }

    public async Task<ServiceCatalogDto> CreateOrUpdateServicePriceAsync(
        CreateServiceCatalogDto request,
        PropertyCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        EnsureCanManage(caller);
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(ServiceCatalogRules.Validate(request));

        if (!request.PropertyId.HasValue)
        {
            throw new ArgumentException("Bắt buộc phải chọn nhà trọ (PropertyId) khi tạo danh mục dịch vụ.", nameof(request.PropertyId));
        }

        var propertyExists = await _context.Properties.AnyAsync(property =>
            property.Id == request.PropertyId.Value && property.IsActive && !property.IsDeleted);
        if (!propertyExists)
        {
            throw new ArgumentException("Nhà trọ không tồn tại.", nameof(request.PropertyId));
        }

        var hasAccess = await PropertyScope.Apply(_context.Properties, _context.PropertyManagers, caller)
            .AnyAsync(property => property.Id == request.PropertyId.Value);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý nhà trọ này.");
        }

        var catalog = new ServiceCatalog
        {
            PropertyId = request.PropertyId,
            IsReadOnly = false,
            CreatedAt = _timeProvider.UtcNow(),
        };
        catalog.PriceHistory.Add(CreateVersion(
            request.Name,
            request.Unit,
            request.CalculationMethod,
            request.UnitPrice,
            request.EffectiveDate,
            caller.UserId));

        _context.ServiceCatalogs.Add(catalog);
        await _context.SaveChangesAsync();
        return ToDto(catalog, _timeProvider.TodayVietnam());
    }

    public async Task<ServiceCatalogDto> CreateOrUpdateServicePriceAsync(
        int serviceCatalogId,
        UpdateServicePriceDto request,
        PropertyCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        EnsureCanManage(caller);
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(ServiceCatalogRules.Validate(request));

        var catalog = await _context.ServiceCatalogs
            .Include(item => item.PriceHistory)
            .SingleOrDefaultAsync(item => item.Id == serviceCatalogId);
        if (catalog is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy danh mục dịch vụ {serviceCatalogId}.");
        }

        if (catalog.IsReadOnly)
        {
            throw new UnauthorizedAccessException("Biểu giá điện/nước chỉ được xem, không thể sửa.");
        }

        if (!catalog.PropertyId.HasValue)
        {
            throw new UnauthorizedAccessException("Không thể cập nhật biểu giá danh mục hệ thống dùng chung.");
        }

        var hasAccess = await PropertyScope.Apply(_context.Properties, _context.PropertyManagers, caller)
            .AnyAsync(property => property.Id == catalog.PropertyId.Value && property.IsActive && !property.IsDeleted);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý danh mục của nhà trọ này.");
        }

        if (catalog.PriceHistory.Any(version => version.EffectiveDate == request.EffectiveDate))
        {
            throw new InvalidOperationException("Đã có phiên bản giá cho ngày hiệu lực này.");
        }

        // Add a new immutable row. Existing versions remain unchanged so invoices can retain their snapshots.
        catalog.PriceHistory.Add(CreateVersion(
            request.Name,
            request.Unit,
            request.CalculationMethod,
            request.UnitPrice,
            request.EffectiveDate,
            caller.UserId));

        await _context.SaveChangesAsync();
        return ToDto(catalog, _timeProvider.TodayVietnam());
    }

    private ServicePriceHistory CreateVersion(
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate,
        string? userId) => new()
    {
        Name = name.Trim(),
        Unit = unit.Trim(),
        CalculationMethod = calculationMethod.Trim(),
        UnitPrice = unitPrice,
        EffectiveDate = effectiveDate,
        CreatedAt = _timeProvider.UtcNow(),
        CreatedByUserId = userId,
    };

    private static ServiceCatalogDto ToDto(ServiceCatalog catalog, DateOnly today)
    {
        var history = catalog.PriceHistory
            .OrderByDescending(version => version.EffectiveDate)
            .ThenByDescending(version => version.Id)
            .Select(version => new ServicePriceVersionDto(
                version.Id,
                version.Name,
                version.Unit,
                version.CalculationMethod,
                version.UnitPrice,
                version.EffectiveDate))
            .ToArray();

        // If every version is scheduled for the future, expose the next scheduled version.
        // Its EffectiveDate remains visible so callers can distinguish it from an active price.
        var current = history
            .Where(version => version.EffectiveDate <= today)
            .OrderByDescending(version => version.EffectiveDate)
            .FirstOrDefault()
            ?? history.OrderBy(version => version.EffectiveDate).First();

        return new ServiceCatalogDto(
            catalog.Id,
            catalog.PropertyId,
            current.Name,
            current.Unit,
            current.CalculationMethod,
            current.UnitPrice,
            current.EffectiveDate,
            catalog.IsReadOnly,
            history);
    }

    private static void ValidateRequest(IReadOnlyList<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ArgumentException($"Dữ liệu danh mục/đơn giá không hợp lệ: {string.Join(", ", errors)}.");
        }
    }

    private static void EnsureCanView(string userRole)
    {
        if (IsRole(userRole, Roles.ChuTro, "Landlord") ||
            IsRole(userRole, Roles.QuanLy, "Manager") ||
            IsRole(userRole, Roles.KhachThue, "Tenant"))
        {
            return;
        }

        throw new UnauthorizedAccessException("Vai trò này không được xem danh mục P5.");
    }

    private static void EnsureCanManage(PropertyCaller caller)
    {
        if (!IsRole(caller.Role, Roles.ChuTro, "Landlord"))
        {
            throw new UnauthorizedAccessException("Chỉ Chủ trọ được thiết lập danh mục và đơn giá dịch vụ.");
        }
    }

    private static bool IsRole(string actual, string canonical, string english) =>
        string.Equals(actual, canonical, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(actual, english, StringComparison.OrdinalIgnoreCase);
}
