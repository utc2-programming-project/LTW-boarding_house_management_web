namespace Web.Features.Utilities.DTOs;

public sealed record ServicePriceVersionDto(
    int Id,
    string Name,
    string Unit,
    string CalculationMethod,
    decimal UnitPrice,
    DateOnly EffectiveDate,
    DateOnly? EffectiveTo = null,
    DateTime CreatedAt = default,
    string? CreatedByUserId = null,
    string? Note = null);

public sealed record ServiceCatalogDto(
    int Id,
    int? PropertyId,
    string Name,
    string Unit,
    string CalculationMethod,
    decimal UnitPrice,
    DateOnly EffectiveDate,
    bool IsReadOnly,
    IReadOnlyList<ServicePriceVersionDto> PriceHistory,
    bool IsActive = true,
    DateOnly? EffectiveTo = null,
    string? Description = null);
