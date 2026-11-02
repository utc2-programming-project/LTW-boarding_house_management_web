using System.Security.Claims;
using Web.Features.Accounts;

namespace NhaTro.UnitTests.Features.Accounts;

/// <summary>
/// VHT-119: một quy tắc chọn vai trò duy nhất (Admin → ChuTro → QuanLy → KhachThue).
/// </summary>
public class RoleResolutionTests
{
    private static ClaimsPrincipal User(params string[] roles) =>
        new(new ClaimsIdentity(roles.Select(role => new Claim(ClaimTypes.Role, role)), "test"));

    [Fact]
    public void Mot_vai_tro_tra_dung_vai_tro()
    {
        Assert.Equal(Roles.QuanLy, Roles.ResolvePrimaryRole(User(Roles.QuanLy)));
    }

    [Fact]
    public void ChuTro_thang_QuanLy_va_KhachThue_khi_du_lieu_lech()
    {
        Assert.Equal(
            Roles.ChuTro,
            Roles.ResolvePrimaryRole(User(Roles.KhachThue, Roles.QuanLy, Roles.ChuTro)));
    }

    [Fact]
    public void Admin_thang_moi_vai_tro_khac()
    {
        Assert.Equal(Roles.Admin, Roles.ResolvePrimaryRole(User(Roles.KhachThue, Roles.Admin)));
    }

    [Fact]
    public void Khong_co_vai_tro_tra_chuoi_rong()
    {
        Assert.Equal(
            string.Empty,
            Roles.ResolvePrimaryRole(new ClaimsPrincipal(new ClaimsIdentity())));
    }
}
