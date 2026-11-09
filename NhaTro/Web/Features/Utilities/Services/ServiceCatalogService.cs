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

    public ServiceCatalogService(
        ApplicationDbContext context,
        TimeProvider? timeProvider = null)
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
        var query = _context.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
            .Where(catalog => catalog.PropertyId == null || allowedPropertyIds.Contains(catalog.PropertyId.Value));

        // TF-13: ChuTro sees both Active and Inactive to toggle them back.
        // Manager and Tenant only see Active services.
        if (!caller.IsChuTro)
        {
            query = query.Where(catalog => catalog.IsActive);
        }

        var catalogs = await query
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
            throw WithKey(new ArgumentException("Bắt buộc phải chọn nhà trọ (PropertyId) khi tạo danh mục dịch vụ.", nameof(request.PropertyId)), "ErrorBuildingRequired");
        }

        var propertyExists = await _context.Properties.AnyAsync(property =>
            property.Id == request.PropertyId.Value && property.IsActive && !property.IsDeleted);
        if (!propertyExists)
        {
            throw WithKey(new ArgumentException("Nhà trọ không tồn tại.", nameof(request.PropertyId)), "ErrorBuildingNotFound");
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
            IsActive = true,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = _timeProvider.UtcNow(),
        };
        catalog.PriceHistory.Add(CreateVersion(
            request.Name,
            request.Unit,
            request.CalculationMethod,
            request.UnitPrice,
            request.EffectiveDate,
            caller.UserId,
            effectiveTo: request.EffectiveTo,
            note: request.Note));

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
            throw WithKey(new UnauthorizedAccessException("Biểu giá điện/nước chỉ được xem, không thể sửa."), "ErrorReadOnlyTariff");
        }

        if (!catalog.PropertyId.HasValue)
        {
            throw WithKey(new UnauthorizedAccessException("Không thể cập nhật biểu giá danh mục hệ thống dùng chung."), "ErrorSystemCatalogReadOnly");
        }

        var hasAccess = await PropertyScope.Apply(_context.Properties, _context.PropertyManagers, caller)
            .AnyAsync(property => property.Id == catalog.PropertyId.Value && property.IsActive && !property.IsDeleted);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý danh mục của nhà trọ này.");
        }

        if (!catalog.IsActive)
        {
            throw WithKey(new InvalidOperationException("Không thể tạo phiên bản giá mới cho danh mục đang ngưng hoạt động."), "ErrorServiceInactive");
        }

        var latestVersion = catalog.PriceHistory.OrderByDescending(v => v.EffectiveDate).FirstOrDefault();
        if (latestVersion != null && request.EffectiveDate <= latestVersion.EffectiveDate)
        {
            throw WithKey(
                new InvalidOperationException($"Ngày hiệu lực của phiên bản mới ({request.EffectiveDate:dd/MM/yyyy}) phải sau ngày hiệu lực của phiên bản mới nhất hiện có ({latestVersion.EffectiveDate:dd/MM/yyyy})."),
                "ErrorEffectiveDateNotAfterLatest",
                request.EffectiveDate.ToString("dd/MM/yyyy"),
                latestVersion.EffectiveDate.ToString("dd/MM/yyyy"));
        }

        // Close out the previous version's EffectiveTo to D2.AddDays(-1) (never extend an earlier explicit end date)
        if (latestVersion != null)
        {
            var closeAt = request.EffectiveDate.AddDays(-1);
            if (closeAt <= latestVersion.EffectiveDate &&
                (!latestVersion.EffectiveTo.HasValue || latestVersion.EffectiveTo.Value >= request.EffectiveDate))
            {
                throw WithKey(
                    new InvalidOperationException("Ngày áp dụng giá mới khiến phiên bản hiện tại không còn đủ khoảng hiệu lực hợp lệ."),
                    "ErrorPriceVersionTooShort");
            }

            if (latestVersion.EffectiveTo.HasValue && latestVersion.EffectiveTo.Value < request.EffectiveDate.AddDays(-1))
            {
                throw WithKey(
                    new InvalidOperationException("Phiên bản giá mới phải bắt đầu liền sau ngày kết thúc của phiên bản gần nhất."),
                    "ErrorPriceVersionGap");
            }

            if (latestVersion.EffectiveTo is null || latestVersion.EffectiveTo > closeAt)
            {
                latestVersion.EffectiveTo = closeAt;
            }
        }

        // Add a new immutable row. Existing versions remain unchanged so invoices can retain their snapshots.
        catalog.PriceHistory.Add(CreateVersion(
            request.Name,
            request.Unit,
            request.CalculationMethod,
            request.UnitPrice,
            request.EffectiveDate,
            caller.UserId,
            effectiveTo: request.EffectiveTo,
            note: request.Note));

        await _context.SaveChangesAsync();
        return ToDto(catalog, _timeProvider.TodayVietnam());
    }

    public async Task<ServiceCatalogDto> ToggleServiceCatalogStatusAsync(
        int serviceCatalogId,
        PropertyCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        EnsureCanManage(caller);

        var catalog = await _context.ServiceCatalogs
            .Include(item => item.PriceHistory)
            .SingleOrDefaultAsync(item => item.Id == serviceCatalogId);
        if (catalog is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy danh mục dịch vụ {serviceCatalogId}.");
        }

        if (catalog.IsReadOnly)
        {
            throw WithKey(new UnauthorizedAccessException("Không thể bật/tắt trạng thái của danh mục chỉ đọc."), "ErrorReadOnlyTariff");
        }

        if (!catalog.PropertyId.HasValue)
        {
            throw WithKey(new UnauthorizedAccessException("Không thể bật/tắt trạng thái của danh mục hệ thống dùng chung."), "ErrorSystemCatalogReadOnly");
        }

        var hasAccess = await PropertyScope.Apply(_context.Properties, _context.PropertyManagers, caller)
            .AnyAsync(property => property.Id == catalog.PropertyId.Value && property.IsActive && !property.IsDeleted);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý danh mục của nhà trọ này.");
        }

        catalog.IsActive = !catalog.IsActive;
        await _context.SaveChangesAsync();
        return ToDto(catalog, _timeProvider.TodayVietnam());
    }

    public async Task DeleteServiceCatalogAsync(
        int serviceCatalogId,
        PropertyCaller caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        EnsureCanManage(caller);

        var catalog = await _context.ServiceCatalogs
            .SingleOrDefaultAsync(item => item.Id == serviceCatalogId);
        if (catalog is null)
        {
            throw new KeyNotFoundException($"Không tìm thấy danh mục dịch vụ {serviceCatalogId}.");
        }

        if (catalog.IsReadOnly)
        {
            throw WithKey(new UnauthorizedAccessException("Biểu giá điện/nước chỉ được xem, không thể xóa."), "ErrorReadOnlyTariff");
        }

        if (!catalog.PropertyId.HasValue)
        {
            throw WithKey(new UnauthorizedAccessException("Không thể xóa danh mục hệ thống dùng chung."), "ErrorSystemCatalogReadOnly");
        }

        var hasAccess = await PropertyScope.Apply(_context.Properties, _context.PropertyManagers, caller)
            .AnyAsync(property => property.Id == catalog.PropertyId.Value && property.IsActive && !property.IsDeleted);
        if (!hasAccess)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý danh mục của nhà trọ này.");
        }

        // Xóa mềm: giữ danh mục + lịch sử giá để đối soát hóa đơn cũ, chỉ ẩn khỏi danh mục.
        catalog.IsDeleted = true;
        catalog.DeletedAt = _timeProvider.UtcNow();
        catalog.DeletedBy = caller.UserId;
        catalog.IsActive = false;
        await _context.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task<ServicePriceCopyResult> CopyPricesFromPropertyAsync(
        int sourcePropertyId,
        int targetPropertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        EnsureCanManage(caller);

        if (sourcePropertyId <= 0 || targetPropertyId <= 0)
        {
            throw WithKey(new ArgumentException("Nhà trọ nguồn và đích không hợp lệ."), "ErrorInvalidData");
        }

        if (sourcePropertyId == targetPropertyId)
        {
            throw WithKey(new ArgumentException("Không thể sao chép giá từ chính nhà trọ này."), "ErrorCopySameProperty");
        }

        var accessibleIds = await PropertyScope.Apply(_context.Properties, _context.PropertyManagers, caller)
            .Where(property => property.Id == sourcePropertyId || property.Id == targetPropertyId)
            .Select(property => property.Id)
            .ToListAsync(ct);

        if (!accessibleIds.Contains(targetPropertyId))
        {
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý nhà trọ đích.");
        }

        if (!accessibleIds.Contains(sourcePropertyId))
        {
            throw WithKey(new UnauthorizedAccessException("Bạn không có quyền xem nhà trọ nguồn."), "ErrorCopySourceForbidden");
        }

        var today = _timeProvider.TodayVietnam();

        // Chỉ lấy danh mục đang dùng; bỏ danh mục chỉ đọc (biểu giá hệ thống) và bản đã xóa mềm.
        var sources = await _context.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
            .Where(catalog => catalog.PropertyId == sourcePropertyId
                && !catalog.IsDeleted
                && catalog.IsActive
                && !catalog.IsReadOnly)
            .ToListAsync(ct);

        var targetNames = (await _context.ServiceCatalogs
                .AsNoTracking()
                .Include(catalog => catalog.PriceHistory)
                .Where(catalog => catalog.PropertyId == targetPropertyId && !catalog.IsDeleted)
                .ToListAsync(ct))
            .SelectMany(catalog => catalog.PriceHistory)
            .Select(version => version.Name.Trim())
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var copied = 0;
        var skipped = 0;

        foreach (var source in sources)
        {
            // Chỉ sao chép bản giá đang hiệu lực; không mang lịch sử cũ sang nhà trọ mới.
            var current = source.PriceHistory
                .Where(version => version.EffectiveDate <= today
                    && (version.EffectiveTo is null || version.EffectiveTo.Value >= today))
                .OrderByDescending(version => version.EffectiveDate)
                .FirstOrDefault();

            if (current is null) continue;

            var name = current.Name.Trim();
            if (name.Length == 0) continue;

            if (!targetNames.Add(name))
            {
                skipped++;
                continue;
            }

            var catalog = new ServiceCatalog
            {
                PropertyId = targetPropertyId,
                IsReadOnly = false,
                IsActive = true,
                Description = source.Description,
                CreatedAt = _timeProvider.UtcNow(),
            };
            catalog.PriceHistory.Add(CreateVersion(
                name,
                current.Unit,
                current.CalculationMethod,
                current.UnitPrice,
                today,
                caller.UserId,
                note: $"Sao chép từ nhà trọ #{sourcePropertyId}"));
            _context.ServiceCatalogs.Add(catalog);
            copied++;
        }

        // Một lần lưu cho cả lô: hoặc sao chép đủ, hoặc không ghi gì.
        if (copied > 0) await _context.SaveChangesAsync(ct);

        return new ServicePriceCopyResult(copied, skipped);
    }

    private ServicePriceHistory CreateVersion(
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate,
        string? userId,
        DateOnly? effectiveTo = null,
        string? note = null) => new()
    {
        Name = name.Trim(),
        Unit = unit.Trim(),
        CalculationMethod = calculationMethod.Trim(),
        UnitPrice = unitPrice,
        EffectiveDate = effectiveDate,
        EffectiveTo = effectiveTo,
        CreatedAt = _timeProvider.UtcNow(),
        CreatedByUserId = userId,
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
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
                version.EffectiveDate,
                version.EffectiveTo,
                version.CreatedAt,
                version.CreatedByUserId,
                version.Note))
            .ToArray();

        // Lookup phiên bản hiện hành: EffectiveDate <= today && (EffectiveTo == null || EffectiveTo >= today)
        var current = history
            .Where(version => version.EffectiveDate <= today && (version.EffectiveTo == null || version.EffectiveTo >= today))
            .OrderByDescending(version => version.EffectiveDate)
            .FirstOrDefault()
            ?? history.Where(version => version.EffectiveDate > today).OrderBy(version => version.EffectiveDate).FirstOrDefault()
            ?? history.First();

        return new ServiceCatalogDto(
            catalog.Id,
            catalog.PropertyId,
            current.Name,
            current.Unit,
            current.CalculationMethod,
            current.UnitPrice,
            current.EffectiveDate,
            catalog.IsReadOnly,
            history,
            catalog.IsActive,
            current.EffectiveTo,
            catalog.Description);
    }

    /// <summary>
    /// Tags an exception with a UI resource key so the adapter can localize the failure
    /// without parsing the (developer-facing) message.
    /// </summary>
    private static TException WithKey<TException>(TException exception, string resourceKey, params object[] args)
        where TException : Exception
    {
        exception.Data[ServiceCatalogErrorKeys.ResourceKey] = resourceKey;
        exception.Data[ServiceCatalogErrorKeys.ResourceArgs] = args;
        return exception;
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
            throw WithKey(new UnauthorizedAccessException("Chỉ Chủ trọ được thiết lập danh mục và đơn giá dịch vụ."), "ErrorOwnerOnlyService");
        }
    }

    private static bool IsRole(string actual, string canonical, string english) =>
        string.Equals(actual, canonical, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(actual, english, StringComparison.OrdinalIgnoreCase);
}
