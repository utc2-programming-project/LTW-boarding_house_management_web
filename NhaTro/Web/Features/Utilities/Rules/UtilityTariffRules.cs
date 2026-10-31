using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;

namespace Web.Features.Utilities.Rules;

public static class UtilityTariffRules
{
    public static void EnsureValidPeriod(DateOnly periodStart, DateOnly periodEnd)
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException("The billing period end must be on or after its start.", nameof(periodEnd));
        }
    }

    /// <summary>Splits an inclusive billing period at each tariff effective date.</summary>
    public static IReadOnlyList<TariffPeriodSegment> SplitByEffectiveDates(
        DateOnly periodStart,
        DateOnly periodEnd,
        IEnumerable<DateOnly> effectiveDates)
    {
        EnsureValidPeriod(periodStart, periodEnd);
        ArgumentNullException.ThrowIfNull(effectiveDates);

        var boundaries = effectiveDates
            .Where(date => date > periodStart && date <= periodEnd)
            .Distinct()
            .OrderBy(date => date)
            .ToArray();
        var segments = new List<TariffPeriodSegment>();
        var currentStart = periodStart;
        foreach (var boundary in boundaries)
        {
            segments.Add(new TariffPeriodSegment(currentStart, boundary.AddDays(-1)));
            currentStart = boundary;
        }

        segments.Add(new TariffPeriodSegment(currentStart, periodEnd));
        return segments;
    }

    /// <summary>Allocates total period consumption evenly across inclusive calendar days.</summary>
    public static decimal AllocateQuantityByDays(decimal quantity, int segmentDays, int totalDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segmentDays);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalDays);
        if (segmentDays > totalDays)
        {
            throw new ArgumentOutOfRangeException(nameof(segmentDays), "A segment cannot exceed the full period length.");
        }

        return quantity * segmentDays / totalDays;
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

    public static IReadOnlyList<TariffTierDto> Calculate(
        decimal quantity,
        IReadOnlyList<UtilityTariffTier> tiers,
        decimal tierLimitMultiplier = 1m)
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

        var lines = new List<TariffTierDto>();
        foreach (var tier in ordered)
        {
            var from = tier.From * tierLimitMultiplier;
            var to = tier.To * tierLimitMultiplier;
            var tierQuantity = Math.Max(0m, Math.Min(quantity, to ?? quantity) - from);
            if (tierQuantity > 0m)
            {
                lines.Add(new TariffTierDto(
                    tier.From,
                    tier.To,
                    tier.UnitPrice,
                    tier.TierNumber,
                    tierQuantity,
                    tierQuantity * tier.UnitPrice));
            }
        }

        return lines;
    }
}

public sealed record TariffPeriodSegment(DateOnly Start, DateOnly End);
