using Web.Common.Utils;
using Web.Features.Utilities.Rules;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityCalculationRulesTests
{
    [Fact]
    public void CalculateElectricityTiered_WithZeroUsage_ReturnsZeroAndNoTiers()
    {
        var result = UtilityDraftRules.CalculateElectricityTiered(0);

        Assert.Equal(0m, result.Total);
        Assert.Empty(result.TiersApplied);
    }

    [Fact]
    public void CalculateElectricityTiered_WithinFirstTier_CalculatesCorrectly()
    {
        var result = UtilityDraftRules.CalculateElectricityTiered(40);

        Assert.Equal(72_000m, result.Total);
        var tier = Assert.Single(result.TiersApplied);
        Assert.Equal(1, tier.TierId);
        Assert.Equal(40m, tier.Units);
        Assert.Equal(1_800m, tier.Rate);
    }

    [Fact]
    public void CalculateElectricityTiered_AcrossTwoTiers_CalculatesCorrectly()
    {
        var result = UtilityDraftRules.CalculateElectricityTiered(80);

        Assert.Equal(150_000m, result.Total);
        Assert.Equal([50m, 30m], result.TiersApplied.Select(tier => tier.Units));
        Assert.Equal([1, 2], result.TiersApplied.Select(tier => tier.TierId));
    }

    [Fact]
    public void CalculateElectricityTiered_AcrossAllThreeTiers_CalculatesExact120Kwh()
    {
        var result = UtilityDraftRules.CalculateElectricityTiered(120);

        Assert.Equal(250_000m, result.Total);
        Assert.Equal([50m, 50m, 20m], result.TiersApplied.Select(tier => tier.Units));
        Assert.Equal([1_800m, 2_000m, 3_000m], result.TiersApplied.Select(tier => tier.Rate));
    }

    [Fact]
    public void CalculateWaterCost_WithUsage_CalculatesCorrectly()
    {
        var result = UtilityDraftRules.CalculateWaterCost(6, 15_000m);

        Assert.Equal(90_000m, result.Total);
    }

    [Fact]
    public void CalculateTotalMonthlyBill_MatchesSampleDto_Total3470000()
    {
        var bill = UtilityDraftRules.CalculateTotalMonthlyBill(
            roomRent: 3_000_000m,
            electricityUsageKwh: 120m,
            waterUsageM3: 6m,
            waterRate: 15_000m,
            internetCost: 100_000m,
            cleaningCost: 30_000m,
            parkingCost: 0m);

        Assert.Equal(3_000_000m, bill.RoomRent);
        Assert.Equal(250_000m, bill.ElectricityCost);
        Assert.Equal(90_000m, bill.WaterCost);
        Assert.Equal(130_000m, bill.ServiceCost);
        Assert.Equal(3_470_000m, bill.TotalCost);
        Assert.Equal("3.470.000 ₫", MoneyFormatter.Format(bill.TotalCost));
    }
}
