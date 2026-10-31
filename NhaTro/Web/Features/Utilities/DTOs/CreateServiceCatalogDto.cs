namespace Web.Features.Utilities.DTOs;

public sealed class CreateServiceCatalogDto
{
    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string CalculationMethod { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public DateOnly EffectiveDate { get; set; }

    /// <summary>Null means the price applies to every property.</summary>
    public int? PropertyId { get; set; }
}
