using Web.Features.Utilities;
using Web.Data.Seeding;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Rules;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityTariffRulesTests
{
    [Theory]
    [InlineData(25, 49_600)]
    [InlineData(50, 99_200)]
    [InlineData(51, 101_250)]
    [InlineData(100, 201_700)]
    [InlineData(101, 204_080)]
    [InlineData(200, 439_700)]
    [InlineData(201, 442_698)]
    [InlineData(300, 739_500)]
    [InlineData(301, 742_850)]
    [InlineData(400, 1_074_500)]
    [InlineData(401, 1_077_960)]
    public void Calculate_ElectricityTierAndBoundaryMatchesPublishedProgressiveRates(decimal kwh, decimal expected)
    {
        var tiers = UtilityTariffSeedData.Tiers.Where(tier => tier.UtilityTariffId == 1).ToArray();
        var lines = UtilityTariffRules.Calculate(kwh, tiers);

        Assert.Equal(expected, lines.Sum(line => line.Amount));
    }

    [Theory]
    [InlineData(1, 4, 26_800)]
    [InlineData(1, 5, 39_700)]
    [InlineData(1, 6, 52_600)]
    [InlineData(1, 7, 67_000)]
    [InlineData(2, 8, 53_600)]
    [InlineData(2, 9, 66_500)]
    public void Calculate_SawacoWaterBoundariesUsePerResidentAllowance(
        int residentCount,
        decimal cubicMeters,
        decimal expected)
    {
        var tiers = UtilityTariffSeedData.Tiers.Where(tier => tier.UtilityTariffId == 2).ToArray();

        var lines = UtilityTariffRules.Calculate(cubicMeters, tiers, tierLimitMultiplier: residentCount);

        Assert.Equal(expected, lines.Sum(line => line.Amount));
    }

    [Fact]
    public void SelectEffective_UsesScheduleEffectiveAtPeriodStart_AndOldSnapshotRetainsOldPrices()
    {
        var oldTariff = new UtilityTariff
        {
            Id = 1,
            Type = TariffType.Electric,
            ScopeCode = "TEST",
            EffectiveDate = new DateOnly(2025, 1, 1),
            Tiers = [new UtilityTariffTier { TierNumber = 1, From = 0m, To = null, UnitPrice = 1_000m }],
        };
        var newTariff = new UtilityTariff
        {
            Id = 2,
            Type = TariffType.Electric,
            ScopeCode = "TEST",
            EffectiveDate = new DateOnly(2026, 1, 1),
            Tiers = [new UtilityTariffTier { TierNumber = 1, From = 0m, To = null, UnitPrice = 1_200m }],
        };
        var schedules = new[] { oldTariff, newTariff };

        var oldPeriodTariff = UtilityTariffRules.QueryEffective(
            schedules.AsQueryable(), TariffType.Electric, "TEST", new DateOnly(2025, 12, 1)).First();
        var oldCostSnapshot = UtilityTariffRules.Calculate(10m, oldPeriodTariff!.Tiers.ToArray());
        var nextPeriodTariff = UtilityTariffRules.QueryEffective(
            schedules.AsQueryable(), TariffType.Electric, "TEST", new DateOnly(2026, 1, 1)).First();
        var newCostSnapshot = UtilityTariffRules.Calculate(10m, nextPeriodTariff!.Tiers.ToArray());

        Assert.Equal(10_000m, oldCostSnapshot.Sum(line => line.Amount));
        Assert.Equal(12_000m, newCostSnapshot.Sum(line => line.Amount));
        Assert.Equal(1_000m, Assert.Single(oldCostSnapshot).Price);
    }

    [Fact]
    public void SeededTariffs_AreLockedVersionedAndHaveTraceableSources()
    {
        Assert.Equal(5, UtilityTariffSeedData.Tariffs.Length);
        Assert.All(UtilityTariffSeedData.Tariffs, tariff =>
        {
            Assert.True(tariff.IsReadOnly);
            Assert.False(string.IsNullOrWhiteSpace(tariff.SourceName));
            Assert.False(string.IsNullOrWhiteSpace(tariff.SourceReference));
        });
        Assert.Contains(UtilityTariffSeedData.Tariffs, tariff =>
            tariff.Type == TariffType.Electric && tariff.EffectiveDate == new DateOnly(2025, 5, 10));
        Assert.Contains(UtilityTariffSeedData.Tariffs, tariff =>
            tariff.Type == TariffType.Water && tariff.IsPerResident && tariff.EffectiveDate == new DateOnly(2026, 1, 1));
        Assert.Contains(UtilityTariffSeedData.Tariffs, tariff =>
            tariff.Type == TariffType.Electric && tariff.EffectiveDate == new DateOnly(2024, 10, 11));
        Assert.Contains(UtilityTariffSeedData.Tariffs, tariff =>
            tariff.Type == TariffType.Water && tariff.EffectiveDate == new DateOnly(2025, 1, 1));
    }

    [Fact]
    public void SplitByEffectiveDates_SeparatesInclusivePeriodAndKeepsEffectiveDateInNewSegment()
    {
        var segments = UtilityTariffRules.SplitByEffectiveDates(
            new DateOnly(2024, 10, 1),
            new DateOnly(2024, 10, 31),
            [new DateOnly(2024, 10, 11)]);

        Assert.Equal(
            [new TariffPeriodSegment(new DateOnly(2024, 10, 1), new DateOnly(2024, 10, 10)),
             new TariffPeriodSegment(new DateOnly(2024, 10, 11), new DateOnly(2024, 10, 31))],
            segments);
    }

    [Fact]
    public void AllocateQuantityByDays_DistributesUsageProportionallyAndPreservesTotal()
    {
        var beforeChange = UtilityTariffRules.AllocateQuantityByDays(310m, segmentDays: 10, totalDays: 31);
        var afterChange = UtilityTariffRules.AllocateQuantityByDays(310m, segmentDays: 21, totalDays: 31);

        Assert.Equal(100m, beforeChange);
        Assert.Equal(210m, afterChange);
        Assert.Equal(310m, beforeChange + afterChange);
    }

    [Fact]
    public void Calculate_ProratesTierAllowanceForPartialTariffSegment()
    {
        UtilityTariffTier[] tiers =
        [
            new() { TierNumber = 1, From = 0m, To = 5m, UnitPrice = 10m },
            new() { TierNumber = 2, From = 5m, To = null, UnitPrice = 20m },
        ];

        var lines = UtilityTariffRules.Calculate(4m, tiers, tierLimitMultiplier: 0.5m);

        Assert.Equal(new[] { 2.5m, 1.5m }, lines.Select(line => line.Quantity));
        Assert.Equal(55m, lines.Sum(line => line.Amount));
    }

    [Fact]
    public void Calculate_RoundsEachCostLineToWholeVndAndKeepsNormalizedQuantity()
    {
        UtilityTariffTier[] tiers =
        [
            new() { TierNumber = 1, From = 0m, To = 0.5m, UnitPrice = 1m },
            new() { TierNumber = 2, From = 0.5m, To = 1m, UnitPrice = 1m },
            new() { TierNumber = 3, From = 1m, To = null, UnitPrice = 1m },
        ];

        var normalizedQuantity = UtilityTariffRules.NormalizeQuantity(1.00005m);
        var lines = UtilityTariffRules.Calculate(normalizedQuantity, tiers);

        Assert.Equal(1.0001m, normalizedQuantity);
        Assert.Equal(normalizedQuantity, lines.Sum(line => line.Quantity));
        Assert.All(lines, line => Assert.Equal(decimal.Truncate(line.Amount), line.Amount));
        Assert.Equal(2m, lines.Sum(line => line.Amount));
    }

    [Fact]
    public void Calculate_ZeroUsageHasNoCostLines()
    {
        var tiers = UtilityTariffSeedData.Tiers.Where(tier => tier.UtilityTariffId == 1).ToArray();

        var lines = UtilityTariffRules.Calculate(0m, tiers);

        Assert.Empty(lines);
    }

    [Fact]
    public void SplitByEffectiveDates_HandlesEffectiveDateAtPeriodStartAndEnd()
    {
        var start = new DateOnly(2024, 10, 1);
        var end = new DateOnly(2024, 10, 31);

        var startsOnEffectiveDate = UtilityTariffRules.SplitByEffectiveDates(start, end, [start]);
        var endsOnEffectiveDate = UtilityTariffRules.SplitByEffectiveDates(start, end, [end]);

        Assert.Equal([new TariffPeriodSegment(start, end)], startsOnEffectiveDate);
        Assert.Equal(
            [new TariffPeriodSegment(start, end.AddDays(-1)), new TariffPeriodSegment(end, end)],
            endsOnEffectiveDate);
    }

    [Fact]
    public void Calculate_RejectsNegativeUsageAndMalformedTierSchedule()
    {
        var electricTiers = UtilityTariffSeedData.Tiers.Where(tier => tier.UtilityTariffId == 1).ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => UtilityTariffRules.Calculate(-1m, electricTiers));

        UtilityTariffTier[] gap =
        [
            new() { TierNumber = 1, From = 0m, To = 4m, UnitPrice = 1m },
            new() { TierNumber = 2, From = 5m, To = null, UnitPrice = 2m },
        ];
        Assert.Throws<InvalidOperationException>(() => UtilityTariffRules.Calculate(10m, gap));
    }
}
