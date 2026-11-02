using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Utilities;

public sealed class ServiceCatalogPageServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string Owner1Id = "owner-user-1";
    private const string Owner2Id = "owner-user-2";
    private const string Manager1Id = "manager-user-1";
    private const string Tenant1UserId = "tenant-user-1";

    private static readonly DateTime FixedTestNow = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly FixedTimeProvider TestClock = new(FixedTestNow);

    [SqlServerFact]
    public async Task PropertyScope_ChuTro_OnlySeesOwnedProperties()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Owner2Id, Roles.ChuTro);

        var p1 = new Property { Name = "Nhà trọ 1", OwnerId = Owner1Id, Address = "A1", CreatedAt = FixedTestNow };
        var p2 = new Property { Name = "Nhà trọ 2", OwnerId = Owner2Id, Address = "A2", CreatedAt = FixedTestNow };
        var pDeleted = new Property { Name = "Nhà trọ cũ", OwnerId = Owner1Id, Address = "A3", IsDeleted = true, CreatedAt = FixedTestNow };
        db.Properties.AddRange(p1, p2, pDeleted);
        await db.SaveChangesAsync();

        var accessible = await ServiceCatalogPageService.ResolveAccessiblePropertyIdsAsync(
            db, Owner1Id, Roles.ChuTro, TestClock);

        Assert.Contains(p1.Id, accessible);
        Assert.DoesNotContain(p2.Id, accessible); // Thuộc owner 2
        Assert.DoesNotContain(pDeleted.Id, accessible); // Đã xóa
    }

    [SqlServerFact]
    public async Task PropertyScope_QuanLy_OnlySeesAssignedActiveProperties()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Manager1Id, Roles.QuanLy);

        var p1 = new Property { Name = "Nhà được phân công", OwnerId = Owner1Id, Address = "A1", CreatedAt = FixedTestNow };
        var p2 = new Property { Name = "Nhà đã thu hồi phân công", OwnerId = Owner1Id, Address = "A2", CreatedAt = FixedTestNow };
        var p3 = new Property { Name = "Nhà chưa từng phân công", OwnerId = Owner1Id, Address = "A3", CreatedAt = FixedTestNow };
        db.Properties.AddRange(p1, p2, p3);
        await db.SaveChangesAsync();

        var pm1 = new PropertyManager { PropertyId = p1.Id, ManagerUserId = Manager1Id, AssignedByUserId = Owner1Id, AssignedAt = FixedTestNow };
        var pm2 = new PropertyManager { PropertyId = p2.Id, ManagerUserId = Manager1Id, AssignedByUserId = Owner1Id, AssignedAt = FixedTestNow, RevokedAt = FixedTestNow.AddDays(1) };
        db.PropertyManagers.AddRange(pm1, pm2);
        await db.SaveChangesAsync();

        var accessible = await ServiceCatalogPageService.ResolveAccessiblePropertyIdsAsync(
            db, Manager1Id, Roles.QuanLy, TestClock);

        Assert.Contains(p1.Id, accessible);
        Assert.DoesNotContain(p2.Id, accessible); // Đã revoke
        Assert.DoesNotContain(p3.Id, accessible); // Chưa gán
    }

    [SqlServerFact]
    public async Task PropertyScope_KhachThue_OnlySeesPropertyWithApprovedContract()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Tenant1UserId, Roles.KhachThue);

        var pValid = new Property { Name = "Nhà thuê hợp lệ", OwnerId = Owner1Id, Address = "A1", CreatedAt = FixedTestNow };
        var pOther = new Property { Name = "Nhà không thuê", OwnerId = Owner1Id, Address = "A2", CreatedAt = FixedTestNow };
        db.Properties.AddRange(pValid, pOther);
        await db.SaveChangesAsync();

        var room = new Room { PropertyId = pValid.Id, Code = "R101", StandardPrice = 3000000m, CreatedAt = FixedTestNow };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var tenant = new Tenant { UserId = Tenant1UserId, FullName = "Nguyễn Văn A", CitizenId = "012345678901", Phone = "0900000001", CreatedAt = FixedTestNow, IsActive = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            RoomId = room.Id,
            ContractNumber = "HD-001",
            StartDate = FixedTestNow.AddMonths(-1),
            EndDate = FixedTestNow.AddMonths(5),
            SnapshotPrice = 3000000m,
            DepositAmount = 3000000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        var member = new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = tenant.Id,
        };
        db.ContractMembers.Add(member);
        await db.SaveChangesAsync();

        var accessible = await ServiceCatalogPageService.ResolveAccessiblePropertyIdsAsync(
            db, Tenant1UserId, Roles.KhachThue, TestClock);

        Assert.Contains(pValid.Id, accessible);
        Assert.DoesNotContain(pOther.Id, accessible);
    }

    [SqlServerFact]
    public async Task PropertyScope_KhachThue_ExpiredOrFutureContract_DoesNotGrantAccess()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Tenant1UserId, Roles.KhachThue);

        var pExpired = new Property { Name = "Nhà hết hạn", OwnerId = Owner1Id, Address = "A1", CreatedAt = FixedTestNow };
        var pFuture = new Property { Name = "Nhà chưa bắt đầu", OwnerId = Owner1Id, Address = "A2", CreatedAt = FixedTestNow };
        db.Properties.AddRange(pExpired, pFuture);
        await db.SaveChangesAsync();

        var r1 = new Room { PropertyId = pExpired.Id, Code = "R1", StandardPrice = 3000000m, CreatedAt = FixedTestNow };
        var r2 = new Room { PropertyId = pFuture.Id, Code = "R2", StandardPrice = 3000000m, CreatedAt = FixedTestNow };
        db.Rooms.AddRange(r1, r2);
        await db.SaveChangesAsync();

        var tenant = new Tenant { UserId = Tenant1UserId, FullName = "Khách Hết Hạn", CitizenId = "012345678902", Phone = "0900000002", CreatedAt = FixedTestNow, IsActive = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        // Expired contract: ended last month
        var cExpired = new Contract
        {
            RoomId = r1.Id,
            ContractNumber = "HD-EXPIRED",
            StartDate = FixedTestNow.AddMonths(-6),
            EndDate = FixedTestNow.AddMonths(-1),
            SnapshotPrice = 3000000m,
            DepositAmount = 3000000m,
            Status = ContractStatus.Approved,
        };

        // Future contract: starts next month
        var cFuture = new Contract
        {
            RoomId = r2.Id,
            ContractNumber = "HD-FUTURE",
            StartDate = FixedTestNow.AddMonths(1),
            EndDate = FixedTestNow.AddMonths(6),
            SnapshotPrice = 3000000m,
            DepositAmount = 3000000m,
            Status = ContractStatus.Approved,
        };

        db.Contracts.AddRange(cExpired, cFuture);
        await db.SaveChangesAsync();

        db.ContractMembers.AddRange(
            new ContractMember { ContractId = cExpired.ContractId, TenantId = tenant.Id },
            new ContractMember { ContractId = cFuture.ContractId, TenantId = tenant.Id }
        );
        await db.SaveChangesAsync();

        var accessible = await ServiceCatalogPageService.ResolveAccessiblePropertyIdsAsync(
            db, Tenant1UserId, Roles.KhachThue, TestClock);

        Assert.DoesNotContain(pExpired.Id, accessible);
        Assert.DoesNotContain(pFuture.Id, accessible);
        Assert.Empty(accessible);
    }

    [SqlServerFact]
    public async Task PropertyScope_KhachThue_PendingOrRejectedContract_DoesNotGrantAccess()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Tenant1UserId, Roles.KhachThue);

        var pPending = new Property { Name = "Nhà hợp đồng chờ duyệt", OwnerId = Owner1Id, Address = "A1", CreatedAt = FixedTestNow };
        var pRejected = new Property { Name = "Nhà hợp đồng bị từ chối", OwnerId = Owner1Id, Address = "A2", CreatedAt = FixedTestNow };
        db.Properties.AddRange(pPending, pRejected);
        await db.SaveChangesAsync();

        var r1 = new Room { PropertyId = pPending.Id, Code = "R1", StandardPrice = 3000000m, CreatedAt = FixedTestNow };
        var r2 = new Room { PropertyId = pRejected.Id, Code = "R2", StandardPrice = 3000000m, CreatedAt = FixedTestNow };
        db.Rooms.AddRange(r1, r2);
        await db.SaveChangesAsync();

        var tenant = new Tenant { UserId = Tenant1UserId, FullName = "Khách Chờ Duyệt", CitizenId = "012345678903", Phone = "0900000003", CreatedAt = FixedTestNow, IsActive = true };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var cPending = new Contract
        {
            RoomId = r1.Id,
            ContractNumber = "HD-PENDING",
            StartDate = FixedTestNow.AddMonths(-1),
            EndDate = FixedTestNow.AddMonths(5),
            SnapshotPrice = 3000000m,
            DepositAmount = 3000000m,
            Status = ContractStatus.Pending, // Chưa approved
        };

        var cRejected = new Contract
        {
            RoomId = r2.Id,
            ContractNumber = "HD-REJECTED",
            StartDate = FixedTestNow.AddMonths(-1),
            EndDate = FixedTestNow.AddMonths(5),
            SnapshotPrice = 3000000m,
            DepositAmount = 3000000m,
            Status = ContractStatus.Rejected,
        };

        db.Contracts.AddRange(cPending, cRejected);
        await db.SaveChangesAsync();

        db.ContractMembers.AddRange(
            new ContractMember { ContractId = cPending.ContractId, TenantId = tenant.Id },
            new ContractMember { ContractId = cRejected.ContractId, TenantId = tenant.Id }
        );
        await db.SaveChangesAsync();

        var accessible = await ServiceCatalogPageService.ResolveAccessiblePropertyIdsAsync(
            db, Tenant1UserId, Roles.KhachThue, TestClock);

        Assert.Empty(accessible);
    }

    [SqlServerFact]
    public async Task GetServicesForCallerAsync_SystemCatalogsVisibleToAll_PropertyCatalogsStrictlyScoped()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Owner2Id, Roles.ChuTro);

        var p1 = new Property { Name = "Nhà trọ của Owner 1", OwnerId = Owner1Id, Address = "A1", CreatedAt = FixedTestNow };
        var p2 = new Property { Name = "Nhà trọ của Owner 2", OwnerId = Owner2Id, Address = "A2", CreatedAt = FixedTestNow };
        db.Properties.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var systemCat = new ServiceCatalog
        {
            PropertyId = null,
            IsReadOnly = true,
            CreatedAt = FixedTestNow,
            PriceHistory = [new ServicePriceHistory
            {
                Name = "Điện toàn hệ thống",
                Unit = "kWh",
                CalculationMethod = "meter",
                UnitPrice = 3500m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedByUserId = Owner1Id,
                CreatedAt = FixedTestNow,
            }]
        };
        var p1Cat = new ServiceCatalog
        {
            PropertyId = p1.Id,
            IsReadOnly = false,
            CreatedAt = FixedTestNow,
            PriceHistory = [new ServicePriceHistory
            {
                Name = "Rác nhà 1",
                Unit = "phòng",
                CalculationMethod = "fixed-monthly",
                UnitPrice = 50000m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedByUserId = Owner1Id,
                CreatedAt = FixedTestNow,
            }]
        };
        var p2Cat = new ServiceCatalog
        {
            PropertyId = p2.Id,
            IsReadOnly = false,
            CreatedAt = FixedTestNow,
            PriceHistory = [new ServicePriceHistory
            {
                Name = "Rác nhà 2",
                Unit = "phòng",
                CalculationMethod = "fixed-monthly",
                UnitPrice = 60000m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedByUserId = Owner2Id,
                CreatedAt = FixedTestNow,
            }]
        };

        db.ServiceCatalogs.AddRange(systemCat, p1Cat, p2Cat);
        await db.SaveChangesAsync();

        var pageService = CreatePageService(Owner1Id, Roles.ChuTro);
        var services = await pageService.GetServicesForCallerAsync();

        Assert.Contains(services, s => s.Name == "Điện toàn hệ thống" && s.BuildingId == "all");
        Assert.Contains(services, s => s.Name == "Rác nhà 1" && s.BuildingId == p1.Id.ToString());
        Assert.DoesNotContain(services, s => s.Name == "Rác nhà 2");
    }

    [SqlServerFact]
    public async Task AddServiceAsync_Owner_SuccessfullyPersistsAndVerifiesFromSql()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);

        var prop = new Property { Name = "Nhà kiểm tra ghi", OwnerId = Owner1Id, Address = "Ghi 1", CreatedAt = FixedTestNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var pageService = CreatePageService(Owner1Id, Roles.ChuTro);
        var request = new CreateServiceRequest
        {
            Name = "Dịch vụ WiFi tốc độ cao",
            Type = ServiceTypeCategory.FixedMonthly,
            Unit = ServiceUnitCategory.Room,
            Rate = 120_000m,
            EffectiveFrom = new DateOnly(2026, 11, 1),
            BuildingId = prop.Id.ToString(),
        };

        var (success, error, item) = await pageService.AddServiceAsync(request);

        Assert.True(success, error);
        Assert.Null(error);
        Assert.NotNull(item);
        Assert.Equal("Dịch vụ WiFi tốc độ cao", item.Name);
        Assert.Equal(120_000m, item.CurrentRate);

        // Verify in database directly
        await using var verifyDb = fixture.CreateContext();
        var catalog = await verifyDb.ServiceCatalogs
            .Include(c => c.PriceHistory)
            .SingleOrDefaultAsync(c => c.Id == int.Parse(item.Id));
        Assert.NotNull(catalog);
        Assert.Equal(prop.Id, catalog.PropertyId);
        var version = Assert.Single(catalog.PriceHistory);
        Assert.Equal(Owner1Id, version.CreatedByUserId);
        Assert.Equal(120_000m, version.UnitPrice);
    }

    [SqlServerFact]
    public async Task AddServiceAsync_CrossPropertyOrNonOwner_Rejected()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Owner2Id, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Manager1Id, Roles.QuanLy);

        var p2 = new Property { Name = "Nhà của Owner 2", OwnerId = Owner2Id, Address = "A2", CreatedAt = FixedTestNow };
        db.Properties.Add(p2);
        await db.SaveChangesAsync();

        // 1. Owner 1 tries to add service to Owner 2's property -> rejected
        var owner1Service = CreatePageService(Owner1Id, Roles.ChuTro);
        var (s1, err1, _) = await owner1Service.AddServiceAsync(new CreateServiceRequest
        {
            Name = "Hack Service",
            Rate = 50000m,
            BuildingId = p2.Id.ToString(),
        });
        Assert.False(s1);
        Assert.NotNull(err1);

        // 2. Manager tries to add service -> rejected by role
        var managerService = CreatePageService(Manager1Id, Roles.QuanLy);
        var (s2, err2, _) = await managerService.AddServiceAsync(new CreateServiceRequest
        {
            Name = "Manager Service",
            Rate = 50000m,
            BuildingId = p2.Id.ToString(),
        });
        Assert.False(s2);
        Assert.NotNull(err2);
        Assert.Contains("Chủ trọ", err2);
    }

    [SqlServerFact]
    public async Task UpdateServicePriceAsync_Owner_SuccessfullyPersistsAndVerifiesFromSql()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner1Id, Roles.ChuTro);

        var prop = new Property { Name = "Nhà sửa giá", OwnerId = Owner1Id, Address = "S1", CreatedAt = FixedTestNow };
        db.Properties.Add(prop);
        await db.SaveChangesAsync();

        var cat = new ServiceCatalog
        {
            PropertyId = prop.Id,
            IsReadOnly = false,
            CreatedAt = FixedTestNow,
            PriceHistory = [new ServicePriceHistory
            {
                Name = "Gửi xe máy",
                Unit = "xe",
                CalculationMethod = "fixed-monthly",
                UnitPrice = 100_000m,
                EffectiveDate = new DateOnly(2026, 1, 1),
                CreatedByUserId = Owner1Id,
                CreatedAt = FixedTestNow,
            }]
        };
        db.ServiceCatalogs.Add(cat);
        await db.SaveChangesAsync();

        var pageService = CreatePageService(Owner1Id, Roles.ChuTro);
        var (success, error, version) = await pageService.UpdateServicePriceAsync(new UpdatePriceRequest
        {
            ServiceId = cat.Id.ToString(),
            NewRate = 120_000m,
            Unit = ServiceUnitCategory.Vehicle,
            EffectiveFrom = new DateOnly(2026, 12, 1),
            BuildingScope = prop.Id.ToString(),
        });

        Assert.True(success, error);
        Assert.Null(error);
        Assert.NotNull(version);
        Assert.Equal(120_000m, version.Rate);

        // Verify in database
        await using var verifyDb = fixture.CreateContext();
        var reloaded = await verifyDb.ServiceCatalogs
            .Include(c => c.PriceHistory)
            .SingleAsync(c => c.Id == cat.Id);
        Assert.Equal(2, reloaded.PriceHistory.Count);
        var newV = Assert.Single(reloaded.PriceHistory, v => v.EffectiveDate == new DateOnly(2026, 12, 1));
        Assert.Equal(Owner1Id, newV.CreatedByUserId);
        Assert.Equal(120_000m, newV.UnitPrice);
    }

    private ServiceCatalogPageService CreatePageService(string userId, string role)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim(ClaimTypes.Name, userId));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        var user = new ClaimsPrincipal(identity);

        var services = new ServiceCollection();
        services.AddScoped<ApplicationDbContext>(_ => fixture.CreateContext());
        services.AddScoped<IServiceCatalogService, ServiceCatalogService>();
        services.AddSingleton<TimeProvider>(TestClock);
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return new ServiceCatalogPageService(
            new SimpleTestAuthStateProvider(user),
            scopeFactory,
            TestClock,
            NullLogger<ServiceCatalogPageService>.Instance);
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class SimpleTestAuthStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }
}
