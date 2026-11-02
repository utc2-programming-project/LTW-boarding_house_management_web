using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.DTOs;
using Web.Infrastructure.Time;

namespace Web.Features.Utilities.Services;

/// <summary>
/// Thin UI Adapter connecting Blazor UI to persisted service catalog data.
/// Resolves the authenticated caller, enforces role &amp; property scope rules,
/// creates short-lived AsyncScopes to prevent circuit DbContext retention,
/// and maps backend DTOs to view models.
/// </summary>
public sealed class ServiceCatalogPageService(
    AuthenticationStateProvider authStateProvider,
    IServiceScopeFactory scopeFactory,
    TimeProvider? timeProvider,
    ILogger<ServiceCatalogPageService> logger)
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Indicates whether the active service data source is mock/in-memory or persisted SQL Server.
    /// In VHT-126, this adapter queries IServiceCatalogService backed by SQL Server, returning false.
    /// </summary>
    public bool IsSampleData => false;

    public DateOnly Today => _timeProvider.TodayVietnam();

    public async Task<string> GetCurrentRoleAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        BusinessGuard.EnsureBusinessAllowed(user);

        var role = Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
        if (string.Equals(role, Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Admin bị chặn truy cập nghiệp vụ P5 theo BusinessGuard.");
        }

        return role;
    }

    public async Task<string> GetCurrentEmailAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        return user.FindFirst(ClaimTypes.Email)?.Value ?? user.Identity?.Name ?? string.Empty;
    }

    public async Task<string> GetCurrentUserIdAsync()
    {
        var authState = await authStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        return user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }

    /// <summary>
    /// Loads service catalogs from SQL Server, enforcing role and property scope filtering.
    /// System catalogs (PropertyId == null) are visible to all authenticated roles.
    /// Property-scoped catalogs are strictly restricted to properties owned by ChuTro, assigned to QuanLy, or rented by KhachThue.
    /// </summary>
    public async Task<IReadOnlyList<ServiceItemDto>> GetServicesForCallerAsync(
        string? buildingFilter = null,
        string? typeFilter = null,
        string? statusFilter = null,
        string? searchTerm = null,
        CancellationToken ct = default)
    {
        var role = await GetCurrentRoleAsync();
        var userId = await GetCurrentUserIdAsync();

        await using var scope = scopeFactory.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Fetch from persisted backend with Property Scope applied by IServiceCatalogService
        var caller = new PropertyCaller(userId, role);
        var scopedCatalogs = await catalogService.GetServiceCatalogsAsync(caller);

        // Load property names for display
        var propertyIdsToLookup = scopedCatalogs
            .Where(c => c.PropertyId.HasValue)
            .Select(c => c.PropertyId!.Value)
            .Distinct()
            .ToList();

        var propertyNameMap = await db.Properties
            .AsNoTracking()
            .Where(p => propertyIdsToLookup.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        // 2. Map to ServiceItemDto
        var mappedServices = scopedCatalogs.Select(catalog =>
        {
            var propName = catalog.PropertyId.HasValue && propertyNameMap.TryGetValue(catalog.PropertyId.Value, out var name)
                ? name
                : (catalog.PropertyId.HasValue ? $"Nhà trọ #{catalog.PropertyId.Value}" : "Tất cả nhà trọ");

            return MapToServiceItemDto(catalog, propName, Today);
        }).ToList();

        // 3. Apply UI search & dropdown filters in memory
        IEnumerable<ServiceItemDto> query = mappedServices;

        if (!string.IsNullOrWhiteSpace(buildingFilter) && buildingFilter != "all")
        {
            query = query.Where(s => s.BuildingId == "all" || s.BuildingId == buildingFilter);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(s =>
                s.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                s.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(typeFilter) && typeFilter != "all")
        {
            query = typeFilter switch
            {
                "meter" => query.Where(s => s.Type == ServiceTypeCategory.Meter),
                "fixed_monthly" => query.Where(s => s.Type == ServiceTypeCategory.FixedMonthly),
                "custom" => query.Where(s => s.Type == ServiceTypeCategory.Custom),
                _ => query,
            };
        }

        if (!string.IsNullOrWhiteSpace(statusFilter) && statusFilter != "all")
        {
            query = statusFilter switch
            {
                "active" => query.Where(s => s.Active),
                "inactive" => query.Where(s => !s.Active),
                _ => query,
            };
        }

        return query.ToArray();
    }

    /// <summary>
    /// Loads price history versions for a given catalog or all catalogs within caller's scope.
    /// </summary>
    public async Task<IReadOnlyList<PriceVersionDto>> GetPriceVersionsForCallerAsync(
        string? serviceId = null,
        CancellationToken ct = default)
    {
        var services = await GetServicesForCallerAsync(ct: ct);
        var serviceIds = services.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);

        await using var scope = scopeFactory.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var role = await GetCurrentRoleAsync();
        var userId = await GetCurrentUserIdAsync();

        var caller = new PropertyCaller(userId, role);
        var catalogs = await catalogService.GetServiceCatalogsAsync(caller);

        var list = new List<PriceVersionDto>();
        foreach (var catalog in catalogs)
        {
            var stringId = catalog.Id.ToString();
            if (!serviceIds.Contains(stringId)) continue;
            if (!string.IsNullOrWhiteSpace(serviceId) && serviceId != "all" && stringId != serviceId) continue;

            var serviceItem = services.FirstOrDefault(s => s.Id == stringId);
            var buildingScope = catalog.PropertyId.HasValue ? catalog.PropertyId.Value.ToString() : "all";
            var buildingName = serviceItem?.BuildingName ?? "Tất cả nhà trọ";

            foreach (var version in catalog.PriceHistory)
            {
                list.Add(MapToPriceVersionDto(catalog, version, buildingScope, buildingName, Today));
            }
        }

        return list
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.Id)
            .ToArray();
    }

    /// <summary>
    /// Loads properties accessible to the caller for dropdown population.
    /// </summary>
    public async Task<IReadOnlyList<BuildingOptionDto>> GetBuildingsForCallerAsync(CancellationToken ct = default)
    {
        var role = await GetCurrentRoleAsync();
        var userId = await GetCurrentUserIdAsync();

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var accessiblePropertyIds = await ResolveAccessiblePropertyIdsAsync(db, userId, role, _timeProvider, ct);

        var properties = await db.Properties
            .AsNoTracking()
            .Where(p => accessiblePropertyIds.Contains(p.Id) && p.IsActive && !p.IsDeleted)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var result = new List<BuildingOptionDto>();
        if (role == Roles.ChuTro)
        {
            result.Add(new BuildingOptionDto("all", "Tất cả nhà trọ", "Toàn bộ hệ thống", properties.Count, properties.Count));
        }

        foreach (var p in properties)
        {
            var roomCount = await db.Rooms.CountAsync(r => r.PropertyId == p.Id, ct);
            result.Add(new BuildingOptionDto(
                p.Id.ToString(),
                p.Name,
                p.Address ?? string.Empty,
                roomCount,
                roomCount));
        }

        return result;
    }

    public async Task<IReadOnlyList<BuildingOptionDto>> GetEditableBuildingScopesAsync(
        ServiceItemDto service, CancellationToken ct = default)
    {
        var all = await GetBuildingsForCallerAsync(ct);
        if (service.BuildingId != "all")
        {
            return all.Where(b => b.Id == service.BuildingId).ToArray();
        }

        return all.Where(b => b.Id != "all").ToArray();
    }

    /// <summary>
    /// Checks whether write actions (Create/Update price) can proceed.
    /// Write actions are supported when backend accepts PropertyCaller and enforces Property Scope.
    /// </summary>
    public (bool CanWrite, string? BlockerReason) CheckWriteSupport()
    {
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage, ServiceItemDto? Item)> AddServiceAsync(
        CreateServiceRequest request, CancellationToken ct = default)
    {
        var role = await GetCurrentRoleAsync();
        var userId = await GetCurrentUserIdAsync();
        var caller = new PropertyCaller(userId, role);

        if (!caller.IsChuTro)
        {
            return (false, "Chỉ Chủ trọ được thiết lập danh mục và đơn giá dịch vụ.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return (false, "Tên dịch vụ không được để trống.", null);
        }

        if (string.IsNullOrWhiteSpace(request.BuildingId) || request.BuildingId == "all" || !int.TryParse(request.BuildingId, out var propertyId))
        {
            return (false, "Bắt buộc phải chọn nhà trọ cụ thể khi tạo danh mục dịch vụ.", null);
        }

        if (request.Rate < 0)
        {
            return (false, "Đơn giá không được âm.", null);
        }

        var method = request.Type switch
        {
            ServiceTypeCategory.FixedMonthly => "fixed-monthly",
            ServiceTypeCategory.Meter => "meter",
            _ => "custom"
        };

        var unitString = request.Unit switch
        {
            ServiceUnitCategory.Room => "phòng",
            ServiceUnitCategory.Person => "người",
            ServiceUnitCategory.Vehicle => "xe",
            ServiceUnitCategory.Visit => "lượt",
            ServiceUnitCategory.KilowattHour => "kWh",
            ServiceUnitCategory.CubicMeter => "m³",
            _ => "phòng"
        };

        var createDto = new CreateServiceCatalogDto
        {
            PropertyId = propertyId,
            Name = request.Name.Trim(),
            Unit = unitString,
            CalculationMethod = method,
            UnitPrice = request.Rate,
            EffectiveDate = request.EffectiveFrom == default ? Today : request.EffectiveFrom,
        };

        await using var scope = scopeFactory.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var created = await catalogService.CreateOrUpdateServicePriceAsync(createDto, caller);

            // Verify persisted in SQL
            var verifyCatalogs = await catalogService.GetServiceCatalogsAsync(caller);
            var verified = verifyCatalogs.FirstOrDefault(c => c.Id == created.Id);
            if (verified is null)
            {
                return (false, "Không thể xác minh dữ liệu đã lưu vào cơ sở dữ liệu.", null);
            }

            var propName = await db.Properties
                .Where(p => p.Id == propertyId)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct) ?? $"Nhà trọ #{propertyId}";

            var item = MapToServiceItemDto(verified, propName, Today);
            return (true, null, item);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi tạo dịch vụ.");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, PriceVersionDto? Version)> UpdateServicePriceAsync(
        UpdatePriceRequest request, CancellationToken ct = default)
    {
        var role = await GetCurrentRoleAsync();
        var userId = await GetCurrentUserIdAsync();
        var caller = new PropertyCaller(userId, role);

        if (!caller.IsChuTro)
        {
            return (false, "Chỉ Chủ trọ được thiết lập danh mục và đơn giá dịch vụ.", null);
        }

        if (!int.TryParse(request.ServiceId, out var catalogId) || catalogId <= 0)
        {
            return (false, "Mã dịch vụ không hợp lệ.", null);
        }

        if (request.NewRate < 0)
        {
            return (false, "Đơn giá không được âm.", null);
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IServiceCatalogService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var existingCatalogs = await catalogService.GetServiceCatalogsAsync(caller);
        var targetCatalog = existingCatalogs.FirstOrDefault(c => c.Id == catalogId);
        if (targetCatalog is null)
        {
            return (false, $"Không tìm thấy dịch vụ {catalogId} trong phạm vi quản lý của bạn.", null);
        }

        var unitString = request.Unit switch
        {
            ServiceUnitCategory.Room => "phòng",
            ServiceUnitCategory.Person => "người",
            ServiceUnitCategory.Vehicle => "xe",
            ServiceUnitCategory.Visit => "lượt",
            ServiceUnitCategory.KilowattHour => "kWh",
            ServiceUnitCategory.CubicMeter => "m³",
            _ => targetCatalog.Unit
        };

        var updateDto = new UpdateServicePriceDto
        {
            Name = targetCatalog.Name,
            Unit = unitString,
            CalculationMethod = targetCatalog.CalculationMethod,
            UnitPrice = request.NewRate,
            EffectiveDate = request.EffectiveFrom == default ? Today : request.EffectiveFrom,
        };

        try
        {
            var updated = await catalogService.CreateOrUpdateServicePriceAsync(catalogId, updateDto, caller);

            // Verify persisted in SQL
            var verifyCatalogs = await catalogService.GetServiceCatalogsAsync(caller);
            var verified = verifyCatalogs.FirstOrDefault(c => c.Id == catalogId);
            var verifiedVersion = verified?.PriceHistory.FirstOrDefault(v => v.EffectiveDate == updateDto.EffectiveDate);
            if (verifiedVersion is null)
            {
                return (false, "Không thể xác minh đơn giá mới đã lưu vào cơ sở dữ liệu.", null);
            }

            var propName = verified!.PropertyId.HasValue
                ? await db.Properties.Where(p => p.Id == verified.PropertyId.Value).Select(p => p.Name).FirstOrDefaultAsync(ct) ?? $"Nhà trọ #{verified.PropertyId}"
                : "Tất cả nhà trọ";

            var buildingScope = verified.PropertyId.HasValue ? verified.PropertyId.Value.ToString() : "all";
            var versionDto = MapToPriceVersionDto(verified, verifiedVersion, buildingScope, propName, Today);
            return (true, null, versionDto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Lỗi khi cập nhật đơn giá dịch vụ.");
            return (false, ex.Message, null);
        }
    }

    // ==========================================
    // Scope & Helper Resolution
    // ==========================================

    public static async Task<HashSet<int>> ResolveAccessiblePropertyIdsAsync(
        ApplicationDbContext db,
        string userId,
        string role,
        TimeProvider? timeProvider = null,
        CancellationToken ct = default)
    {
        var propertyIds = new HashSet<int>();
        var time = timeProvider ?? TimeProvider.System;

        if (role == Roles.ChuTro)
        {
            var ids = await db.Properties
                .AsNoTracking()
                .Where(p => p.OwnerId == userId && !p.IsDeleted)
                .Select(p => p.Id)
                .ToListAsync(ct);
            foreach (var id in ids) propertyIds.Add(id);
        }
        else if (role == Roles.QuanLy)
        {
            var ids = await db.PropertyManagers
                .AsNoTracking()
                .Where(pm => pm.ManagerUserId == userId && pm.RevokedAt == null)
                .Select(pm => pm.PropertyId)
                .ToListAsync(ct);
            foreach (var id in ids) propertyIds.Add(id);
        }
        else if (role == Roles.KhachThue)
        {
            // Resolve property of room currently rented by tenant
            var tenant = await db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.UserId == userId && t.IsActive, ct);

            if (tenant is not null)
            {
                var contractMember = await db.ContractMembers
                    .AsNoTracking()
                    .Where(cm => cm.TenantId == tenant.Id)
                    .Select(cm => cm.ContractId)
                    .ToListAsync(ct);

                if (contractMember.Count > 0)
                {
                    var now = time.UtcNow();
                    var activeRoomIds = await db.Contracts
                        .AsNoTracking()
                        .Where(c => contractMember.Contains(c.ContractId)
                            && c.Status == ContractStatus.Approved
                            && c.StartDate <= now
                            && c.EndDate >= now)
                        .Select(c => c.RoomId)
                        .ToListAsync(ct);

                    if (activeRoomIds.Count > 0)
                    {
                        var propIds = await db.Rooms
                            .AsNoTracking()
                            .Where(r => activeRoomIds.Contains(r.Id))
                            .Select(r => r.PropertyId)
                            .ToListAsync(ct);

                        foreach (var id in propIds) propertyIds.Add(id);
                    }
                }
            }
        }

        return propertyIds;
    }

    // ==========================================
    // DTO Mappings
    // ==========================================

    public static ServiceItemDto MapToServiceItemDto(ServiceCatalogDto catalog, string propertyName, DateOnly today)
    {
        var (rateType, serviceType) = MapCalculationMethod(catalog.CalculationMethod);
        var unitEnum = MapUnit(catalog.Unit);
        var priceStatus = catalog.EffectiveDate > today
            ? PriceVersionStatus.Upcoming
            : PriceVersionStatus.Active;

        return new ServiceItemDto
        {
            Id = catalog.Id.ToString(),
            Name = catalog.Name,
            Code = $"SRV{catalog.Id:D2}",
            Type = serviceType,
            Unit = unitEnum,
            RateType = rateType,
            CurrentRate = catalog.UnitPrice,
            EffectiveFrom = catalog.EffectiveDate,
            EffectiveTo = null, // Backend does not support EffectiveTo
            PriceStatus = priceStatus,
            Active = true, // Backend does not support IsActive, default true
            IsReadOnly = catalog.IsReadOnly,
            IsSessionCreated = false,
            BuildingId = catalog.PropertyId.HasValue ? catalog.PropertyId.Value.ToString() : "all",
            BuildingName = propertyName,
            Description = string.Empty,
            HistoryCount = catalog.PriceHistory.Count,
        };
    }

    public static PriceVersionDto MapToPriceVersionDto(
        ServiceCatalogDto catalog,
        ServicePriceVersionDto version,
        string buildingScope,
        string buildingName,
        DateOnly today)
    {
        var (rateType, _) = MapCalculationMethod(version.CalculationMethod);
        var unitEnum = MapUnit(version.Unit);
        var priceStatus = version.EffectiveDate > today
            ? PriceVersionStatus.Upcoming
            : PriceVersionStatus.Active;

        return new PriceVersionDto
        {
            Id = $"pv-{catalog.Id}-{version.Id}",
            ServiceId = catalog.Id.ToString(),
            ServiceName = version.Name,
            Unit = unitEnum,
            Rate = version.UnitPrice,
            RateType = rateType,
            EffectiveFrom = version.EffectiveDate,
            EffectiveTo = null, // Backend does not have EffectiveTo
            Status = priceStatus,
            BuildingScope = buildingScope,
            BuildingName = buildingName,
            CreatedAt = DateTime.MinValue, // DTO does not have CreatedAt
            CreatedBy = "—", // DTO does not have CreatedBy
            Note = string.Empty,
            IsSessionCreated = false,
        };
    }

    public static (ServiceRateType RateType, ServiceTypeCategory Type) MapCalculationMethod(string method)
    {
        var normalized = method.Trim().ToLowerInvariant();
        return normalized switch
        {
            "fixed-monthly" => (ServiceRateType.Flat, ServiceTypeCategory.FixedMonthly),
            "tiered" => (ServiceRateType.Tiered, ServiceTypeCategory.Meter),
            "meter" => (ServiceRateType.Flat, ServiceTypeCategory.Meter),
            "custom" => (ServiceRateType.Flat, ServiceTypeCategory.Custom),
            _ => (ServiceRateType.Flat, ServiceTypeCategory.Custom)
        };
    }

    public static ServiceUnitCategory MapUnit(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return normalized switch
        {
            "phòng" or "room" => ServiceUnitCategory.Room,
            "người" or "person" => ServiceUnitCategory.Person,
            "xe" or "vehicle" => ServiceUnitCategory.Vehicle,
            "lượt" or "visit" => ServiceUnitCategory.Visit,
            "kwh" => ServiceUnitCategory.KilowattHour,
            "m³" or "m3" => ServiceUnitCategory.CubicMeter,
            _ => ServiceUnitCategory.Room
        };
    }
}
