namespace Web.Features.Utilities.DTOs;

public enum ServiceTypeCategory
{
    Meter,
    FixedMonthly,
    Custom,
}

public enum ServiceRateType
{
    Flat,
    Tiered,
}

public sealed class ServiceItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ServiceTypeCategory Type { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public ServiceRateType RateType { get; set; }
    public decimal CurrentRate { get; set; }
    public string RateDisplay { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool Active { get; set; } = true;
    public bool IsReadOnly { get; set; }
    public string BuildingId { get; set; } = "all";
    public string BuildingName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int HistoryCount { get; set; } = 1;

    public ServiceItemDto Copy() => new()
    {
        Id = Id,
        Name = Name,
        Code = Code,
        Type = Type,
        TypeLabel = TypeLabel,
        Unit = Unit,
        RateType = RateType,
        CurrentRate = CurrentRate,
        RateDisplay = RateDisplay,
        EffectiveFrom = EffectiveFrom,
        EffectiveTo = EffectiveTo,
        Active = Active,
        IsReadOnly = IsReadOnly,
        BuildingId = BuildingId,
        BuildingName = BuildingName,
        Description = Description,
        HistoryCount = HistoryCount,
    };
}

public sealed class PriceVersionDto
{
    public string Id { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public ServiceRateType RateType { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string Status { get; set; } = "active"; // active, upcoming, expired
    public string BuildingScope { get; set; } = "all";
    public string BuildingName { get; set; } = string.Empty;
    public bool AutoActivate { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public sealed record ElectricTierDto(int Id, string Name, decimal? MaxKwh, decimal Rate);

public sealed record TierAppliedDto(string TierName, decimal Units, decimal Rate, decimal Amount);

public sealed record ElectricityCalculationResult(decimal Total, string Breakdown, IReadOnlyList<TierAppliedDto> TiersApplied);

public sealed record WaterCalculationResult(decimal Total, string Breakdown);

public sealed record TotalMonthlyBillResult(
    decimal RoomRent,
    decimal ElectricityCost,
    decimal WaterCost,
    decimal ServiceCost,
    decimal TotalCost);

public sealed class AuditLogEntryDto
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Modifier { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string FieldChanged { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
    public string DetailDiff { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class RoomReadingSampleDto
{
    public string Id { get; set; } = string.Empty;
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string BuildingId { get; set; } = string.Empty;
    public string BuildingName { get; set; } = string.Empty;
    public string ContractCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string TenantPhone { get; set; } = string.Empty;
    public decimal RoomRent { get; set; }
    public string PeriodMonth { get; set; } = string.Empty;
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    public string Status { get; set; } = "open"; // open, locked, billed
    public string ElectricMeterCode { get; set; } = string.Empty;
    public decimal ElectricStart { get; set; }
    public decimal ElectricEnd { get; set; }
    public string WaterMeterCode { get; set; } = string.Empty;
    public decimal WaterStart { get; set; }
    public decimal WaterEnd { get; set; }
    public decimal WaterRate { get; set; } = 15_000m;
    public decimal InternetCost { get; set; } = 100_000m;
    public decimal CleaningCost { get; set; } = 30_000m;
    public decimal ParkingCost { get; set; }
    public string RefCode { get; set; } = string.Empty;
    public List<AuditLogEntryDto> AuditLogs { get; set; } = [];

    public RoomReadingSampleDto Copy() => new()
    {
        Id = Id,
        RoomCode = RoomCode,
        RoomName = RoomName,
        BuildingId = BuildingId,
        BuildingName = BuildingName,
        ContractCode = ContractCode,
        TenantName = TenantName,
        TenantPhone = TenantPhone,
        RoomRent = RoomRent,
        PeriodMonth = PeriodMonth,
        PeriodStart = PeriodStart,
        PeriodEnd = PeriodEnd,
        Status = Status,
        ElectricMeterCode = ElectricMeterCode,
        ElectricStart = ElectricStart,
        ElectricEnd = ElectricEnd,
        WaterMeterCode = WaterMeterCode,
        WaterStart = WaterStart,
        WaterEnd = WaterEnd,
        WaterRate = WaterRate,
        InternetCost = InternetCost,
        CleaningCost = CleaningCost,
        ParkingCost = ParkingCost,
        RefCode = RefCode,
        AuditLogs = AuditLogs.Select(a => new AuditLogEntryDto
        {
            Id = a.Id,
            Modifier = a.Modifier,
            Role = a.Role,
            Timestamp = a.Timestamp,
            FieldChanged = a.FieldChanged,
            OldValue = a.OldValue,
            NewValue = a.NewValue,
            DetailDiff = a.DetailDiff,
            Reason = a.Reason,
        }).ToList(),
    };
}

public sealed class UpdatePriceRequest
{
    public string ServiceId { get; set; } = string.Empty;
    public decimal NewRate { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string BuildingScope { get; set; } = "all";
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool AutoActivate { get; set; } = true;
    public string? Note { get; set; }
}

public sealed class CreateServiceRequest
{
    public string Name { get; set; } = string.Empty;
    public ServiceTypeCategory Type { get; set; } = ServiceTypeCategory.FixedMonthly;
    public string Unit { get; set; } = "Phòng";
    public decimal Rate { get; set; }
    public string BuildingId { get; set; } = "b1";
    public string? Description { get; set; }
}

public sealed record BuildingOptionDto(string Id, string Name, string Address, int TotalRooms, int OccupiedRooms);

