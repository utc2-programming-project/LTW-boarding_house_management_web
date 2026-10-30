namespace Web.Features.Utilities.DTOs;

public sealed record ServicePriceVersionDto(
    int Id,
    string Name,
    string Unit,
    string CalculationMethod,
    decimal UnitPrice,
    DateOnly EffectiveDate);

public sealed record ServiceCatalogDto(
    int Id,
    int? PropertyId,
    string Name,
    string Unit,
    string CalculationMethod,
    decimal UnitPrice,
    DateOnly EffectiveDate,
    bool IsReadOnly,
    IReadOnlyList<ServicePriceVersionDto> PriceHistory);
