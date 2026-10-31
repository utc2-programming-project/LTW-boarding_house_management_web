using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Utilities;

public sealed class ServiceCatalogServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_AppendsVersionWithoutChangingHistoricalSnapshot()
    {
        await fixture.ResetAsync();
        var oldEffectiveDate = new DateOnly(2026, 1, 1);
        var newEffectiveDate = new DateOnly(2026, 12, 1);
        int catalogId;

        await using (var db = fixture.CreateContext())
        {
            var service = new ServiceCatalogService(db);
            var created = await service.CreateOrUpdateServicePriceAsync(
                CreateRequest("Internet", "phòng", "fixed-monthly", 100_000m, oldEffectiveDate),
                1,
                Roles.ChuTro);
            catalogId = created.Id;

            var updated = await service.CreateOrUpdateServicePriceAsync(
                catalogId,
                UpdateRequest("Internet", "phòng", "fixed-monthly", 150_000m, newEffectiveDate),
                1,
                Roles.ChuTro);

            Assert.Equal(2, updated.PriceHistory.Count);
        }

        await using var reloadedDb = fixture.CreateContext();
        var savedCatalog = await reloadedDb.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
            .SingleAsync(catalog => catalog.Id == catalogId);
        var oldVersion = Assert.Single(savedCatalog.PriceHistory, version => version.EffectiveDate == oldEffectiveDate);
        var newVersion = Assert.Single(savedCatalog.PriceHistory, version => version.EffectiveDate == newEffectiveDate);

        Assert.Equal(100_000m, oldVersion.UnitPrice);
        Assert.Equal(150_000m, newVersion.UnitPrice);
        Assert.Equal("phòng", oldVersion.Unit);
        Assert.Equal("fixed-monthly", oldVersion.CalculationMethod);
    }

    [SqlServerFact]
    public async Task GetServiceCatalogsAsync_AllowsLandlordManagerAndTenant()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        db.ServiceCatalogs.Add(new ServiceCatalog
        {
            PriceHistory =
            [
                new ServicePriceHistory
                {
                    Name = "Internet",
                    Unit = "phòng",
                    CalculationMethod = "fixed-monthly",
                    UnitPrice = 100_000m,
                    EffectiveDate = new DateOnly(2026, 1, 1),
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = 1,
                },
            ],
        });
        await db.SaveChangesAsync();
        var service = new ServiceCatalogService(db);

        Assert.Single(await service.GetServiceCatalogsAsync(Roles.ChuTro));
        Assert.Single(await service.GetServiceCatalogsAsync(Roles.QuanLy));
        Assert.Single(await service.GetServiceCatalogsAsync(Roles.KhachThue));
    }

    [SqlServerFact]
    public async Task GetServiceCatalogsAsync_SelectsMostRecentPriceEffectiveToday()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        db.ServiceCatalogs.Add(CatalogWithPrices(
            ("Earlier", 100_000m, new DateOnly(2026, 1, 1)),
            ("Current", 120_000m, new DateOnly(2026, 10, 1)),
            ("Upcoming", 150_000m, new DateOnly(2026, 12, 1))));
        await db.SaveChangesAsync();

        var service = new ServiceCatalogService(
            db,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));

        var catalog = Assert.Single(await service.GetServiceCatalogsAsync(Roles.ChuTro));
        Assert.Equal("Current", catalog.Name);
        Assert.Equal(120_000m, catalog.UnitPrice);
        Assert.Equal(new DateOnly(2026, 10, 1), catalog.EffectiveDate);
    }

    [SqlServerFact]
    public async Task GetServiceCatalogsAsync_ReturnsNextScheduledPriceWhenAllVersionsAreFuture()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        db.ServiceCatalogs.Add(CatalogWithPrices(
            ("Later", 150_000m, new DateOnly(2026, 12, 1)),
            ("Next", 120_000m, new DateOnly(2026, 11, 1))));
        await db.SaveChangesAsync();

        var service = new ServiceCatalogService(
            db,
            new FixedTimeProvider(new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero)));

        var catalog = Assert.Single(await service.GetServiceCatalogsAsync(Roles.ChuTro));
        Assert.Equal("Next", catalog.Name);
        Assert.Equal(120_000m, catalog.UnitPrice);
        Assert.Equal(new DateOnly(2026, 11, 1), catalog.EffectiveDate);
    }

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsDuplicateEffectiveDate()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new ServiceCatalogService(db);
        var effectiveDate = new DateOnly(2026, 11, 1);
        var catalog = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest("Internet", "phòng", "fixed-monthly", 100_000m, effectiveDate),
            1,
            Roles.ChuTro);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrUpdateServicePriceAsync(
                catalog.Id,
                UpdateRequest("Internet", "phòng", "fixed-monthly", 150_000m, effectiveDate),
                1,
                Roles.ChuTro));

        Assert.Contains("hiệu lực", exception.Message, StringComparison.OrdinalIgnoreCase);

        await using var verificationDb = fixture.CreateContext();
        var storedHistory = await verificationDb.ServicePriceHistories
            .Where(version => version.ServiceCatalogId == catalog.Id)
            .ToListAsync();
        var storedVersion = Assert.Single(storedHistory);
        Assert.Equal(100_000m, storedVersion.UnitPrice);
    }

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_CannotChangeReadOnlyTariff()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var catalog = new ServiceCatalog
        {
            IsReadOnly = true,
            PriceHistory =
            [
                new ServicePriceHistory
                {
                    Name = "Electricity",
                    Unit = "kWh",
                    CalculationMethod = "tiered",
                    UnitPrice = 1_800m,
                    EffectiveDate = new DateOnly(2026, 1, 1),
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = 0,
                },
            ],
        };
        db.ServiceCatalogs.Add(catalog);
        await db.SaveChangesAsync();
        var service = new ServiceCatalogService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateOrUpdateServicePriceAsync(
            catalog.Id,
            UpdateRequest("Electricity", "kWh", "tiered", 2_000m, new DateOnly(2026, 12, 1)),
            1,
            Roles.ChuTro));
    }

    private static CreateServiceCatalogDto CreateRequest(
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate) => new()
    {
        Name = name,
        Unit = unit,
        CalculationMethod = calculationMethod,
        UnitPrice = unitPrice,
        EffectiveDate = effectiveDate,
    };

    private static UpdateServicePriceDto UpdateRequest(
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate) => new()
    {
        Name = name,
        Unit = unit,
        CalculationMethod = calculationMethod,
        UnitPrice = unitPrice,
        EffectiveDate = effectiveDate,
    };

    private static ServiceCatalog CatalogWithPrices(params (string Name, decimal Price, DateOnly Date)[] prices)
    {
        var catalog = new ServiceCatalog();
        foreach (var (name, price, date) in prices)
        {
            catalog.PriceHistory.Add(new ServicePriceHistory
            {
                Name = name,
                Unit = "phòng",
                CalculationMethod = "fixed-monthly",
                UnitPrice = price,
                EffectiveDate = date,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = 1,
            });
        }

        return catalog;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
