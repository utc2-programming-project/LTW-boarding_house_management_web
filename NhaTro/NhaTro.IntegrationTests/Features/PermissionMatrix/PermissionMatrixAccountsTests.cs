using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.DTOs;
using Xunit;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// P1 tài khoản &amp; xác thực — sheet Ma trận quyền, các ô ❌ ở dòng 4/6/7/8.
/// Mỗi test một ô và tên test mang địa chỉ ô Excel để truy vết ngược về workbook.
/// Ô ⚠️/⚙️ do <see cref="PermissionMatrixAutomatedTests"/> chứng minh vì cần cả case
/// trong/ngoài phạm vi hoặc cả ba chế độ công tắc trong cùng một ô.
/// </summary>
public sealed class PermissionMatrixAccountsTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private async Task<(ApplicationDbContext Db, AccountService Accounts)> SeedAsync()
    {
        await fixture.ResetAsync();
        var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "admin-1", Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "manager-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);

        var userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser>(db),
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);

        return (db, new AccountService(userManager, fixture, NullLogger<AccountService>.Instance));
    }

    private static async Task<int> UserCountAsync(ApplicationDbContext db) =>
        await db.Users.AsNoTracking().CountAsync();

    /// <summary>Vai trò đang gắn trong DB — dùng để chứng minh ô ❌ không đổi quyền hạn.</summary>
    private static async Task<string> RoleOfAsync(ApplicationDbContext db, string userId) =>
        (await (from userRole in db.UserRoles.AsNoTracking()
                join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId
                select role.Name!).ToListAsync())
        .FirstOrDefault() ?? string.Empty;

    /// <summary>Trạng thái khóa trong DB — dùng để chứng minh ô ❌ không khóa được ai.</summary>
    private static async Task<bool> IsLockedAsync(ApplicationDbContext db, string userId) =>
        await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.LockoutEnd != null)
            .FirstAsync();

    /// <summary>Ô G4 ❌: Khách thuê không xem được danh sách người dùng khác.</summary>
    [SqlServerFact]
    public async Task Cell_G4_OP03_KhachThue_IsDenied()
    {
        var (db, accounts) = await SeedAsync();
        await using var _ = db;

        var result = await accounts.ListAsync(null, null, 1, 10, "khach-1", Roles.KhachThue);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    /// <summary>Ô G6 ❌: Khách thuê không tự tạo được tài khoản và AspNetUsers không thêm dòng.</summary>
    [SqlServerFact]
    public async Task Cell_G6_OP05_KhachThue_IsDenied()
    {
        var (db, accounts) = await SeedAsync();
        await using var _ = db;

        var before = await UserCountAsync(db);
        var result = await accounts.CreateAsync(
            new CreateAccountRequest { FullName = "Khach Moi", PhoneNumber = "0909999999", Role = Roles.KhachThue },
            "khach-1",
            Roles.KhachThue);

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.RoleNotAllowed, result.ErrorCode);
        Assert.Equal(before, await UserCountAsync(db));
    }

    /// <summary>Ô F7 ❌: Khóa/mở tài khoản là cố định cho Admin (A49), Chủ trọ và Quản lý bị chặn.</summary>
    [SqlServerFact]
    public async Task Cell_F7_OP06_QuanLy_IsDenied()
    {
        var (db, accounts) = await SeedAsync();
        await using var _ = db;

        var before = await IsLockedAsync(db, "khach-1");
        var result = await accounts.LockAsync("khach-1", "manager-1", Roles.QuanLy);

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await IsLockedAsync(db, "khach-1"));
    }

    /// <summary>Ô G7 ❌: Khách thuê không khóa được tài khoản nào kể cả của mình.</summary>
    [SqlServerFact]
    public async Task Cell_G7_OP06_KhachThue_IsDenied()
    {
        var (db, accounts) = await SeedAsync();
        await using var _ = db;

        var before = await IsLockedAsync(db, "khach-1");
        var result = await accounts.LockAsync("khach-1", "khach-1", Roles.KhachThue);

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await IsLockedAsync(db, "khach-1"));
    }

    /// <summary>Ô E8 ❌: Cấp/sửa role chỉ dành cho Admin (A49), Chủ trọ cũng không được.</summary>
    [SqlServerFact]
    public async Task Cell_E8_OP07_ChuTro_IsDenied()
    {
        var (db, accounts) = await SeedAsync();
        await using var _ = db;

        var before = await RoleOfAsync(db, "manager-1");
        var result = await accounts.ChangeRoleAsync("manager-1", Roles.KhachThue, "owner-a", Roles.ChuTro);

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await RoleOfAsync(db, "manager-1"));
    }

    /// <summary>Ô F8 ❌: Quản lý không tự nâng role của mình lên Chủ trọ.</summary>
    [SqlServerFact]
    public async Task Cell_F8_OP07_QuanLy_IsDenied()
    {
        var (db, accounts) = await SeedAsync();
        await using var _ = db;

        var before = await RoleOfAsync(db, "manager-1");
        var result = await accounts.ChangeRoleAsync("manager-1", Roles.ChuTro, "manager-1", Roles.QuanLy);

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await RoleOfAsync(db, "manager-1"));
    }

    /// <summary>Ô G8 ❌: Khách thuê không cấp được role.</summary>
    [SqlServerFact]
    public async Task Cell_G8_OP07_KhachThue_IsDenied()
    {
        var (db, accounts) = await SeedAsync();
        await using var _ = db;

        var before = await RoleOfAsync(db, "khach-1");
        var result = await accounts.ChangeRoleAsync("khach-1", Roles.QuanLy, "khach-1", Roles.KhachThue);

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await RoleOfAsync(db, "khach-1"));
    }

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô D2 ✅: OP01 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D2_OP01_Admin_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D2");

    /// <summary>Ô E2 ✅: OP01 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E2_OP01_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E2");

    /// <summary>Ô F2 ✅: OP01 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F2_OP01_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F2");

    /// <summary>Ô G2 ✅: OP01 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G2_OP01_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G2");

    /// <summary>Ô D3 ✅: OP02 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D3_OP02_Admin_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D3");

    /// <summary>Ô E3 ✅: OP02 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E3_OP02_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E3");

    /// <summary>Ô F3 ✅: OP02 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F3_OP02_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F3");

    /// <summary>Ô G3 ✅: OP02 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G3_OP02_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G3");

    /// <summary>Ô D4 ⚠️: OP03 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D4_OP03_Admin_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D4");

    /// <summary>Ô E4 ✅: OP03 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E4_OP03_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E4");

    /// <summary>Ô F4 ⚠️: OP03 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F4_OP03_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F4");

    /// <summary>Ô D5 ✅: OP04 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D5_OP04_Admin_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D5");

    /// <summary>Ô E5 ✅: OP04 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E5_OP04_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E5");

    /// <summary>Ô F5 ✅: OP04 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F5_OP04_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F5");

    /// <summary>Ô G5 ✅: OP04 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G5_OP04_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G5");

    /// <summary>Ô D6 ✅: OP05 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D6_OP05_Admin_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D6");

    /// <summary>Ô E6 ✅: OP05 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E6_OP05_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E6");

    /// <summary>Ô F6 ⚠️: OP05 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F6_OP05_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F6");

    /// <summary>Ô D7 ✅: OP06 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D7_OP06_Admin_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D7");

    /// <summary>Ô E7 ❌: OP06 - ChuTro (probe hành vi thật). Khóa tài khoản chỉ dành cho Admin.</summary>
    [SqlServerFact]
    public async Task Cell_E7_OP06_ChuTro_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E7");

    /// <summary>Ô D8 ✅: OP07 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D8_OP07_Admin_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D8");

    /// <summary>Ô D11 ✅: OP10 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D11_OP10_Admin_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D11");

    /// <summary>Ô E11 ✅: OP10 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E11_OP10_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E11");

    /// <summary>Ô F11 ❌: OP10 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F11_OP10_QuanLy_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F11");

    /// <summary>Ô G11 ❌: OP10 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G11_OP10_KhachThue_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G11");
}
