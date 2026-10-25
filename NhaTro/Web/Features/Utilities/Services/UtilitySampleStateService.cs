using System.Globalization;
using Web.Features.Accounts;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.MockData;
using Web.Features.Utilities.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Utilities.Services;

/// <summary>In-memory P5 sample state for Sprint 2A. Nothing in this service is persisted.</summary>
public sealed class UtilitySampleStateService(TimeProvider? timeProvider = null)
{
    private readonly List<ServiceItemDto> _services = UtilitySampleData.CreateDefaultServices();
    private readonly List<PriceVersionDto> _priceVersions = UtilitySampleData.CreateDefaultPriceVersions();
    private readonly List<RoomReadingSampleDto> _roomReadings = UtilitySampleData.CreateDefaultRoomReadings();
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public event Action? OnChange;

    public IReadOnlyList<BuildingOptionDto> Buildings => UtilitySampleData.Buildings;

    public DateOnly Today => _timeProvider.TodayVietnam();

    public IReadOnlyList<BuildingOptionDto> GetBuildingsForCaller(string? role, string? email)
    {
        var allowedIds = UtilitySampleData.GetAllowedBuildingIds(role, email).ToHashSet(StringComparer.Ordinal);
        return UtilitySampleData.Buildings.Where(building => allowedIds.Contains(building.Id)).ToArray();
    }

    public IReadOnlyList<ServiceItemDto> GetServicesForCaller(
        string? role,
        string? email,
        string? buildingId = null,
        string? type = null,
        string? status = null,
        string? searchTerm = null)
    {
        if (!UtilityDraftRules.CanViewUtilityData(role)) return [];

        var allowedIds = UtilitySampleData.GetAllowedBuildingIds(role, email).ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(buildingId) && buildingId != "all" && !allowedIds.Contains(buildingId))
        {
            return [];
        }

        var pricingScope = !string.IsNullOrWhiteSpace(buildingId) && buildingId != "all"
            ? buildingId
            : allowedIds.Count == 1 ? allowedIds.First() : "all";
        RefreshPriceStatuses();
        RefreshCurrentPrices(pricingScope);

        var query = _services.AsEnumerable()
            .Where(service => service.BuildingId == "all" || allowedIds.Contains(service.BuildingId));

        if (!string.IsNullOrWhiteSpace(buildingId) && buildingId != "all")
        {
            query = query.Where(service => service.BuildingId == "all" || service.BuildingId == buildingId);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(service =>
                service.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                service.Code.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(type) && type != "all")
        {
            query = type switch
            {
                "meter" => query.Where(service => service.Type == ServiceTypeCategory.Meter),
                "fixed_monthly" => query.Where(service => service.Type == ServiceTypeCategory.FixedMonthly),
                "custom" => query.Where(service => service.Type == ServiceTypeCategory.Custom),
                _ => query,
            };
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            query = status switch
            {
                "active" => query.Where(service => service.Active),
                "inactive" => query.Where(service => !service.Active),
                _ => query,
            };
        }

        return query.Select(service => service.Copy()).ToArray();
    }

    public ServiceItemDto? GetServiceByIdForCaller(string? role, string? email, string serviceId) =>
        GetServicesForCaller(role, email).FirstOrDefault(service => service.Id == serviceId);

    public IReadOnlyList<BuildingOptionDto> GetEditableBuildingScopes(ServiceItemDto service)
    {
        if (service.BuildingId != "all")
        {
            return UtilitySampleData.Buildings.Where(building => building.Id == service.BuildingId).ToArray();
        }

        return UtilitySampleData.Buildings.Where(building => building.Id != "all").ToArray();
    }

    public (bool Success, UtilityErrorCode? Error, ServiceItemDto? Item) AddService(
        CreateServiceRequest request,
        string? callerRole,
        string modifierName)
    {
        if (!UtilityDraftRules.CanManageServicePrices(callerRole))
        {
            return (false, UtilityErrorCode.AccessDenied, null);
        }

        var issues = UtilityDraftRules.ValidateCreateService(request);
        if (issues.Count > 0)
        {
            return (false, issues[0].Error, null);
        }

        var building = UtilitySampleData.Buildings.FirstOrDefault(option => option.Id == request.BuildingId);
        if (building is null)
        {
            return (false, UtilityErrorCode.InvalidBuildingScope, null);
        }

        var newId = $"srv-{_services.Count + 1}";
        var newService = new ServiceItemDto
        {
            Id = newId,
            Name = request.Name.Trim(),
            Code = $"SRV{_services.Count + 1:D2}",
            Type = request.Type,
            Unit = request.Unit,
            RateType = ServiceRateType.Flat,
            CurrentRate = request.Rate,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            PriceStatus = GetStatus(request.EffectiveFrom, request.EffectiveTo),
            Active = true,
            IsReadOnly = false,
            IsSessionCreated = true,
            BuildingId = request.BuildingId,
            BuildingName = building.Name,
            Description = request.Description,
            HistoryCount = 1,
        };

        _services.Add(newService);
        _priceVersions.Insert(0, new PriceVersionDto
        {
            Id = $"pv-{newId}-1",
            ServiceId = newId,
            ServiceName = newService.Name,
            Unit = newService.Unit,
            Rate = newService.CurrentRate,
            RateType = newService.RateType,
            EffectiveFrom = newService.EffectiveFrom,
            EffectiveTo = newService.EffectiveTo,
            Status = GetStatus(newService.EffectiveFrom, newService.EffectiveTo),
            BuildingScope = newService.BuildingId,
            BuildingName = newService.BuildingName,
            CreatedAt = _timeProvider.UtcNow(),
            CreatedBy = modifierName,
            Note = request.Description,
            IsSessionCreated = true,
        });

        RefreshCurrentPrices(newService.BuildingId);
        NotifyStateChanged();
        return (true, null, newService.Copy());
    }

    public (bool Success, UtilityErrorCode? Error, PriceVersionDto? Version) UpdateServicePrice(
        UpdatePriceRequest request,
        string? callerRole,
        string modifierName)
    {
        if (!UtilityDraftRules.CanManageServicePrices(callerRole))
        {
            return (false, UtilityErrorCode.AccessDenied, null);
        }

        var issues = UtilityDraftRules.ValidateUpdatePrice(request);
        if (issues.Count > 0)
        {
            return (false, issues[0].Error, null);
        }

        var service = _services.FirstOrDefault(item => item.Id == request.ServiceId);
        if (service is null)
        {
            return (false, UtilityErrorCode.ServiceNotFound, null);
        }

        if (service.IsReadOnly || UtilityDraftRules.IsTariffReadOnly(service.Code))
        {
            return (false, UtilityErrorCode.ReadOnlyTariff, null);
        }

        if (!IsValidScopeForService(service, request.BuildingScope))
        {
            return (false, UtilityErrorCode.InvalidBuildingScope, null);
        }

        var sameScopeVersions = _priceVersions
            .Where(version => version.ServiceId == request.ServiceId && version.BuildingScope == request.BuildingScope)
            .ToList();
        var overlapsLaterVersion = sameScopeVersions.Any(version =>
            version.EffectiveFrom >= request.EffectiveFrom &&
            (!request.EffectiveTo.HasValue || version.EffectiveFrom <= request.EffectiveTo.Value));
        if (overlapsLaterVersion)
        {
            return (false, UtilityErrorCode.PriceVersionConflict, null);
        }

        foreach (var previous in sameScopeVersions.Where(version =>
                     version.EffectiveFrom < request.EffectiveFrom &&
                     (!version.EffectiveTo.HasValue || version.EffectiveTo.Value >= request.EffectiveFrom)))
        {
            previous.EffectiveTo = request.EffectiveFrom.AddDays(-1);
            previous.Status = GetStatus(previous.EffectiveFrom, previous.EffectiveTo);
        }

        var buildingName = UtilitySampleData.Buildings
            .First(option => option.Id == request.BuildingScope).Name;
        var newVersion = new PriceVersionDto
        {
            Id = $"pv-{request.ServiceId}-{_priceVersions.Count + 1}",
            ServiceId = request.ServiceId,
            ServiceName = service.Name,
            Unit = request.Unit,
            Rate = request.NewRate,
            RateType = service.RateType,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = GetStatus(request.EffectiveFrom, request.EffectiveTo),
            BuildingScope = request.BuildingScope,
            BuildingName = buildingName,
            CreatedAt = _timeProvider.UtcNow(),
            CreatedBy = modifierName,
            Note = request.Note,
            IsSessionCreated = true,
        };

        _priceVersions.Insert(0, newVersion);
        service.HistoryCount = _priceVersions.Count(version => version.ServiceId == service.Id);
        RefreshCurrentPrices(request.BuildingScope);
        NotifyStateChanged();
        return (true, null, newVersion.Copy());
    }

    public bool ToggleServiceActive(string id, string? callerRole)
    {
        var service = _services.FirstOrDefault(item => item.Id == id);
        if (!UtilityDraftRules.CanManageServicePrices(callerRole) || service is null || service.IsReadOnly)
        {
            return false;
        }

        service.Active = !service.Active;
        NotifyStateChanged();
        return true;
    }

    public IReadOnlyList<PriceVersionDto> GetPriceVersionsForCaller(
        string? role,
        string? email,
        string? serviceId = null)
    {
        if (!UtilityDraftRules.CanViewUtilityData(role)) return [];

        var allowedIds = UtilitySampleData.GetAllowedBuildingIds(role, email).ToHashSet(StringComparer.Ordinal);
        RefreshPriceStatuses();
        var visibleServiceIds = GetServicesForCaller(role, email)
            .Select(service => service.Id)
            .ToHashSet(StringComparer.Ordinal);

        var query = _priceVersions.Where(version =>
            visibleServiceIds.Contains(version.ServiceId) &&
            (version.BuildingScope == "all" || allowedIds.Contains(version.BuildingScope)));
        if (!string.IsNullOrWhiteSpace(serviceId) && serviceId != "all")
        {
            query = query.Where(version => version.ServiceId == serviceId);
        }

        return query
            .OrderByDescending(version => version.EffectiveFrom)
            .ThenByDescending(version => version.CreatedAt)
            .Select(version => version.Copy())
            .ToArray();
    }

    public IReadOnlyList<RoomReadingSampleDto> GetRoomReadingsForCaller(string? role, string? email)
    {
        if (!UtilityDraftRules.CanViewUtilityData(role)) return [];
        var allowedRoomIds = UtilitySampleData.GetAllowedRoomIds(role, email).ToHashSet(StringComparer.Ordinal);
        return _roomReadings
            .Where(room => allowedRoomIds.Contains(room.Id))
            .Select(room => room.Copy())
            .ToArray();
    }

    public (bool Success, UtilityErrorCode? Error, RoomReadingSampleDto? Room) SaveRoomReading(
        string roomId,
        decimal electricEnd,
        decimal waterEnd,
        DateOnly periodStart,
        DateOnly periodEnd,
        string? reason,
        string? callerRole,
        string? callerEmail,
        string modifierName)
    {
        if (!UtilityDraftRules.CanRecordMeterReadings(callerRole))
        {
            return (false, UtilityErrorCode.AccessDenied, null);
        }

        var room = _roomReadings.FirstOrDefault(item => item.Id == roomId);
        if (room is null || !UtilitySampleData.GetAllowedRoomIds(callerRole, callerEmail).Contains(roomId))
        {
            return (false, UtilityErrorCode.RoomNotFound, null);
        }

        var periodIssues = UtilityDraftRules.ValidateBillingPeriod(periodStart, periodEnd);
        if (periodIssues.Count > 0)
        {
            return (false, periodIssues[0].Error, null);
        }

        var hasElectricChanged = electricEnd != room.ElectricEnd;
        var hasWaterChanged = waterEnd != room.WaterEnd;
        var readingIssues = UtilityDraftRules.ValidateMeterReading(
                room.ElectricStart, electricEnd, hasElectricChanged, reason)
            .Concat(UtilityDraftRules.ValidateMeterReading(
                room.WaterStart, waterEnd, hasWaterChanged, reason))
            .ToArray();
        if (readingIssues.Length > 0)
        {
            return (false, readingIssues[0].Error, null);
        }

        room.PeriodStart = periodStart;
        room.PeriodEnd = periodEnd;
        room.HasSessionChanges = true;
        if (hasElectricChanged)
        {
            room.AuditLogs.Insert(0, CreateReadingAudit(
                callerRole!, modifierName, "ElectricMeterReading", room.ElectricEnd, electricEnd, "kWh", reason));
            room.ElectricEnd = electricEnd;
        }

        if (hasWaterChanged)
        {
            room.AuditLogs.Insert(0, CreateReadingAudit(
                callerRole!, modifierName, "WaterMeterReading", room.WaterEnd, waterEnd, "m³", reason));
            room.WaterEnd = waterEnd;
        }

        NotifyStateChanged();
        return (true, null, room.Copy());
    }

    private bool IsValidScopeForService(ServiceItemDto service, string buildingScope) =>
        service.BuildingId == "all"
            ? UtilitySampleData.Buildings.Any(building => building.Id == buildingScope)
            : buildingScope == service.BuildingId;

    private PriceVersionStatus GetStatus(DateOnly start, DateOnly? end)
    {
        if (start > Today) return PriceVersionStatus.Upcoming;
        if (end.HasValue && end.Value < Today) return PriceVersionStatus.Expired;
        return PriceVersionStatus.Active;
    }

    private void RefreshPriceStatuses()
    {
        foreach (var version in _priceVersions)
        {
            version.Status = GetStatus(version.EffectiveFrom, version.EffectiveTo);
        }
    }

    private void RefreshCurrentPrices(string selectedBuildingScope = "all")
    {
        RefreshPriceStatuses();
        foreach (var service in _services)
        {
            var serviceScope = service.BuildingId == "all" ? selectedBuildingScope : service.BuildingId;
            var versions = _priceVersions
                .Where(version => version.ServiceId == service.Id &&
                                  (version.BuildingScope == serviceScope || version.BuildingScope == "all"))
                .OrderBy(version => version.BuildingScope == serviceScope ? 0 : 1)
                .ThenByDescending(version => version.EffectiveFrom)
                .ToArray();

            var current = versions.FirstOrDefault(version => version.Status == PriceVersionStatus.Active);
            var visible = current ?? versions.FirstOrDefault(version => version.Status == PriceVersionStatus.Upcoming)
                ?? versions.FirstOrDefault();
            if (visible is null) continue;

            service.CurrentRate = visible.Rate;
            service.Unit = visible.Unit;
            service.EffectiveFrom = visible.EffectiveFrom;
            service.EffectiveTo = visible.EffectiveTo;
            service.PriceStatus = visible.Status;
        }
    }

    private AuditLogEntryDto CreateReadingAudit(
        string role,
        string modifier,
        string field,
        decimal oldValue,
        decimal newValue,
        string unit,
        string? reason)
    {
        var oldText = oldValue.ToString("0.##", CultureInfo.InvariantCulture);
        var newText = newValue.ToString("0.##", CultureInfo.InvariantCulture);
        return new AuditLogEntryDto
        {
            Modifier = modifier,
            Role = role,
            Timestamp = _timeProvider.UtcNow(),
            FieldChanged = field,
            OldValue = oldText,
            NewValue = newText,
            DetailDiff = $"{oldText} → {newText} {unit}",
            Reason = reason?.Trim() ?? string.Empty,
        };
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
