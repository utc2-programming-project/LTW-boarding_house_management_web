using Web.Features.Utilities.Entities;
using Web.Features.Utilities;

namespace Web.Data.Tariffs;

/// <summary>Immutable utility charge snapshot for one utility and billing period.</summary>
public sealed class UtilityBillingPeriodSnapshot
{
    public int Id { get; set; }

    public TariffType Type { get; set; }

    public string ScopeCode { get; set; } = string.Empty;

    public int BillingPeriodId { get; set; }

    public string Unit { get; set; } = string.Empty;

    public DateOnly EffectiveDate { get; set; }

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public decimal Quantity { get; set; }

    public int ResidentCount { get; set; }

    public decimal Total { get; set; }

    public string SourceName { get; set; } = string.Empty;

    public string SourceReference { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ICollection<UtilityBillingCostLineSnapshot> CostLines { get; set; } = new List<UtilityBillingCostLineSnapshot>();
}
