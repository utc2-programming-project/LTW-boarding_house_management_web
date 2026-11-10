using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.Rules;

public static class ServiceCatalogRules
{
    public const int MaxUnitLength = 30;
    public static IReadOnlyList<string> Validate(CreateServiceCatalogDto request) =>
        Validate(request.Name, request.Unit, request.CalculationMethod, request.UnitPrice, request.EffectiveDate, request.EffectiveTo, request.Note, request.Description);

    public static IReadOnlyList<string> Validate(UpdateServicePriceDto request) =>
        Validate(request.Name, request.Unit, request.CalculationMethod, request.UnitPrice, request.EffectiveDate, request.EffectiveTo, request.Note);

    private static IReadOnlyList<string> Validate(
        string? name,
        string? unit,
        string? calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate,
        DateOnly? effectiveTo,
        string? note,
        string? description = null)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) errors.Add(nameof(CreateServiceCatalogDto.Name));
        if (string.IsNullOrWhiteSpace(unit) || unit.Trim().Length > MaxUnitLength) errors.Add(nameof(CreateServiceCatalogDto.Unit));
        if (string.IsNullOrWhiteSpace(calculationMethod)) errors.Add(nameof(CreateServiceCatalogDto.CalculationMethod));
        if (unitPrice <= 0m) errors.Add(nameof(CreateServiceCatalogDto.UnitPrice));
        if (effectiveDate == default) errors.Add(nameof(CreateServiceCatalogDto.EffectiveDate));
        if (effectiveTo.HasValue && effectiveDate != default && effectiveTo.Value <= effectiveDate) errors.Add(nameof(CreateServiceCatalogDto.EffectiveTo));
        if (note is not null && note.Length > 500) errors.Add(nameof(CreateServiceCatalogDto.Note));
        if (description is not null && description.Length > 500) errors.Add(nameof(CreateServiceCatalogDto.Description));
        return errors;
    }
}
