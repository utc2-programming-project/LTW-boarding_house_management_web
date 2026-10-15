using System.Security.Claims;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Rooms;
using Web.Features.Tenants;

namespace NhaTro.UnitTests.Features.Accounts;

public class GuardTests
{
    private static ClaimsPrincipal PrincipalOf(params string[] roles)
    {
        var claims = roles.Select(r => new Claim(ClaimTypes.Role, r));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public void Admin_goi_nghiep_vu_bi_tu_choi()
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => BusinessGuard.EnsureBusinessAllowed(PrincipalOf(Roles.Admin)));
    }

    [Theory]
    [InlineData(Roles.ChuTro)]
    [InlineData(Roles.QuanLy)]
    [InlineData(Roles.KhachThue)]
    public void Ba_vai_tro_nghiep_vu_duoc_qua_cong_1(string role)
    {
        BusinessGuard.EnsureBusinessAllowed(PrincipalOf(role));
    }

    [Fact]
    public void ChuTro_doi_role_bi_tu_choi()
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => BusinessGuard.EnsureCanChangeRole(PrincipalOf(Roles.ChuTro)));
    }

    [Fact]
    public void Khong_duoc_go_Admin_cuoi_cung()
    {
        var reason = RoleChangeGuard.Check(
            callerIsAdmin: true, "admin-1", "admin-2",
            targetIsAdmin: true, targetIsLastAdmin: true, Roles.ChuTro);
        Assert.NotNull(reason);
    }

    [Fact]
    public void Khong_duoc_tu_doi_role_cua_minh()
    {
        var reason = RoleChangeGuard.Check(
            callerIsAdmin: true, "admin-1", "admin-1",
            targetIsAdmin: true, targetIsLastAdmin: false, Roles.ChuTro);
        Assert.NotNull(reason);
    }

    [Fact]
    public void Admin_doi_QuanLy_sang_KhachThue_duoc_phep()
    {
        var reason = RoleChangeGuard.Check(
            callerIsAdmin: true, "admin-1", "user-9",
            targetIsAdmin: false, targetIsLastAdmin: false, Roles.KhachThue);
        Assert.Null(reason);
    }

    [Fact]
    public void Doi_ID_sang_nha_nguoi_khac_bi_chan()
    {
        Assert.Throws<UnauthorizedAccessException>(
            () => PropertyScope.EnsureCanAccess("owner-A", "owner-B", Roles.ChuTro));
        Assert.Throws<UnauthorizedAccessException>(
            () => RoomScope.EnsureCanAccess("owner-A", "owner-B", Roles.ChuTro));
        Assert.Throws<UnauthorizedAccessException>(
            () => TenantScope.EnsureCanAccess("user-A", "user-B", Roles.KhachThue));
    }

    [Fact]
    public void Dung_chu_duoc_qua()
    {
        PropertyScope.EnsureCanAccess("owner-A", "owner-A", Roles.ChuTro);
        RoomScope.EnsureCanAccess("owner-A", "owner-A", Roles.ChuTro);
        TenantScope.EnsureCanAccess("user-A", "user-A", Roles.KhachThue);
    }
}
