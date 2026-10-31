namespace Web.Features.Utilities.Entities;

/// <summary>Immutable service terms and rate effective from a given date.</summary>
public sealed class ServicePriceHistory
{
    public int Id { get; set; }

    public int ServiceCatalogId { get; set; }

    public ServiceCatalog ServiceCatalog { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string CalculationMethod { get; set; } = string.Empty;

    public decimal UnitPrice { get; set; }

    public DateOnly EffectiveDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedByUserId { get; set; }
}
