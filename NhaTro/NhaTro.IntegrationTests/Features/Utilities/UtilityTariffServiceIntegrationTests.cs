using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Data.Seeding;
using Web.Features.Utilities;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Rules;
using Web.Features.Utilities.Services;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Utilities;

public sealed class UtilityTariffServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task CalculateForPeriodAsync_SelectsTariffEffectiveAtPeriodStart()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new UtilityTariffService(db);

        var result = await service.CalculateForPeriodAsync(
            TariffType.Electric,
            "VN-EVN-HOUSEHOLD",
            new DateOnly(2024, 12, 1),
            new DateOnly(2024, 12, 31),
            quantity: 10m);

        Assert.Equal(new DateOnly(2024, 10, 11), result.EffectiveFrom);
        Assert.Equal(4, result.VersionId);
        Assert.Equal(18_930m, result.Total);
    }

    [SqlServerFact]
    public async Task CalculateForPeriodAsync_SplitsUsageProportionallyAcrossTariffEffectiveDate()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new UtilityTariffService(db);

        var result = await service.CalculateForPeriodAsync(
            TariffType.Electric,
            "VN-EVN-HOUSEHOLD",
            new DateOnly(2024, 10, 1),
            new DateOnly(2024, 10, 31),
            quantity: 310m);

        var oldTariffCost = UtilityTariffRules.Calculate(
            100m,
            UtilityTariffSeedData.Tiers.Where(tier => tier.UtilityTariffId == 3).ToArray(),
            tierLimitMultiplier: 10m / 31m).Sum(line => line.Amount);
        var newTariffCost = UtilityTariffRules.Calculate(
            210m,
            UtilityTariffSeedData.Tiers.Where(tier => tier.UtilityTariffId == 4).ToArray(),
            tierLimitMultiplier: 21m / 31m).Sum(line => line.Amount);
        Assert.Equal(MoneyFormatter.RoundVnd(oldTariffCost + newTariffCost), result.Total);
        Assert.Equal(result.Tiers.Sum(tier => tier.Amount), result.Total);
        Assert.Equal(new[] { 3, 4 }, result.TariffVersionIds);
        Assert.Equal(310m, result.Tiers.Sum(tier => tier.Quantity));
        Assert.Equal(100m, result.Tiers.Where(tier => tier.TariffVersionId == 3).Sum(tier => tier.Quantity));
        Assert.Equal(210m, result.Tiers.Where(tier => tier.TariffVersionId == 4).Sum(tier => tier.Quantity));
    }

    [SqlServerFact]
    public async Task CalculateForPeriodAsync_UsesVersionFourInAprilAndVersionOneInJune2025()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new UtilityTariffService(db);

        var april = await service.CalculateForPeriodAsync(
            TariffType.Electric,
            "VN-EVN-HOUSEHOLD",
            new DateOnly(2025, 4, 1),
            new DateOnly(2025, 4, 30),
            quantity: 10m);
        var june = await service.CalculateForPeriodAsync(
            TariffType.Electric,
            "VN-EVN-HOUSEHOLD",
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 30),
            quantity: 10m);

        Assert.Equal(4, april.VersionId);
        Assert.Equal(1_893m, april.Tiers.Single(tier => tier.Quantity > 0m).Price);
        Assert.Equal(18_930m, april.Total);
        Assert.Equal(1, june.VersionId);
        Assert.Equal(1_984m, june.Tiers.Single(tier => tier.Quantity > 0m).Price);
        Assert.Equal(19_840m, june.Total);
    }

    [SqlServerFact]
    public async Task CalculateForPeriodAsync_ZeroUsageReturnsZeroTotal()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new UtilityTariffService(db);

        var result = await service.CalculateForPeriodAsync(
            TariffType.Electric,
            "VN-EVN-HOUSEHOLD",
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 30),
            quantity: 0m);

        Assert.Equal(0m, result.Total);
        Assert.All(result.Tiers, tier => Assert.Equal(0m, tier.Amount));
    }

    [SqlServerFact]
    public async Task CalculateForPeriodAsync_UsesEffectiveTariffAtPeriodStartAndEndBoundaries()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new UtilityTariffService(db);

        var startsOnChange = await service.CalculateForPeriodAsync(
            TariffType.Electric,
            "VN-EVN-HOUSEHOLD",
            new DateOnly(2024, 10, 11),
            new DateOnly(2024, 10, 31),
            quantity: 10m);
        var endsOnChange = await service.CalculateForPeriodAsync(
            TariffType.Electric,
            "VN-EVN-HOUSEHOLD",
            new DateOnly(2024, 10, 1),
            new DateOnly(2024, 10, 11),
            quantity: 11m);

        Assert.Equal(new[] { 4 }, startsOnChange.TariffVersionIds);
        Assert.Equal(new[] { 3, 4 }, endsOnChange.TariffVersionIds);
        Assert.Equal(endsOnChange.Tiers.Sum(tier => tier.Amount), endsOnChange.Total);
    }

    [SqlServerFact]
    public async Task SnapshotForPeriodAsync_WaterCrossoverPersistsRoundedLinesAndReadsThemBack()
    {
        await fixture.ResetAsync();
        int snapshotId;

        await using (var db = fixture.CreateContext())
        {
            var service = new UtilityTariffService(db);
            var result = await service.SnapshotForPeriodAsync(
                84,
                TariffType.Water,
                "HCMC-BENTHANH",
                new DateOnly(2025, 12, 20),
                new DateOnly(2026, 1, 10),
                quantity: 22m,
                residentCount: 2);

            snapshotId = result.SnapshotId;
            Assert.Equal(new[] { 5, 2 }, result.TariffVersionIds);
            Assert.Equal(result.Tiers.Sum(tier => tier.Amount), result.Total);
        }

        await using var readDb = fixture.CreateContext();
        var saved = await new UtilityTariffService(readDb).GetPeriodSnapshotAsync(snapshotId);

        Assert.NotNull(saved);
        Assert.Equal(new[] { 5, 2 }, saved.TariffVersionIds);
        Assert.Equal(saved.Quantity, saved.Tiers.Sum(tier => tier.Quantity));
        Assert.All(saved.Tiers, tier => Assert.Equal(decimal.Truncate(tier.Amount), tier.Amount));
        Assert.Equal(saved.Tiers.Sum(tier => tier.Amount), saved.Total);
    }

    [SqlServerFact]
    public async Task CalculateForPeriodAsync_RejectsMissingTariff()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new UtilityTariffService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CalculateForPeriodAsync(
            TariffType.Water,
            "NO-SUCH-SCOPE",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            quantity: 1m));
    }

    [SqlServerFact]
    public async Task CalculateForPeriodAsync_AppliesWaterAllowancePerResident()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new UtilityTariffService(db);

        var result = await service.CalculateForPeriodAsync(
            TariffType.Water,
            "HCMC-BENTHANH",
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31),
            quantity: 10m,
            residentCount: 2);

        Assert.Equal(2, result.ResidentCount);
        Assert.Equal(79_400m, result.Total);
        Assert.Equal(new[] { 8m, 2m }, result.Tiers.Where(tier => tier.Quantity > 0m).Select(tier => tier.Quantity));
    }

    [SqlServerFact]
    public async Task SnapshotForPeriodAsync_ReadsOldPeriodSnapshotAfterNewTariffIsAdded()
    {
        await fixture.ResetAsync();
        int snapshotId;

        await using (var db = fixture.CreateContext())
        {
            var service = new UtilityTariffService(db);
            var saved = await service.SnapshotForPeriodAsync(
                42,
                TariffType.Electric,
                "VN-EVN-HOUSEHOLD",
                new DateOnly(2025, 6, 1),
                new DateOnly(2025, 6, 30),
                quantity: 10m);
            snapshotId = saved.SnapshotId;

            db.UtilityTariffs.Add(new UtilityTariff
            {
                Type = TariffType.Electric,
                ScopeCode = "VN-EVN-HOUSEHOLD",
                Unit = "kWh",
                IsPerResident = false,
                EffectiveDate = new DateOnly(2027, 1, 1),
                IsReadOnly = true,
                SourceName = "Test-reviewed-source",
                SourceReference = "https://example.test/test-tariff",
                Tiers = [new UtilityTariffTier { TierNumber = 1, From = 0m, To = null, UnitPrice = 9_999m }],
            });
            await db.SaveChangesAsync();
        }

        await using var readDb = fixture.CreateContext();
        var serviceForRead = new UtilityTariffService(readDb);
        var oldPeriod = await serviceForRead.GetPeriodSnapshotAsync(snapshotId);
        var retriedSnapshot = await serviceForRead.SnapshotForPeriodAsync(
            42,
            TariffType.Electric,
            "VN-EVN-HOUSEHOLD",
            new DateOnly(2025, 6, 1),
            new DateOnly(2025, 6, 30),
            quantity: 999m);

        Assert.NotNull(oldPeriod);
        Assert.Equal(snapshotId, oldPeriod.SnapshotId);
        Assert.Equal(1, oldPeriod.VersionId);
        Assert.Equal(1_984m, oldPeriod.Tiers.First(tier => tier.Quantity > 0m).Price);
        Assert.Equal(19_840m, oldPeriod.Total);
        Assert.Equal(new DateOnly(2025, 6, 1), oldPeriod.PeriodStart);
        Assert.Contains(1, oldPeriod.TariffVersionIds!);
        Assert.Equal(19_840m, retriedSnapshot.Total);
        Assert.Equal(10m, retriedSnapshot.Quantity);
    }
}
