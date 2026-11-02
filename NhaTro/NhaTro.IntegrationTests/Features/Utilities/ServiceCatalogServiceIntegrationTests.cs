using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms;
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
    public async Task MigrationDown_Succeeds_WhenAllCreatedByUserIdsAreConvertibleToInt()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "101", Roles.ChuTro);

        // Run in an isolated transaction that rolls back at the end so it doesn't affect other tests
        await using var tx = await db.Database.BeginTransactionAsync();

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO [ServiceCatalogs] ([PropertyId], [IsReadOnly], [CreatedAt]) VALUES (NULL, 0, GETUTCDATE());
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
            INSERT INTO [ServiceCatalogs] ([PropertyId], [IsReadOnly], [CreatedAt]) VALUES (NULL, 0, GETUTCDATE());
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
            INSERT INTO [ServiceCatalogs] ([PropertyId], [IsReadOnly], [CreatedAt]) VALUES (NULL, 0, GETUTCDATE());
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

    private static CreateServiceCatalogDto CreateRequest(
        int? propertyId,
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        DateOnly effectiveDate) => new()
    {
        PropertyId = propertyId,
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
                CreatedByUserId = null,
            });
        }

        return catalog;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
