using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Settings;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Xunit;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// OP08 "Cấu hình công tắc Quản lý" — sheet Ma trận quyền, dòng 9 (D9 ❌ / E9 ✅ / F9 ❌ / G9 ❌).
/// Mỗi test một ô; tên test mang địa chỉ ô Excel để truy vết ngược về workbook.
/// Cả bốn ô đều kiểm chứng chính thao tác ghi (SetAsync) vì ô mô tả quyền "cấu hình", không phải "xem danh mục";
/// case ✅ đọc lại DB để chắc chắn chế độ đổi thật rồi khôi phục giá trị cũ trong finally.
/// </summary>
public sealed class PermissionMatrixSettingsTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string SwitchCode = "SW-01";

    private async Task<(ApplicationDbContext Db, ManagerToggleService Service)> SeedAsync()
    {
        await fixture.ResetAsync();
        var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "admin-1", Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "manager-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        return (db, new ManagerToggleService(db, TimeProvider.System, NullLogger<ManagerToggleService>.Instance));
    }

    private static ManagerToggleCaller Caller(string userId, string role) => new(userId, role);

    /// <summary>Ô E9 ✅: Chủ trọ đổi được chế độ công tắc và hiệu lực thật trong DB.</summary>
    [SqlServerFact]
    public async Task Cell_E9_OP08_ChuTro_IsAllowed()
    {
        var (db, service) = await SeedAsync();
        await using var _ = db;

        var before = await service.GetModeAsync(SwitchCode);
        var target = before == ManagerToggleMode.Deny ? ManagerToggleMode.Allow : ManagerToggleMode.Deny;

        try
        {
            var written = await service.SetAsync(SwitchCode, target, Caller("owner-a", Roles.ChuTro));
            var after = await service.GetModeAsync(SwitchCode);

            Assert.True(written.Succeeded, written.ErrorCode);
            Assert.Equal(target, after);
        }
        finally
        {
            // Khôi phục kể cả khi assert fail: DB dùng chung cho cả class nên chế độ rò
            // sẽ làm các ô ❌ cùng thao tác đọc phải trạng thái không còn là mặc định.
            await service.SetAsync(SwitchCode, before, Caller("owner-a", Roles.ChuTro));
        }
    }

    /// <summary>Ô D9 ❌: Admin là vai trò hệ thống (D1), không đụng tới công tắc nghiệp vụ.</summary>
    [SqlServerFact]
    public async Task Cell_D9_OP08_Admin_IsDenied()
    {
        var (db, service) = await SeedAsync();
        await using var _ = db;

        var before = await service.GetModeAsync(SwitchCode);
        var result = await service.SetAsync(SwitchCode, ManagerToggleMode.Deny, Caller("admin-1", Roles.Admin));

        Assert.False(result.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await service.GetModeAsync(SwitchCode));
    }

    /// <summary>Ô F9 ❌: Quản lý không được tự đổi công tắc điều khiển chính mình.</summary>
    [SqlServerFact]
    public async Task Cell_F9_OP08_QuanLy_IsDenied()
    {
        var (db, service) = await SeedAsync();
        await using var _ = db;

        var before = await service.GetModeAsync(SwitchCode);
        var result = await service.SetAsync(SwitchCode, ManagerToggleMode.Allow, Caller("manager-1", Roles.QuanLy));

        Assert.False(result.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await service.GetModeAsync(SwitchCode));
    }

    /// <summary>Ô G9 ❌: Khách thuê không có quyền vào màn hình công tắc.</summary>
    [SqlServerFact]
    public async Task Cell_G9_OP08_KhachThue_IsDenied()
    {
        var (db, service) = await SeedAsync();
        await using var _ = db;

        var before = await service.GetModeAsync(SwitchCode);
        var result = await service.SetAsync(SwitchCode, ManagerToggleMode.Deny, Caller("khach-1", Roles.KhachThue));

        Assert.False(result.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await service.GetModeAsync(SwitchCode));
    }
}
