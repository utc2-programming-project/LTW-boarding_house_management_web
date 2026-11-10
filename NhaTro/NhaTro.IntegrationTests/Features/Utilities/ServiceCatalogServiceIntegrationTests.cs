using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Utilities;

public sealed class ServiceCatalogServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string OwnerId = "owner-integration-user";
    private const string OtherOwnerId = "other-owner-user";
    private const string ManagerId = "manager-integration-user";
    private const string TenantId = "tenant-integration-user";

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_AppendsVersionWithoutChangingHistoricalSnapshot()
    {
        await fixture.ResetAsync();
        var oldEffectiveDate = new DateOnly(2026, 1, 1);
        var newEffectiveDate = new DateOnly(2026, 12, 1);
        int catalogId;
        int propId;

        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
            var prop = new Property { Name = "Nhà 1", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Properties.Add(prop);
            await db.SaveChangesAsync();
            propId = prop.Id;

            var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
            var service = new ServiceCatalogService(db);
            var created = await service.CreateOrUpdateServicePriceAsync(
                CreateRequest(propId, "Internet", "phòng", "fixed-monthly", 100_000m, oldEffectiveDate),
                caller);
            catalogId = created.Id;

            var updated = await service.CreateOrUpdateServicePriceAsync(
                catalogId,
                UpdateRequest("Internet", "phòng", "fixed-monthly", 150_000m, newEffectiveDate),
                caller);

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
        Assert.Equal(OwnerId, oldVersion.CreatedByUserId);
        Assert.Equal(OwnerId, newVersion.CreatedByUserId);
    }

    [SqlServerFact]
    public async Task GetServiceCatalogsAsync_AllowsLandlordManagerAndTenantForGlobalCatalogs()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        db.ServiceCatalogs.Add(new ServiceCatalog
        {
            PropertyId = null,
            PriceHistory =
            [
                new ServicePriceHistory
                {
                    Name = "Điện chung",
                    Unit = "kWh",
                    CalculationMethod = "meter",
                    UnitPrice = 3_500m,
                    EffectiveDate = new DateOnly(2026, 1, 1),
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = null,
                },
            ],
        });
        await db.SaveChangesAsync();
        var service = new ServiceCatalogService(db);

        Assert.Single(await service.GetServiceCatalogsAsync(new PropertyCaller(OwnerId, Roles.ChuTro)));
        Assert.Single(await service.GetServiceCatalogsAsync(new PropertyCaller(ManagerId, Roles.QuanLy)));
        Assert.Single(await service.GetServiceCatalogsAsync(new PropertyCaller(TenantId, Roles.KhachThue)));
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

        var catalog = Assert.Single(await service.GetServiceCatalogsAsync(new PropertyCaller(OwnerId, Roles.ChuTro)));
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

        var catalog = Assert.Single(await service.GetServiceCatalogsAsync(new PropertyCaller(OwnerId, Roles.ChuTro)));
        Assert.Equal("Next", catalog.Name);
        Assert.Equal(120_000m, catalog.UnitPrice);
        Assert.Equal(new DateOnly(2026, 11, 1), catalog.EffectiveDate);
    }

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsDuplicateEffectiveDate()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        var prop = new Property { Name = "Nhà 1", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);
        var effectiveDate = new DateOnly(2026, 11, 1);
        var catalog = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Internet", "phòng", "fixed-monthly", 100_000m, effectiveDate),
            caller);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrUpdateServicePriceAsync(
                catalog.Id,
                UpdateRequest("Internet", "phòng", "fixed-monthly", 150_000m, effectiveDate),
                caller));

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
                    CreatedByUserId = null,
                },
            ],
        };
        db.ServiceCatalogs.Add(catalog);
        await db.SaveChangesAsync();
        var service = new ServiceCatalogService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateOrUpdateServicePriceAsync(
            catalog.Id,
            UpdateRequest("Electricity", "kWh", "tiered", 2_000m, new DateOnly(2026, 12, 1)),
            new PropertyCaller(OwnerId, Roles.ChuTro)));
    }

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsGlobalCatalogCreation()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var request = CreateRequest(null, "Internet", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 11, 1));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrUpdateServicePriceAsync(request, caller));
        Assert.Contains("PropertyId", ex.Message);
    }

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsCrossPropertyAccess()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwnerId, Roles.ChuTro);

        var otherProp = new Property { Name = "Nhà của chủ khác", OwnerId = OtherOwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(otherProp);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        // Cannot create catalog for a property owned by someone else
        var request = CreateRequest(otherProp.Id, "Internet", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 11, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateOrUpdateServicePriceAsync(request, caller));
    }

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsCrossPropertyUpdate()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwnerId, Roles.ChuTro);

        var otherProp = new Property { Name = "Nhà của chủ khác", OwnerId = OtherOwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(otherProp);
        await db.SaveChangesAsync();

        var otherCatalog = new ServiceCatalog
        {
            PropertyId = otherProp.Id,
            IsReadOnly = false,
            CreatedAt = DateTime.UtcNow,
            PriceHistory = [new ServicePriceHistory
            {
                Name = "Nước",
                Unit = "m³",
                CalculationMethod = "meter",
                UnitPrice = 20_000m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedByUserId = OtherOwnerId,
                CreatedAt = DateTime.UtcNow,
            }]
        };
        db.ServiceCatalogs.Add(otherCatalog);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        // Cannot update catalog belonging to another owner's property
        var updateRequest = UpdateRequest("Nước", "m³", "meter", 25_000m, new DateOnly(2026, 12, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateOrUpdateServicePriceAsync(otherCatalog.Id, updateRequest, caller));
    }

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsUpdatingGlobalCatalog()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var globalCatalog = new ServiceCatalog
        {
            PropertyId = null,
            IsReadOnly = false,
            CreatedAt = DateTime.UtcNow,
            PriceHistory = [new ServicePriceHistory
            {
                Name = "Phí chung",
                Unit = "phòng",
                CalculationMethod = "fixed-monthly",
                UnitPrice = 10_000m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedByUserId = null,
                CreatedAt = DateTime.UtcNow,
            }]
        };
        db.ServiceCatalogs.Add(globalCatalog);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var updateRequest = UpdateRequest("Phí chung", "phòng", "fixed-monthly", 15_000m, new DateOnly(2026, 12, 1));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CreateOrUpdateServicePriceAsync(globalCatalog.Id, updateRequest, caller));
    }

    [SqlServerFact]
    public async Task GetServiceCatalogsAsync_EnforcesPropertyScopeForOwnerManagerAndTenant()
    {
        await fixture.ResetAsync();
        var now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        var clock = new FixedTimeProvider(now);

        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, TenantId, Roles.KhachThue);

        var prop1 = new Property { Name = "Nhà 1 (Owner 1)", OwnerId = OwnerId, IsActive = true, CreatedAt = now };
        var prop2 = new Property { Name = "Nhà 2 (Owner 2)", OwnerId = OtherOwnerId, IsActive = true, CreatedAt = now };
        db.Properties.AddRange(prop1, prop2);
        await db.SaveChangesAsync();

        // Manager assigned to prop2
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = prop2.Id,
            ManagerUserId = ManagerId,
            AssignedByUserId = OtherOwnerId,
            AssignedAt = now,
        });

        // Room & active contract for Tenant in prop1
        var room = new Web.Features.Rooms.Entities.Room { PropertyId = prop1.Id, Code = "R1", StandardPrice = 3000000m, CreatedAt = now };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var tenant = new Tenant { UserId = TenantId, FullName = "Khách 1", CitizenId = "123456789012", Phone = "0900000001", CreatedAt = now, IsActive = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            RoomId = room.Id,
            ContractNumber = "HD-T1",
            StartDate = now.AddMonths(-1),
            EndDate = now.AddMonths(5),
            SnapshotPrice = 3000000m,
            DepositAmount = 3000000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        db.ContractMembers.Add(new ContractMember { ContractId = contract.ContractId, TenantId = tenant.Id });
        await db.SaveChangesAsync();

        // Global catalog, Catalog for Prop1, Catalog for Prop2
        var globalCat = new ServiceCatalog
        {
            PropertyId = null,
            PriceHistory = [new ServicePriceHistory { Name = "Global Rate", Unit = "kWh", CalculationMethod = "meter", UnitPrice = 3500m, EffectiveDate = new DateOnly(2026, 1, 1), CreatedAt = now }]
        };
        var prop1Cat = new ServiceCatalog
        {
            PropertyId = prop1.Id,
            PriceHistory = [new ServicePriceHistory { Name = "Prop1 Water", Unit = "m³", CalculationMethod = "meter", UnitPrice = 20000m, EffectiveDate = new DateOnly(2026, 1, 1), CreatedByUserId = OwnerId, CreatedAt = now }]
        };
        var prop2Cat = new ServiceCatalog
        {
            PropertyId = prop2.Id,
            PriceHistory = [new ServicePriceHistory { Name = "Prop2 Water", Unit = "m³", CalculationMethod = "meter", UnitPrice = 25000m, EffectiveDate = new DateOnly(2026, 1, 1), CreatedByUserId = OtherOwnerId, CreatedAt = now }]
        };
        db.ServiceCatalogs.AddRange(globalCat, prop1Cat, prop2Cat);
        await db.SaveChangesAsync();

        var service = new ServiceCatalogService(db, clock);

        // 1. Owner 1: Sees global + Prop1, does NOT see Prop2
        var owner1Catalogs = await service.GetServiceCatalogsAsync(new PropertyCaller(OwnerId, Roles.ChuTro));
        Assert.Contains(owner1Catalogs, c => c.Name == "Global Rate");
        Assert.Contains(owner1Catalogs, c => c.Name == "Prop1 Water");
        Assert.DoesNotContain(owner1Catalogs, c => c.Name == "Prop2 Water");

        // 2. Manager: Assigned to Prop2, sees global + Prop2, does NOT see Prop1
        var managerCatalogs = await service.GetServiceCatalogsAsync(new PropertyCaller(ManagerId, Roles.QuanLy));
        Assert.Contains(managerCatalogs, c => c.Name == "Global Rate");
        Assert.Contains(managerCatalogs, c => c.Name == "Prop2 Water");
        Assert.DoesNotContain(managerCatalogs, c => c.Name == "Prop1 Water");

        // 3. Tenant: Renting in Prop1, sees global + Prop1, does NOT see Prop2
        var tenantCatalogs = await service.GetServiceCatalogsAsync(new PropertyCaller(TenantId, Roles.KhachThue));
        Assert.Contains(tenantCatalogs, c => c.Name == "Global Rate");
        Assert.Contains(tenantCatalogs, c => c.Name == "Prop1 Water");
        Assert.DoesNotContain(tenantCatalogs, c => c.Name == "Prop2 Water");
    }

    [SqlServerFact]
    public async Task ToggleServiceCatalogStatusAsync_Owner_SuccessfullyTogglesAndPersists()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Toggle", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var created = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Internet", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 1, 1)),
            caller);
        Assert.True(created.IsActive);

        // Toggle to inactive
        var toggledOff = await service.ToggleServiceCatalogStatusAsync(created.Id, caller);
        Assert.False(toggledOff.IsActive);

        var dbItem = await db.ServiceCatalogs.FindAsync(created.Id);
        Assert.NotNull(dbItem);
        Assert.False(dbItem.IsActive);

        // Toggle back to active
        var toggledOn = await service.ToggleServiceCatalogStatusAsync(created.Id, caller);
        Assert.True(toggledOn.IsActive);

        await db.Entry(dbItem).ReloadAsync();
        Assert.True(dbItem.IsActive);
    }

    [SqlServerFact]
    public async Task ToggleServiceCatalogStatusAsync_RejectsReadOnlyOrGlobalOrCrossProperty()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwnerId, Roles.ChuTro);

        var otherProp = new Property { Name = "Nhà Chủ Khác", OwnerId = OtherOwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(otherProp);
        await db.SaveChangesAsync();

        var readOnlyCat = new ServiceCatalog
        {
            PropertyId = otherProp.Id,
            IsReadOnly = true,
            PriceHistory = [new ServicePriceHistory { Name = "Điện", Unit = "kWh", CalculationMethod = "meter", UnitPrice = 3000m, EffectiveDate = new DateOnly(2026, 1, 1) }]
        };
        var globalCat = new ServiceCatalog
        {
            PropertyId = null,
            IsReadOnly = false,
            PriceHistory = [new ServicePriceHistory { Name = "Rác", Unit = "phòng", CalculationMethod = "fixed-monthly", UnitPrice = 20000m, EffectiveDate = new DateOnly(2026, 1, 1) }]
        };
        var crossPropCat = new ServiceCatalog
        {
            PropertyId = otherProp.Id,
            IsReadOnly = false,
            PriceHistory = [new ServicePriceHistory { Name = "Nước", Unit = "m3", CalculationMethod = "meter", UnitPrice = 15000m, EffectiveDate = new DateOnly(2026, 1, 1) }]
        };
        db.ServiceCatalogs.AddRange(readOnlyCat, globalCat, crossPropCat);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        // Read-only rejected
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ToggleServiceCatalogStatusAsync(readOnlyCat.Id, caller));

        // Global rejected
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ToggleServiceCatalogStatusAsync(globalCat.Id, caller));

        // Cross-property rejected
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.ToggleServiceCatalogStatusAsync(crossPropCat.Id, caller));
    }

    [SqlServerFact]
    public async Task DeleteServiceCatalogAsync_Owner_SoftDeletesAndKeepsPriceHistory()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Xóa", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);
        var created = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Rác", "phòng", "fixed-monthly", 50_000m, new DateOnly(2026, 1, 1)),
            caller);

        await service.DeleteServiceCatalogAsync(created.Id, caller);

        // Biến mất khỏi danh mục nhưng dữ liệu vẫn còn để đối soát.
        Assert.DoesNotContain(await service.GetServiceCatalogsAsync(caller), catalog => catalog.Id == created.Id);

        await using var checkDb = fixture.CreateContext();
        var deleted = await checkDb.ServiceCatalogs
            .IgnoreQueryFilters()
            .Include(catalog => catalog.PriceHistory)
            .SingleAsync(catalog => catalog.Id == created.Id);

        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
        Assert.Equal(OwnerId, deleted.DeletedBy);
        Assert.False(deleted.IsActive);
        Assert.Single(deleted.PriceHistory);
    }

    [SqlServerFact]
    public async Task DeleteServiceCatalogAsync_RejectsReadOnlyGlobalAndCrossProperty()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwnerId, Roles.ChuTro);

        var otherProp = new Property { Name = "Nhà Chủ Khác", OwnerId = OtherOwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(otherProp);
        await db.SaveChangesAsync();

        var readOnlyCat = new ServiceCatalog
        {
            PropertyId = otherProp.Id,
            IsReadOnly = true,
            PriceHistory = [new ServicePriceHistory { Name = "Điện", Unit = "kWh", CalculationMethod = "meter", UnitPrice = 3000m, EffectiveDate = new DateOnly(2026, 1, 1) }]
        };
        var globalCat = new ServiceCatalog
        {
            PropertyId = null,
            IsReadOnly = false,
            PriceHistory = [new ServicePriceHistory { Name = "Dịch vụ chung", Unit = "tháng", CalculationMethod = "fixed-monthly", UnitPrice = 20_000m, EffectiveDate = new DateOnly(2026, 1, 1) }]
        };
        var crossPropCat = new ServiceCatalog
        {
            PropertyId = otherProp.Id,
            IsReadOnly = false,
            PriceHistory = [new ServicePriceHistory { Name = "Xe", Unit = "xe", CalculationMethod = "fixed-monthly", UnitPrice = 10_000m, EffectiveDate = new DateOnly(2026, 1, 1) }]
        };
        db.ServiceCatalogs.AddRange(readOnlyCat, globalCat, crossPropCat);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        // Read-only (biểu giá điện/nước) rejected
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteServiceCatalogAsync(readOnlyCat.Id, caller));

        // Global rejected
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteServiceCatalogAsync(globalCat.Id, caller));

        // Cross-property rejected
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteServiceCatalogAsync(crossPropCat.Id, caller));
    }

    [SqlServerFact]
    public async Task DeleteServiceCatalogAsync_RejectsManager()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);

        var prop = new Property { Name = "Nhà Quản Lý", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var service = new ServiceCatalogService(db);
        var created = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Internet", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 1, 1)),
            new PropertyCaller(OwnerId, Roles.ChuTro));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeleteServiceCatalogAsync(created.Id, new PropertyCaller(ManagerId, Roles.QuanLy)));

        Assert.False((await db.ServiceCatalogs.SingleAsync(catalog => catalog.Id == created.Id)).IsDeleted);
    }

    [SqlServerFact]
    public async Task CreateOrUpdateServicePriceAsync_WhenInactive_RejectsNewPriceVersion()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Inactive", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var created = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Internet", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 1, 1)),
            caller);

        // Turn inactive
        await service.ToggleServiceCatalogStatusAsync(created.Id, caller);

        // Attempt new price version
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrUpdateServicePriceAsync(
                created.Id,
                UpdateRequest("Internet", "phòng", "fixed-monthly", 120_000m, new DateOnly(2026, 6, 1)),
                caller));
        Assert.Contains("ngưng hoạt động", ex.Message);
    }

    [SqlServerFact]
    public async Task EffectiveTo_WhenNewVersionAdded_ClosesPreviousVersionAndSetsNewToNull()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Timeline", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        // Version 1 from 2026-01-01
        var created = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Internet", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 1, 1)),
            caller);

        var v1 = Assert.Single(created.PriceHistory);
        Assert.Null(v1.EffectiveTo);

        // Version 2 from 2026-07-01
        var updated = await service.CreateOrUpdateServicePriceAsync(
            created.Id,
            UpdateRequest("Internet", "phòng", "fixed-monthly", 150_000m, new DateOnly(2026, 7, 1)),
            caller);

        Assert.Equal(2, updated.PriceHistory.Count);
        var v1After = updated.PriceHistory.Single(v => v.EffectiveDate == new DateOnly(2026, 1, 1));
        var v2 = updated.PriceHistory.Single(v => v.EffectiveDate == new DateOnly(2026, 7, 1));

        Assert.Equal(new DateOnly(2026, 6, 30), v1After.EffectiveTo);
        Assert.Null(v2.EffectiveTo);

        // Reject adding version with date <= latest (2026-07-01)
        var exOverlap = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrUpdateServicePriceAsync(
                created.Id,
                UpdateRequest("Internet", "phòng", "fixed-monthly", 160_000m, new DateOnly(2026, 5, 1)),
                caller));
        Assert.Contains("phải sau ngày hiệu lực", exOverlap.Message);
    }

    [SqlServerFact]
    public async Task ToDto_CurrentVersionRespectsEffectiveDateAndEffectiveTo()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Version Lookup", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var cat = new ServiceCatalog
        {
            PropertyId = prop.Id,
            IsReadOnly = false,
            IsActive = true,
            PriceHistory =
            [
                new ServicePriceHistory
                {
                    Name = "Internet", Unit = "phòng", CalculationMethod = "fixed-monthly", UnitPrice = 100_000m,
                    EffectiveDate = new DateOnly(2026, 1, 1), EffectiveTo = new DateOnly(2026, 6, 30), CreatedByUserId = OwnerId
                },
                new ServicePriceHistory
                {
                    Name = "Internet", Unit = "phòng", CalculationMethod = "fixed-monthly", UnitPrice = 150_000m,
                    EffectiveDate = new DateOnly(2026, 7, 1), EffectiveTo = null, CreatedByUserId = OwnerId
                }
            ]
        };
        db.ServiceCatalogs.Add(cat);
        await db.SaveChangesAsync();

        // 1. Lookup at 2026-04-15 (should resolve Version 1)
        var clock1 = new FixedTimeProvider(new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc));
        var service1 = new ServiceCatalogService(db, clock1);
        var catalogs1 = await service1.GetServiceCatalogsAsync(new PropertyCaller(OwnerId, Roles.ChuTro));
        var active1 = Assert.Single(catalogs1, c => c.Id == cat.Id);
        Assert.Equal(100_000m, active1.UnitPrice);
        Assert.Equal(new DateOnly(2026, 6, 30), active1.EffectiveTo);

        // 2. Lookup at 2026-08-15 (should resolve Version 2)
        var clock2 = new FixedTimeProvider(new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc));
        var service2 = new ServiceCatalogService(db, clock2);
        var catalogs2 = await service2.GetServiceCatalogsAsync(new PropertyCaller(OwnerId, Roles.ChuTro));
        var active2 = Assert.Single(catalogs2, c => c.Id == cat.Id);
        Assert.Equal(150_000m, active2.UnitPrice);
        Assert.Null(active2.EffectiveTo);
    }

    [SqlServerFact]
    public async Task MigrationDown_Succeeds_WhenAllCreatedByUserIdsAreConvertibleToInt()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "101", Roles.ChuTro);

        // Run in an isolated transaction that rolls back at the end so it doesn't affect other tests
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO [ServiceCatalogs] ([PropertyId], [IsReadOnly], [CreatedAt], [IsDeleted]) VALUES (NULL, 0, GETUTCDATE(), 0);
            DECLARE @catId int = SCOPE_IDENTITY();
            INSERT INTO [ServicePriceHistory] ([ServiceCatalogId], [Name], [Unit], [CalculationMethod], [UnitPrice], [EffectiveDate], [CreatedAt], [CreatedByUserId])
            VALUES (@catId, 'Int User Item', 'phòng', 'fixed-monthly', 10000, '2026-01-01', GETUTCDATE(), '101');
        ");

        const string downSql = @"
            ALTER TABLE [ServicePriceHistory] DROP CONSTRAINT [FK_ServicePriceHistory_AspNetUsers_CreatedByUserId];
            DROP INDEX [IX_ServicePriceHistory_CreatedByUserId] ON [ServicePriceHistory];

            IF EXISTS (
                SELECT 1
                FROM [ServicePriceHistory]
                WHERE [CreatedByUserId] IS NULL
                   OR TRY_CAST([CreatedByUserId] AS INT) IS NULL
            )
            BEGIN
                THROW 50000, N'Cannot rollback migration ChangeCreatedByUserIdToString: ServicePriceHistory contains NULL or non-integer CreatedByUserId values. Data loss would occur.', 1;
            END;

            DECLARE @var nvarchar(max);
            SELECT @var = QUOTENAME([d].[name])
            FROM [sys].[default_constraints] [d]
            INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
            WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServicePriceHistory]') AND [c].[name] = N'CreatedByUserId');
            IF @var IS NOT NULL EXEC(N'ALTER TABLE [ServicePriceHistory] DROP CONSTRAINT ' + @var + ';');
            ALTER TABLE [ServicePriceHistory] ALTER COLUMN [CreatedByUserId] int NOT NULL;
        ";

        await db.Database.ExecuteSqlRawAsync(downSql);

        // Verify column type is now int and value is 101
        var result = await db.Database.SqlQueryRaw<int>(
            "SELECT CAST([CreatedByUserId] AS int) AS [Value] FROM [ServicePriceHistory] WHERE [Name] = 'Int User Item'")
            .SingleAsync();
        Assert.Equal(101, result);

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task MigrationDown_FailsAndAborts_WhenCreatedByUserIdContainsNull()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO [ServiceCatalogs] ([PropertyId], [IsReadOnly], [CreatedAt], [IsDeleted]) VALUES (NULL, 0, GETUTCDATE(), 0);
            DECLARE @catId int = SCOPE_IDENTITY();
            INSERT INTO [ServicePriceHistory] ([ServiceCatalogId], [Name], [Unit], [CalculationMethod], [UnitPrice], [EffectiveDate], [CreatedAt], [CreatedByUserId])
            VALUES (@catId, 'Null User Item', 'phòng', 'fixed-monthly', 10000, '2026-01-01', GETUTCDATE(), NULL);
        ");

        const string downSql = @"
            ALTER TABLE [ServicePriceHistory] DROP CONSTRAINT [FK_ServicePriceHistory_AspNetUsers_CreatedByUserId];
            DROP INDEX [IX_ServicePriceHistory_CreatedByUserId] ON [ServicePriceHistory];

            IF EXISTS (
                SELECT 1
                FROM [ServicePriceHistory]
                WHERE [CreatedByUserId] IS NULL
                   OR TRY_CAST([CreatedByUserId] AS INT) IS NULL
            )
            BEGIN
                THROW 50000, N'Cannot rollback migration ChangeCreatedByUserIdToString: ServicePriceHistory contains NULL or non-integer CreatedByUserId values. Data loss would occur.', 1;
            END;

            ALTER TABLE [ServicePriceHistory] ALTER COLUMN [CreatedByUserId] int NOT NULL;
        ";

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(downSql));
        Assert.Contains("Cannot rollback migration ChangeCreatedByUserIdToString", ex.Message);

        // Verify data was NOT changed to '0' and remains NULL
        var val = await db.Database.SqlQueryRaw<string?>(
            "SELECT [CreatedByUserId] AS [Value] FROM [ServicePriceHistory] WHERE [Name] = 'Null User Item'")
            .SingleAsync();
        Assert.Null(val);

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task MigrationDown_FailsAndAborts_WhenCreatedByUserIdContainsGuid()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        const string guidUser = "a1b2c3d4-e5f6-7890-abcd-ef1234567890";
        await SqlServerFixture.SeedAccountAsync(db, guidUser, Roles.ChuTro);
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync($@"
            INSERT INTO [ServiceCatalogs] ([PropertyId], [IsReadOnly], [CreatedAt], [IsDeleted]) VALUES (NULL, 0, GETUTCDATE(), 0);
            DECLARE @catId int = SCOPE_IDENTITY();
            INSERT INTO [ServicePriceHistory] ([ServiceCatalogId], [Name], [Unit], [CalculationMethod], [UnitPrice], [EffectiveDate], [CreatedAt], [CreatedByUserId])
            VALUES (@catId, 'Guid User Item', 'phòng', 'fixed-monthly', 10000, '2026-01-01', GETUTCDATE(), '{guidUser}');
        ");

        const string downSql = @"
            ALTER TABLE [ServicePriceHistory] DROP CONSTRAINT [FK_ServicePriceHistory_AspNetUsers_CreatedByUserId];
            DROP INDEX [IX_ServicePriceHistory_CreatedByUserId] ON [ServicePriceHistory];

            IF EXISTS (
                SELECT 1
                FROM [ServicePriceHistory]
                WHERE [CreatedByUserId] IS NULL
                   OR TRY_CAST([CreatedByUserId] AS INT) IS NULL
            )
            BEGIN
                THROW 50000, N'Cannot rollback migration ChangeCreatedByUserIdToString: ServicePriceHistory contains NULL or non-integer CreatedByUserId values. Data loss would occur.', 1;
            END;

            ALTER TABLE [ServicePriceHistory] ALTER COLUMN [CreatedByUserId] int NOT NULL;
        ";

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(downSql));
        Assert.Contains("Cannot rollback migration ChangeCreatedByUserIdToString", ex.Message);

        // Verify data was NOT changed to '0' and remains the original GUID
        var val = await db.Database.SqlQueryRaw<string?>(
            "SELECT [CreatedByUserId] AS [Value] FROM [ServicePriceHistory] WHERE [Name] = 'Guid User Item'")
            .SingleAsync();
        Assert.Equal(guidUser, val);

        await tx.RollbackAsync();
    }

    [SqlServerFact]
    public async Task EffectiveTo_ExplicitValue_IsPersistedAndNotExtendedByLaterVersion()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà EffectiveTo", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var request = CreateRequest(prop.Id, "Gửi xe", "xe", "fixed-monthly", 80_000m, new DateOnly(2026, 1, 1));
        request.EffectiveTo = new DateOnly(2026, 3, 31);
        var created = await service.CreateOrUpdateServicePriceAsync(request, caller);
        Assert.Equal(new DateOnly(2026, 3, 31), Assert.Single(created.PriceHistory).EffectiveTo);

        var update = UpdateRequest("Gửi xe", "xe", "fixed-monthly", 90_000m, new DateOnly(2026, 4, 1));
        update.EffectiveTo = new DateOnly(2026, 8, 31);
        await service.CreateOrUpdateServicePriceAsync(created.Id, update, caller);

        await using var verifyDb = fixture.CreateContext();
        var versions = await verifyDb.ServicePriceHistories
            .Where(v => v.ServiceCatalogId == created.Id)
            .OrderBy(v => v.EffectiveDate)
            .ToListAsync();

        Assert.Equal(2, versions.Count);
        // The earlier explicit end date (03-31) must stay 03-31 (the later version starts contiguously on 04-01).
        Assert.Equal(new DateOnly(2026, 3, 31), versions[0].EffectiveTo);
        Assert.Equal(new DateOnly(2026, 8, 31), versions[1].EffectiveTo);
    }

    [SqlServerFact]
    public async Task EffectiveTo_NewVersionStartingInsideEarlierRange_ClosesPreviousVersionDayBefore()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Overlap", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var request = CreateRequest(prop.Id, "Vệ sinh", "phòng", "fixed-monthly", 30_000m, new DateOnly(2026, 1, 1));
        request.EffectiveTo = new DateOnly(2026, 12, 31);
        var created = await service.CreateOrUpdateServicePriceAsync(request, caller);

        var tooSoon = UpdateRequest("Vệ sinh", "phòng", "fixed-monthly", 35_000m, new DateOnly(2026, 1, 2));
        var tooSoonError = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateOrUpdateServicePriceAsync(created.Id, tooSoon, caller));
        Assert.Equal("ErrorPriceVersionTooShort", tooSoonError.Data[ServiceCatalogErrorKeys.ResourceKey]);

        await service.CreateOrUpdateServicePriceAsync(
            created.Id,
            UpdateRequest("Vệ sinh", "phòng", "fixed-monthly", 40_000m, new DateOnly(2026, 4, 1)),
            caller);

        await using var verifyDb = fixture.CreateContext();
        var first = await verifyDb.ServicePriceHistories
            .SingleAsync(v => v.ServiceCatalogId == created.Id && v.EffectiveDate == new DateOnly(2026, 1, 1));
        var second = await verifyDb.ServicePriceHistories
            .SingleAsync(v => v.ServiceCatalogId == created.Id && v.EffectiveDate == new DateOnly(2026, 4, 1));

        Assert.Equal(new DateOnly(2026, 3, 31), first.EffectiveTo);
        Assert.Null(second.EffectiveTo);
    }

    [SqlServerFact]
    public async Task EffectiveTo_SameDayAsLatestVersion_IsRejectedWithResourceKey()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Same Day", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);
        var created = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Internet", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 5, 1)),
            caller);

        var same = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateOrUpdateServicePriceAsync(
                created.Id,
                UpdateRequest("Internet", "phòng", "fixed-monthly", 110_000m, new DateOnly(2026, 5, 1)),
                caller));
        Assert.Equal("ErrorEffectiveDateNotAfterLatest", same.Data[ServiceCatalogErrorKeys.ResourceKey]);

        var backward = UpdateRequest("Internet", "phòng", "fixed-monthly", 110_000m, new DateOnly(2026, 6, 1));
        backward.EffectiveTo = new DateOnly(2026, 5, 31);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateOrUpdateServicePriceAsync(created.Id, backward, caller));
    }

    [SqlServerFact]
    public async Task CreateServiceCatalog_WithNote_PersistsNoteOnInitialVersion()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Note Test", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var request = CreateRequest(prop.Id, "Internet FPT", "phòng", "fixed-monthly", 120_000m, new DateOnly(2026, 1, 1), note: "Gói cước ban đầu áp dụng từ 2026");
        var created = await service.CreateOrUpdateServicePriceAsync(request, caller);

        var versionDto = Assert.Single(created.PriceHistory);
        Assert.Equal("Gói cước ban đầu áp dụng từ 2026", versionDto.Note);

        await using var verifyDb = fixture.CreateContext();
        var entity = await verifyDb.ServicePriceHistories.SingleAsync(v => v.ServiceCatalogId == created.Id);
        Assert.Equal("Gói cước ban đầu áp dụng từ 2026", entity.Note);
    }

    [SqlServerFact]
    public async Task UpdateServicePrice_WithNote_PersistsNoteOnNewVersionAndPreservesOldVersionNote()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Note Update", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var created = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Bảo vệ", "phòng", "fixed-monthly", 50_000m, new DateOnly(2026, 1, 1), note: "Phiên bản V1"),
            caller);

        var updated = await service.CreateOrUpdateServicePriceAsync(
            created.Id,
            UpdateRequest("Bảo vệ", "phòng", "fixed-monthly", 60_000m, new DateOnly(2026, 7, 1), note: "Tăng phí bảo vệ đêm V2"),
            caller);

        Assert.Equal(2, updated.PriceHistory.Count);
        var v1Dto = updated.PriceHistory.Single(v => v.EffectiveDate == new DateOnly(2026, 1, 1));
        var v2Dto = updated.PriceHistory.Single(v => v.EffectiveDate == new DateOnly(2026, 7, 1));
        Assert.Equal("Phiên bản V1", v1Dto.Note);
        Assert.Equal("Tăng phí bảo vệ đêm V2", v2Dto.Note);

        await using var verifyDb = fixture.CreateContext();
        var v1Entity = await verifyDb.ServicePriceHistories.SingleAsync(v => v.ServiceCatalogId == created.Id && v.EffectiveDate == new DateOnly(2026, 1, 1));
        var v2Entity = await verifyDb.ServicePriceHistories.SingleAsync(v => v.ServiceCatalogId == created.Id && v.EffectiveDate == new DateOnly(2026, 7, 1));
        Assert.Equal("Phiên bản V1", v1Entity.Note);
        Assert.Equal("Tăng phí bảo vệ đêm V2", v2Entity.Note);
    }

    [SqlServerFact]
    public async Task CreateServiceCatalog_EmptyOrWhitespaceNote_NormalizesToNull()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Note Whitespace", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var created = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Rác sinh hoạt", "phòng", "fixed-monthly", 30_000m, new DateOnly(2026, 1, 1), note: "   "),
            caller);

        Assert.Null(Assert.Single(created.PriceHistory).Note);

        await using var verifyDb = fixture.CreateContext();
        var entity = await verifyDb.ServicePriceHistories.SingleAsync(v => v.ServiceCatalogId == created.Id);
        Assert.Null(entity.Note);
    }

    [SqlServerFact]
    public async Task CreateServiceCatalog_NoteExceeding500Characters_ThrowsArgumentException()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Note Exceed", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var req = CreateRequest(prop.Id, "Internet", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 1, 1), note: new string('N', 501));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOrUpdateServicePriceAsync(req, caller));
    }

    private static CreateServiceCatalogDto CreateRequest(
        int? propertyId,
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate,
        string? note = null) => new()
    {
        PropertyId = propertyId,
        Name = name,
        Unit = unit,
        CalculationMethod = calculationMethod,
        UnitPrice = unitPrice,
        EffectiveDate = effectiveDate,
        Note = note,
    };

    private static UpdateServicePriceDto UpdateRequest(
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate,
        string? note = null) => new()
    {
        Name = name,
        Unit = unit,
        CalculationMethod = calculationMethod,
        UnitPrice = unitPrice,
        EffectiveDate = effectiveDate,
        Note = note,
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
                CreatedByUserId = null,
            });
        }

        return catalog;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    [SqlServerFact]
    public async Task CreateServiceCatalog_WithDescription_PersistsAndReadsBackFromSql()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Description Test", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var request = CreateRequest(prop.Id, "Internet Cáp Quang VIP", "phòng", "fixed-monthly", 150_000m, new DateOnly(2026, 1, 1), note: "Note v1");
        request.Description = "Mô tả gói internet 100Mbps dùng chung cả phòng";

        var created = await service.CreateOrUpdateServicePriceAsync(request, caller);
        Assert.Equal("Mô tả gói internet 100Mbps dùng chung cả phòng", created.Description);

        await using var verifyDb = fixture.CreateContext();
        var entity = await verifyDb.ServiceCatalogs.SingleAsync(c => c.Id == created.Id);
        Assert.Equal("Mô tả gói internet 100Mbps dùng chung cả phòng", entity.Description);
    }

    [SqlServerFact]
    public async Task CreateServiceCatalog_WhitespaceDescription_NormalizesToNull()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà Whitespace Desc", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        var request = CreateRequest(prop.Id, "Dịch vụ Gym", "người", "fixed-monthly", 50_000m, new DateOnly(2026, 1, 1));
        request.Description = "   ";

        var created = await service.CreateOrUpdateServicePriceAsync(request, caller);
        Assert.Null(created.Description);

        await using var verifyDb = fixture.CreateContext();
        var entity = await verifyDb.ServiceCatalogs.SingleAsync(c => c.Id == created.Id);
        Assert.Null(entity.Description);
    }

    [SqlServerFact]
    public async Task CreateServiceCatalog_RejectsEffectiveToEqualOrBeforeEffectiveDate()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var prop = new Property { Name = "Nhà TF12 Test", OwnerId = OwnerId, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        // Same day: EffectiveTo == EffectiveDate -> REJECTED
        var reqSame = CreateRequest(prop.Id, "Internet Same Day", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 5, 1));
        reqSame.EffectiveTo = new DateOnly(2026, 5, 1);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOrUpdateServicePriceAsync(reqSame, caller));

        // Before day: EffectiveTo < EffectiveDate -> REJECTED
        var reqBefore = CreateRequest(prop.Id, "Internet Before Day", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 5, 1));
        reqBefore.EffectiveTo = new DateOnly(2026, 4, 30);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOrUpdateServicePriceAsync(reqBefore, caller));

        // After day: EffectiveTo > EffectiveDate -> ACCEPTED
        var reqAfter = CreateRequest(prop.Id, "Internet After Day", "phòng", "fixed-monthly", 100_000m, new DateOnly(2026, 5, 1));
        reqAfter.EffectiveTo = new DateOnly(2026, 5, 2);
        var created = await service.CreateOrUpdateServicePriceAsync(reqAfter, caller);
        Assert.Equal(new DateOnly(2026, 5, 2), created.EffectiveTo);
    }

    [SqlServerFact]
    public async Task GetServiceCatalogsAsync_TF13_OwnerSeesInactive_ManagerAndTenantOnlySeeActive()
    {
        await fixture.ResetAsync();
        var now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, TenantId, Roles.KhachThue);

        var prop = new Property { Name = "Nhà TF13 Test", OwnerId = OwnerId, IsActive = true, CreatedAt = now };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = prop.Id,
            ManagerUserId = ManagerId,
            AssignedByUserId = OwnerId,
            AssignedAt = now,
        });

        var room = new Room { PropertyId = prop.Id, Code = "R1", StandardPrice = 3000000m, CreatedAt = now };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var tenant = new Tenant { UserId = TenantId, FullName = "Tenant 1", CitizenId = "0123456789", Phone = "0900000000", CreatedAt = now, IsActive = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            RoomId = room.Id,
            ContractNumber = "HD-TF13",
            StartDate = now.AddMonths(-1),
            EndDate = now.AddMonths(5),
            SnapshotPrice = 3000000m,
            DepositAmount = 3000000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        db.ContractMembers.Add(new ContractMember { ContractId = contract.ContractId, TenantId = tenant.Id });
        await db.SaveChangesAsync();

        var callerOwner = new PropertyCaller(OwnerId, Roles.ChuTro);
        var service = new ServiceCatalogService(db);

        // Catalog 1: Active
        var activeCatalog = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Dịch vụ Đang Chạy", "phòng", "fixed-monthly", 50_000m, new DateOnly(2026, 1, 1)), callerOwner);

        // Catalog 2: Inactive
        var inactiveCatalog = await service.CreateOrUpdateServicePriceAsync(
            CreateRequest(prop.Id, "Dịch vụ Tạm Dừng", "phòng", "fixed-monthly", 60_000m, new DateOnly(2026, 1, 1)), callerOwner);
        await service.ToggleServiceCatalogStatusAsync(inactiveCatalog.Id, callerOwner);

        // 1. Owner sees both Active and Inactive
        var ownerCatalogs = await service.GetServiceCatalogsAsync(callerOwner);
        Assert.Contains(ownerCatalogs, c => c.Id == activeCatalog.Id && c.IsActive);
        Assert.Contains(ownerCatalogs, c => c.Id == inactiveCatalog.Id && !c.IsActive);

        // 2. Manager only sees Active
        var callerManager = new PropertyCaller(ManagerId, Roles.QuanLy);
        var managerCatalogs = await service.GetServiceCatalogsAsync(callerManager);
        Assert.Contains(managerCatalogs, c => c.Id == activeCatalog.Id);
        Assert.DoesNotContain(managerCatalogs, c => c.Id == inactiveCatalog.Id);

        // 3. Tenant only sees Active
        var callerTenant = new PropertyCaller(TenantId, Roles.KhachThue);
        var tenantCatalogs = await service.GetServiceCatalogsAsync(callerTenant);
        Assert.Contains(tenantCatalogs, c => c.Id == activeCatalog.Id);
        Assert.DoesNotContain(tenantCatalogs, c => c.Id == inactiveCatalog.Id);
    }

}
