using Web.Common.Utils;
using Web.Features.Utilities.Rules;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityCalculationRulesTests
{
    [Fact]
    public void CalculateElectricityTiered_WithZeroUsage_ReturnsZero()
    {
        var result = UtilityDraftRules.CalculateElectricityTiered(0);

        Assert.Equal(0m, result.Total);
        Assert.Equal("0 kWh × 0 ₫", result.Breakdown);
        Assert.Empty(result.TiersApplied);
    }

    [Fact]
    public void CalculateElectricityTiered_WithinFirstTier_CalculatesCorrectly()
    {
        var result = UtilityDraftRules.CalculateElectricityTiered(40);

        // 40 * 1800 = 72,000
        Assert.Equal(72_000m, result.Total);
        Assert.Single(result.TiersApplied);
        Assert.Equal(40m, result.TiersApplied[0].Units);
        Assert.Equal(1_800m, result.TiersApplied[0].Rate);
    }

    [Fact]
    public void CalculateElectricityTiered_AcrossTwoTiers_CalculatesCorrectly()
    {
        var result = UtilityDraftRules.CalculateElectricityTiered(80);

        // 50 * 1800 (90,000) + 30 * 2000 (60,000) = 150,000
        Assert.Equal(150_000m, result.Total);
        Assert.Equal(2, result.TiersApplied.Count);
        Assert.Equal(50m, result.TiersApplied[0].Units);
        Assert.Equal(30m, result.TiersApplied[1].Units);
    }

    [Fact]
    public void CalculateElectricityTiered_AcrossAllThreeTiers_CalculatesExact120KwhMatchingDemo()
    {
        var result = UtilityDraftRules.CalculateElectricityTiered(120);

        // 50 * 1800 (90,000) + 50 * 2000 (100,000) + 20 * 3000 (60,000) = 250,000
        Assert.Equal(250_000m, result.Total);
        Assert.Equal(3, result.TiersApplied.Count);
        Assert.Equal(50m, result.TiersApplied[0].Units);
        Assert.Equal(50m, result.TiersApplied[1].Units);
        Assert.Equal(20m, result.TiersApplied[2].Units);
        Assert.Equal("Bậc thang 3 bậc: 50×1.800 + 50×2.000 + 20×3.000", result.Breakdown);
    }

    [Fact]
    public void CalculateWaterCost_WithUsage_CalculatesCorrectly()
    {
        var result = UtilityDraftRules.CalculateWaterCost(6, 15_000m);

        Assert.Equal(90_000m, result.Total);
        Assert.Equal("Đơn giá cố định: 6 m³ × 15.000 ₫", result.Breakdown);
    }

    [Fact]
    public void CalculateTotalMonthlyBill_MatchesDemoScreenshot_Total3470000()
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

