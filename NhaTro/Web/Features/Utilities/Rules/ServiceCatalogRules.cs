using Web.Features.Utilities.DTOs;

namespace Web.Features.Utilities.Rules;

public static class ServiceCatalogRules
{
    public static IReadOnlyList<string> Validate(CreateServiceCatalogDto request) =>
        Validate(request.Name, request.Unit, request.CalculationMethod, request.UnitPrice, request.EffectiveDate);

    public static IReadOnlyList<string> Validate(UpdateServicePriceDto request) =>
        Validate(request.Name, request.Unit, request.CalculationMethod, request.UnitPrice, request.EffectiveDate);

    private static IReadOnlyList<string> Validate(
        string? name,
        string? unit,
        string? calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(name)) errors.Add(nameof(CreateServiceCatalogDto.Name));
        if (string.IsNullOrWhiteSpace(unit)) errors.Add(nameof(CreateServiceCatalogDto.Unit));
        if (string.IsNullOrWhiteSpace(calculationMethod)) errors.Add(nameof(CreateServiceCatalogDto.CalculationMethod));
        if (unitPrice < 0m) errors.Add(nameof(CreateServiceCatalogDto.UnitPrice));
        if (effectiveDate == default) errors.Add(nameof(CreateServiceCatalogDto.EffectiveDate));
        return errors;
    }
}
