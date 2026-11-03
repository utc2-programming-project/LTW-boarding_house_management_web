namespace Web.Features.Utilities.DTOs;

public sealed class CreateServiceCatalogDto
{
    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string CalculationMethod { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public DateOnly EffectiveDate { get; set; }

    /// <summary>Optional last day the new price version applies; null means open-ended.</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>Null means the price applies to every property.</summary>
    public int? PropertyId { get; set; }

    /// <summary>Optional note for this initial price version.</summary>
    public string? Note { get; set; }
    public string? Description { get; set; }
}
