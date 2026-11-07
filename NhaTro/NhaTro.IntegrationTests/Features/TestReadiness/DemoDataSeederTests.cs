using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Data.Seeding;
using Web.Features.Approvals.Enums;
using Web.Features.Contracts.Enums;
using Web.Features.Rooms.Enums;
using Web.Features.Settings.Enums;

namespace NhaTro.IntegrationTests.Features.TestReadiness;

/// <summary>
/// VHT-49 3A — dữ liệu demo tạo lại được từ DB sạch, idempotent, đủ 4 vai trò/2 nhà/2 kỳ
/// và số tiền khớp bảng đáp án. Không kiểm UI; chỉ kiểm dữ liệu và phạm vi.
/// </summary>
public sealed class DemoDataSeederTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static Task<DemoDataSeeder.DemoSeedSummary> SeedAsync(ApplicationDbContext db) =>
        DemoDataSeeder.EnsureAsync(db, DemoDataSeeder.DefaultPassword);

    [SqlServerFact]
    public async Task Seed_hai_lan_khong_nhan_doi_du_lieu()
    {
        await fixture.ResetAsync();
        await using (var first = fixture.CreateContext())
        {
            await SeedAsync(first);
        }

        await using var second = fixture.CreateContext();
        await SeedAsync(second);

        Assert.Equal(6, await second.Users.CountAsync());
        Assert.Equal(4, await second.Roles.CountAsync());
        Assert.Equal(2, await second.Properties.CountAsync());
        Assert.Equal(5, await second.Rooms.IgnoreQueryFilters().CountAsync());
        Assert.Equal(2, await second.Contracts.CountAsync());
        Assert.Equal(4, await second.UtilityReadings.CountAsync());
        Assert.Equal(1, await second.UtilityReadingAudits.CountAsync());
        Assert.Equal(7, await second.ServiceCatalogs.IgnoreQueryFilters().CountAsync());
        Assert.Equal(21, await second.ManagerToggles.CountAsync());
        Assert.Equal(1, await second.ManagerApprovalRequests.CountAsync());
    }

    [SqlServerFact]
    public async Task Bon_vai_tro_va_pham_vi_hai_nha_dung_nhu_bang_giao()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SeedAsync(db);

        var propertyA = await db.Properties.SingleAsync(item => item.Name == DemoDataSeeder.PropertyAName);
        var propertyB = await db.Properties.SingleAsync(item => item.Name == DemoDataSeeder.PropertyBName);
        var ownerA = await db.Users.SingleAsync(item => item.Email == DemoDataSeeder.OwnerAEmail);
        var ownerB = await db.Users.SingleAsync(item => item.Email == DemoDataSeeder.OwnerBEmail);
        var managerA = await db.Users.SingleAsync(item => item.Email == DemoDataSeeder.ManagerAEmail);
        var tenantUserA = await db.Users.SingleAsync(item => item.Email == DemoDataSeeder.TenantAEmail);
        var tenantUserB = await db.Users.SingleAsync(item => item.Email == DemoDataSeeder.TenantBEmail);

        Assert.Equal(ownerA.Id, propertyA.OwnerId);
        Assert.Equal(ownerB.Id, propertyB.OwnerId);
        Assert.True(await db.PropertyManagers.AnyAsync(item =>
            item.PropertyId == propertyA.Id && item.ManagerUserId == managerA.Id && item.RevokedAt == null));
        Assert.False(await db.PropertyManagers.AnyAsync(item =>
            item.PropertyId == propertyB.Id && item.ManagerUserId == managerA.Id && item.RevokedAt == null));

        var tenantA = await db.Tenants.SingleAsync(item => item.UserId == tenantUserA.Id);
        var tenantB = await db.Tenants.SingleAsync(item => item.UserId == tenantUserB.Id);
        var contractA = await db.Contracts.SingleAsync(item => item.ContractNumber == DemoDataSeeder.ContractANumber);
        var contractB = await db.Contracts.SingleAsync(item => item.ContractNumber == DemoDataSeeder.ContractBNumber);

        Assert.Equal(ContractStatus.Approved, contractA.Status);
        Assert.Equal(ContractStatus.Approved, contractB.Status);
        Assert.True(await db.ContractMembers.AnyAsync(item =>
            item.ContractId == contractA.ContractId && item.TenantId == tenantA.Id));
        Assert.True(await db.ContractMembers.AnyAsync(item =>
            item.ContractId == contractB.ContractId && item.TenantId == tenantB.Id));

        var roomA101 = await db.Rooms.SingleAsync(item =>
            item.PropertyId == propertyA.Id && item.Code == DemoDataSeeder.RoomA101Code);
        var roomB201 = await db.Rooms.SingleAsync(item =>
            item.PropertyId == propertyB.Id && item.Code == DemoDataSeeder.RoomB201Code);
        Assert.Equal(RoomStatus.Rented, roomA101.Status);
        Assert.Equal(RoomStatus.Rented, roomB201.Status);
        Assert.Equal(contractA.RoomId, roomA101.Id);
        Assert.Equal(contractB.RoomId, roomB201.Id);

        Assert.All(await db.Users.ToListAsync(), user => Assert.EndsWith("@demo.local", user.Email));
        Assert.All(await db.Tenants.IgnoreQueryFilters().ToListAsync(), tenant =>
        {
            Assert.StartsWith("000000", tenant.Phone);
            Assert.StartsWith("000000", tenant.CitizenId);
        });
    }

    [SqlServerFact]
    public async Task So_tien_hai_ky_khop_bang_dap_an()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var summary = await SeedAsync(db);

        Assert.Equal(3_433_300m, summary.PeriodTotals["A101-2026-09"]);
        Assert.Equal(3_458_100m, summary.PeriodTotals["A101-2026-10"]);
        Assert.Equal(4_377_500m, summary.PeriodTotals["B201-2026-09"]);
        Assert.Equal(4_390_400m, summary.PeriodTotals["B201-2026-10"]);
        Assert.Equal(4, summary.PeriodTotals.Count);
    }

    [SqlServerFact]
    public async Task Du_lieu_bo_sung_D1_D17_co_du_thanh_phan()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var summary = await SeedAsync(db);

        var modes = await db.ManagerToggles.Select(item => item.Mode).Distinct().ToListAsync();
        Assert.Equal(3, modes.Count);
        Assert.Contains(ManagerToggleMode.Allow, modes);
        Assert.Contains(ManagerToggleMode.Approve, modes);
        Assert.Contains(ManagerToggleMode.Deny, modes);

        var pending = await db.ManagerApprovalRequests.SingleAsync();
        Assert.Equal(ApprovalRequestStatus.Pending, pending.Status);
        Assert.Equal("SW-02", pending.OperationCode);
        Assert.Equal(ApprovalTargetType.Room, pending.TargetType);

        Assert.Equal(3, summary.TrashItems);
        Assert.Equal(1, await db.Rooms.IgnoreQueryFilters().CountAsync(item => item.IsDeleted));
        Assert.Equal(1, await db.Tenants.IgnoreQueryFilters().CountAsync(item => item.IsDeleted));
        Assert.Equal(1, await db.ServiceCatalogs.IgnoreQueryFilters().CountAsync(item => item.IsDeleted));
        Assert.Equal(1, await db.UtilityReadingAudits.CountAsync());
        Assert.Equal(7, await db.ServicePriceHistories.CountAsync());
        Assert.True(await db.UtilityTariffs.CountAsync() >= 5);
        Assert.Equal(0, await db.Invoices.CountAsync());
        Assert.Equal(0, await db.Payments.CountAsync());
    }
}
