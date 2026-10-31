using Web.Features.Utilities.Entities;

namespace Web.Data.Tariffs;

/// <summary>Immutable price and amount applied to one consumption tier in a billing period.</summary>
public sealed class UtilityBillingCostLineSnapshot
{
    public int Id { get; set; }

    public int UtilityBillingPeriodSnapshotId { get; set; }

    public UtilityBillingPeriodSnapshot PeriodSnapshot { get; set; } = null!;

    public int TierNumber { get; set; }

    public int TariffVersionId { get; set; }

    public UtilityTariff TariffVersion { get; set; } = null!;

    public DateOnly EffectiveDate { get; set; }

    public decimal From { get; set; }

    public decimal? To { get; set; }

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal Amount { get; set; }
}
