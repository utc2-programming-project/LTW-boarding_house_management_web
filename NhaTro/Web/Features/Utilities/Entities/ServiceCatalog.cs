namespace Web.Features.Utilities.Entities;

/// <summary>A service identity and its scope. Price details are stored as immutable versions.</summary>
public sealed class ServiceCatalog
{
    public int Id { get; set; }

    /// <summary>Null means this catalog applies to every property.</summary>
    public int? PropertyId { get; set; }

    /// <summary>Used for system supplied electricity/water tariffs which users may only view.</summary>
    public bool IsReadOnly { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ServicePriceHistory> PriceHistory { get; set; } = new List<ServicePriceHistory>();
}
