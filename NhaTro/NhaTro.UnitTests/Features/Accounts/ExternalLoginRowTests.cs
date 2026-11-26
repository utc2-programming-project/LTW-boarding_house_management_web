using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Web.Features.Accounts.External;

namespace NhaTro.UnitTests.Features.Accounts;

/// <summary>
/// PLAN-012: dòng "Đăng nhập bên ngoài" trong tab Bảo mật chỉ hiện khi có lý do hiện nó —
/// server cấu hình scheme hoặc tài khoản còn liên kết để người dùng gỡ được.
/// </summary>
public class ExternalLoginRowTests
{
    private static AuthenticationScheme Scheme(string name, string? display = null) =>
        new(name, display, typeof(AuthenticationHandler<AuthenticationSchemeOptions>));

    private static UserLoginInfo Login(string provider, string? display = null) =>
        new(provider, "key-1", display);

    [Fact]
    public void Khong_scheme_khong_login_bi_an_khong_hien_thi()
    {
        var row = ExternalLoginRow.Resolve([], []);

        Assert.False(row.Visible);
        Assert.False(row.Linked);
        Assert.Equal(0, row.LinkedCount);
        Assert.Null(row.ProviderLabel);
    }

    [Fact]
    public void Mot_scheme_du_dung_duoi_khi_chua_lien_ket()
    {
        var row = ExternalLoginRow.Resolve([Scheme("Google", "Google")], []);

        Assert.True(row.Visible);
        Assert.False(row.Linked);
        Assert.Equal("Google", row.ProviderLabel);
    }

    [Fact]
    public void Scheme_khong_ten_hien_thi_thi_ten_scheme_du_ten_goi()
    {
        var row = ExternalLoginRow.Resolve([Scheme("Google")], []);

        Assert.Equal("Google", row.ProviderLabel);
    }

    [Fact]
    public void Da_scheme_thi_dung_nhan_chung_khong_du_doan_ten()
    {
        var row = ExternalLoginRow.Resolve([Scheme("Google", "Google"), Scheme("Facebook", "Facebook")], []);

        Assert.True(row.Visible);
        Assert.Null(row.ProviderLabel);
    }

    [Fact]
    public void Lien_ket_da_luu_du_tinh_scheme_de_hien_duoi_du_gan_bo_link()
    {
        var row = ExternalLoginRow.Resolve([], [Login("Google", "Google")]);

        Assert.True(row.Visible);
        Assert.True(row.Linked);
        Assert.Equal(1, row.LinkedCount);
        Assert.Equal("Google", row.ProviderLabel);
    }

    [Fact]
    public void Nhieu_du_lu_lien_ket_dem_dung_so_luong_va_gon_nhan_chung()
    {
        var row = ExternalLoginRow.Resolve(
            [Scheme("Google", "Google"), Scheme("Facebook", "Facebook")],
            [Login("Google"), Login("Facebook")]);

        Assert.True(row.Visible);
        Assert.True(row.Linked);
        Assert.Equal(2, row.LinkedCount);
        Assert.Null(row.ProviderLabel);
    }
}
