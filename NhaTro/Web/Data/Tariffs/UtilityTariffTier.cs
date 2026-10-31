namespace Web.Features.Utilities.Entities;

/// <summary>
/// A progressive marginal tier. From is the cumulative lower threshold; To is the exclusive upper
/// threshold, and a null To marks the final unbounded tier.
/// </summary>
public sealed class UtilityTariffTier
{
    public int Id { get; set; }

    public int UtilityTariffId { get; set; }

    public UtilityTariff UtilityTariff { get; set; } = null!;

    public int TierNumber { get; set; }

    public decimal From { get; set; }

    public decimal? To { get; set; }

    public decimal UnitPrice { get; set; }
}
