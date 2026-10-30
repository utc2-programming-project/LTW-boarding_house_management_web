namespace Web.Features.Utilities.DTOs;

/// <summary>Read-only tariff and the price lines calculated for one billing period.</summary>
public sealed record UtilityTariffSnapshotDto(
    int TariffId,
    TariffType Type,
    string ScopeCode,
    string Unit,
    DateOnly EffectiveDate,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string SourceName,
    string SourceReference,
    decimal Quantity,
    int TierLimitMultiplier,
    decimal Total,
    IReadOnlyList<UtilityTariffCostLineDto> CostLines);

public sealed record UtilityTariffCostLineDto(
    int TierNumber,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);
