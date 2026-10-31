using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
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

    public async Task<IReadOnlyList<ServiceCatalogDto>> GetServiceCatalogsAsync(string userRole)
    {
        EnsureCanView(userRole);
        var today = _timeProvider.TodayVietnam();
        var catalogs = await _context.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
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
        int userId,
        string userRole)
    {
        EnsureCanManage(userRole);
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(ServiceCatalogRules.Validate(request));
        await ValidateScopeAsync(request.PropertyId);

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
            userId));

        _context.ServiceCatalogs.Add(catalog);
        await _context.SaveChangesAsync();
        return ToDto(catalog, _timeProvider.TodayVietnam());
    }

    public async Task<ServiceCatalogDto> CreateOrUpdateServicePriceAsync(
        int serviceCatalogId,
        UpdateServicePriceDto request,
        int userId,
        string userRole)
    {
        EnsureCanManage(userRole);
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
            userId));

        await _context.SaveChangesAsync();
        return ToDto(catalog, _timeProvider.TodayVietnam());
    }

    private async Task ValidateScopeAsync(int? propertyId)
    {
        if (propertyId.HasValue && !await _context.Properties.AnyAsync(property =>
                property.Id == propertyId.Value && property.IsActive && !property.IsDeleted))
        {
            throw new ArgumentException("Nhà trọ không tồn tại.", nameof(propertyId));
        }
    }

    private ServicePriceHistory CreateVersion(
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate,
        int userId) => new()
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

    private static void EnsureCanManage(string userRole)
    {
        if (!IsRole(userRole, Roles.ChuTro, "Landlord"))
        {
            throw new UnauthorizedAccessException("Chỉ Chủ trọ được thiết lập danh mục và đơn giá dịch vụ.");
        }
    }

    private static bool IsRole(string actual, string canonical, string english) =>
        string.Equals(actual, canonical, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(actual, english, StringComparison.OrdinalIgnoreCase);
}
