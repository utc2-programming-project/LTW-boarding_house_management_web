using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;

namespace Web.Features.Utilities.Rules;

public static class UtilityTariffRules
{
    public static void EnsurePeriodUsesSingleTariff(
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly? nextEffectiveDate)
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException("The billing period end must be on or after its start.", nameof(periodEnd));
        }

        if (nextEffectiveDate is { } effectiveDate
            && effectiveDate > periodStart
            && effectiveDate <= periodEnd)
        {
            throw new InvalidOperationException(
                "The billing period crosses a tariff change and must be split at the effective date.");
        }
    }

    public static IQueryable<UtilityTariff> QueryEffective(
        IQueryable<UtilityTariff> tariffs,
        TariffType type,
        string scopeCode,
        DateOnly periodStart) => tariffs
        .Where(tariff => tariff.Type == type
            && tariff.ScopeCode == scopeCode
            && tariff.EffectiveDate <= periodStart)
        .OrderByDescending(tariff => tariff.EffectiveDate);

    public static IReadOnlyList<UtilityTariffCostLineDto> Calculate(
        decimal quantity,
        IReadOnlyList<UtilityTariffTier> tiers,
        int tierLimitMultiplier = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        ArgumentNullException.ThrowIfNull(tiers);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tierLimitMultiplier);

        var ordered = tiers.OrderBy(tier => tier.TierNumber).ToArray();
        if (ordered.Length == 0)
        {
            throw new InvalidOperationException("A tariff must contain at least one tier.");
        }

        decimal previousTo = 0m;
        for (var index = 0; index < ordered.Length; index++)
        {
            var tier = ordered[index];
            if (tier.TierNumber != index + 1 || tier.From != previousTo || tier.UnitPrice < 0m)
            {
                throw new InvalidOperationException("Tariff tiers must be continuous, ordered, and have non-negative prices.");
            }

            if (tier.To is null && index != ordered.Length - 1)
            {
                throw new InvalidOperationException("Only the final tariff tier may have no upper limit.");
            }

            if (tier.To is not null && tier.To <= tier.From)
            {
                throw new InvalidOperationException("A tariff tier upper limit must exceed its lower limit.");
            }

            previousTo = tier.To ?? previousTo;
        }

        if (ordered[^1].To is not null)
        {
            throw new InvalidOperationException("The final tariff tier must have no upper limit.");
        }

        var lines = new List<UtilityTariffCostLineDto>();
        foreach (var tier in ordered)
        {
            var from = tier.From * tierLimitMultiplier;
            var to = tier.To * tierLimitMultiplier;
            var tierQuantity = Math.Max(0m, Math.Min(quantity, to ?? quantity) - from);
            if (tierQuantity > 0m)
            {
                lines.Add(new UtilityTariffCostLineDto(
                    tier.TierNumber,
                    tierQuantity,
                    tier.UnitPrice,
                    tierQuantity * tier.UnitPrice));
            }
        }

        return lines;
    }
}
