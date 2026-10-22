using Web.Features.Accounts;

namespace NhaTro.UnitTests.Features.Accounts;

/// <summary>
/// VHT-8 / D1: luat phan quyen quan tri tai khoan. Ham thuan nen test khong can DB.
/// </summary>
public class AccountPermissionRulesTests
{
    [Fact]
    public void Chi_Admin_duoc_doi_vai_tro()
    {
        Assert.True(AccountPermissionRules.CanChangeRole(Roles.Admin));

        Assert.False(AccountPermissionRules.CanChangeRole(Roles.ChuTro));
        Assert.False(AccountPermissionRules.CanChangeRole(Roles.QuanLy));
        Assert.False(AccountPermissionRules.CanChangeRole(Roles.KhachThue));
    }

    [Theory]
    [InlineData(Roles.Admin, 4)]
    [InlineData(Roles.ChuTro, 1)]
    [InlineData(Roles.QuanLy, 1)]
    [InlineData(Roles.KhachThue, 0)]
    public void So_vai_tro_duoc_phep_gan_theo_D1(string callerRole, int expected)
    {
        Assert.Equal(expected, AccountPermissionRules.AssignableRoles(callerRole).Count);
    }

    [Fact]
    public void ChuTro_chi_tao_duoc_QuanLy_khong_tao_Admin()
    {
        var roles = AccountPermissionRules.AssignableRoles(Roles.ChuTro);
        Assert.Contains(Roles.QuanLy, roles);
        Assert.DoesNotContain(Roles.Admin, roles);
    }

    [Fact]
    public void KhachThue_khong_thay_tai_khoan_nao()
    {
        Assert.Empty(AccountPermissionRules.VisibleRoles(Roles.KhachThue));
        Assert.False(AccountPermissionRules.CanManageAccounts(Roles.KhachThue));
    }

    [Fact]
    public void ChuTro_chi_thay_QuanLy_va_KhachThue()
    {
        var roles = AccountPermissionRules.VisibleRoles(Roles.ChuTro);

        Assert.Contains(Roles.QuanLy, roles);
        Assert.Contains(Roles.KhachThue, roles);
        Assert.DoesNotContain(Roles.Admin, roles);
    }

    [Fact]
    public void Khong_duoc_tu_doi_vai_tro_cua_minh()
    {
        var reason = AccountPermissionRules.CheckChangeRole(
            Roles.Admin, "admin-1", "admin-1", Roles.Admin, targetIsLastAdmin: false, Roles.QuanLy);

        Assert.Equal(AccountErrorCodes.CannotChangeOwnRole, reason);
    }

    [Fact]
    public void Khong_duoc_go_vai_tro_Admin_cuoi_cung()
    {
        var reason = AccountPermissionRules.CheckChangeRole(
            Roles.Admin, "admin-1", "admin-2", Roles.Admin, targetIsLastAdmin: true, Roles.ChuTro);

        Assert.Equal(AccountErrorCodes.CannotRemoveLastAdmin, reason);
    }

    [Fact]
    public void Admin_doi_vai_tro_nguoi_khac_duoc_phep()
    {
        var reason = AccountPermissionRules.CheckChangeRole(
            Roles.Admin, "admin-1", "user-9", Roles.QuanLy, targetIsLastAdmin: false, Roles.KhachThue);

        Assert.Null(reason);
    }

    [Fact]
    public void ChuTro_doi_vai_tro_bi_chan()
    {
        var reason = AccountPermissionRules.CheckChangeRole(
            Roles.ChuTro, "chutro-1", "user-9", Roles.KhachThue, targetIsLastAdmin: false, Roles.QuanLy);

        Assert.Equal(AccountErrorCodes.Forbidden, reason);
    }

    [Fact]
    public void Khong_duoc_tu_khoa_tai_khoan_cua_minh()
    {
        var reason = AccountPermissionRules.CheckLock(
            Roles.Admin, "admin-1", "admin-1", Roles.Admin, unlock: false);

        Assert.Equal(AccountErrorCodes.CannotLockSelf, reason);
    }

    [Fact]
    public void ChuTro_khong_duoc_khoa_Admin()
    {
        var reason = AccountPermissionRules.CheckLock(
            Roles.ChuTro, "chutro-1", "admin-9", Roles.Admin, unlock: false);

        Assert.Equal(AccountErrorCodes.CannotLockAdmin, reason);
    }

    [Fact]
    public void ChuTro_khoa_duoc_QuanLy_va_KhachThue()
    {
        Assert.Null(AccountPermissionRules.CheckLock(
            Roles.ChuTro, "chutro-1", "quanly-9", Roles.QuanLy, unlock: false));
        Assert.Null(AccountPermissionRules.CheckLock(
            Roles.ChuTro, "chutro-1", "khach-9", Roles.KhachThue, unlock: false));
    }

    [Fact]
    public void QuanLy_khong_duoc_khoa_ai()
    {
        var reason = AccountPermissionRules.CheckLock(
            Roles.QuanLy, "quanly-1", "khach-9", Roles.KhachThue, unlock: false);

        Assert.Equal(AccountErrorCodes.Forbidden, reason);
    }

    [Theory]
    [InlineData(Roles.Admin, "RoleAdmin")]
    [InlineData(Roles.ChuTro, "RoleChuTro")]
    [InlineData(Roles.QuanLy, "RoleQuanLy")]
    [InlineData(Roles.KhachThue, "RoleKhachThue")]
    public void Moi_vai_tro_co_key_dich_rieng(string role, string expectedKey)
    {
        Assert.Equal(expectedKey, AccountPermissionRules.RoleResourceKey(role));
    }
}
