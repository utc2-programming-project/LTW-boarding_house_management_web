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

        Assert.Single(await service.GetServiceCatalogsAsync(1, Roles.ChuTro));
        Assert.Single(await service.GetServiceCatalogsAsync(1, Roles.QuanLy));
        Assert.Single(await service.GetServiceCatalogsAsync(1, Roles.KhachThue));
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
}
