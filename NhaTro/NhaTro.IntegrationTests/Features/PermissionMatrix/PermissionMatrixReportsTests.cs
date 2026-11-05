using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Xunit;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// P8 báo cáo — sheet Ma trận quyền, dòng 45 (OP44 Xem báo cáo: D45 ❌ / E45 ✅ / F45 ⚙️ / G45 ❌).
/// OP45 (Xuất Excel, dòng 46) chưa có API trong code nên được khai báo BLOCKED tĩnh trong
/// <see cref="PermissionMatrixProbeExecutor"/>; không có test giả cho ô đó.
/// </summary>
public sealed class PermissionMatrixReportsTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static PropertyCaller OwnerA => new("owner-a", Roles.ChuTro);
    private static PropertyCaller TenantA => new("khach-1", Roles.KhachThue);
    private static PropertyCaller Admin => new("admin-1", Roles.Admin);

    private RoomReportService NewReports() =>
        new(fixture, PermissionMatrixWorld.NewRealTogglePolicy(fixture));

    private async Task SeedAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "admin-1", Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "manager-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);

        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = new RoomService(
            fixture,
            TimeProvider.System,
            PermissionMatrixWorld.NewRealTogglePolicy(fixture),
            new ActiveContractRoomUsageChecker(fixture, TimeProvider.System));

        var property = await properties.CreateAsync(new PropertySave { Name = "Nhà A" }, OwnerA);
        Assert.True(property.Succeeded, property.ErrorMessage);
        var room = await rooms.CreateAsync(
            new RoomSave { PropertyId = property.Property!.Id, Code = "A101", StandardPrice = 1_500_000m }, OwnerA);
        Assert.True(room.Succeeded, room.ErrorMessage);
    }

    /// <summary>Ô D45 ❌: Admin là vai trò hệ thống (D1), không xem báo cáo nghiệp vụ P8.</summary>
    [SqlServerFact]
    public async Task Cell_D45_OP44_Admin_IsDenied()
    {
        await SeedAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewReports().GetAsync(new RoomReportFilter(), Admin));
    }

    /// <summary>Ô G45 ❌: Khách thuê không truy cập được báo cáo quản trị.</summary>
    [SqlServerFact]
    public async Task Cell_G45_OP44_KhachThue_IsDenied()
    {
        await SeedAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewReports().GetAsync(new RoomReportFilter(), TenantA));
    }

    /// <summary>Ô E45 ✅: Chủ trọ đọc báo cáo phòng và thấy phòng của mình (thao tác Chủ trọ luôn hiệu lực ngay, D4).</summary>
    [SqlServerFact]
    public async Task Cell_E45_OP44_ChuTro_IsAllowed()
    {
        await SeedAsync();
        var report = await NewReports().GetAsync(new RoomReportFilter(), OwnerA);

        Assert.Equal(1, report.Kpis.Total);
        Assert.Equal(1, report.Rooms.TotalCount);
    }

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô F45 ⚙️: OP44 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F45_OP44_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F45");

    /// <summary>Ô D46 ❌: OP45 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D46_OP45_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D46");

    /// <summary>Ô E46 ✅: OP45 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E46_OP45_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E46");

    /// <summary>Ô F46 ⚙️: OP45 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F46_OP45_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F46");

    /// <summary>Ô G46 ❌: OP45 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G46_OP45_KhachThue_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G46");
}
