using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Home;

/// <summary>
/// Trang chủ "/" thay trang Layout preview: kiểm khối dữ liệu đặc trưng theo vai trò
/// trên HTML prerender thật (cookie đăng nhập, không gọi service trực tiếp).
/// </summary>
public sealed class HomePageTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Khach_chua_dang_nhap_bi_chuyen_ve_trang_dang_nhap()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            "/Account/Login",
            response.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }

    [WebAppFact]
    public async Task Chu_tro_thay_thong_ke_va_viec_can_lam_khong_trung_kpi()
    {
        var html = await LoginAndGetHomeAsync(TestDataSeeder.ChuTroAEmail);

        Assert.Contains("Cơ cấu phòng", html);
        Assert.DoesNotContain("Tổng quan hôm nay", html);
        Assert.Contains("Việc cần làm", html);
        Assert.DoesNotContain("Doanh thu tháng", html);
        Assert.Contains("Hoá đơn chưa thu", html);
        Assert.Contains("Hợp đồng sắp hết hạn", html);
        Assert.Equal(1, System.Text.RegularExpressions.Regex.Matches(html, "class=\"kpi-grid ").Count);
        Assert.DoesNotContain("quick-action\" href=\"/dashboard\"", html);
        Assert.DoesNotContain("Trang mẫu giao diện", html);
        Assert.DoesNotContain("Layout preview", html);
    }

    [WebAppFact]
    public async Task Quan_ly_khong_thay_doanh_thu_va_cong_no_tong_hop()
    {
        var html = await LoginAndGetHomeAsync(TestDataSeeder.QuanLyEmail);

        Assert.Contains("Cơ cấu phòng", html);
        Assert.DoesNotContain("Tổng quan hôm nay", html);
        Assert.Contains("Việc cần làm", html);
        Assert.DoesNotContain("Doanh thu tháng", html);
        Assert.DoesNotContain("Công nợ", html);
    }

    [WebAppFact]
    public async Task Dashboard_cu_chuyen_ve_trang_chu()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);
        var login = await LoginFormClient.PostFreshAsync(client, TestDataSeeder.ChuTroAEmail, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync("/dashboard");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal("/", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
    }

    [WebAppFact]
    public async Task Admin_thay_tai_khoan_cho_duyet_va_loi_tat_quan_ly_tai_khoan()
    {
        var html = await LoginAndGetHomeAsync(TestDataSeeder.AdminEmail);

        Assert.Contains("Tài khoản chờ duyệt", html);
        Assert.Contains("Quản lý tài khoản", html);
        Assert.Contains("Phân quyền", html);
        Assert.Contains("Đang hoàn thiện", html);
        Assert.DoesNotContain("Doanh thu tháng", html);
    }

    [WebAppFact]
    public async Task Khach_thue_thay_hoa_don_va_loi_tat_phong_cua_minh()
    {
        var html = await LoginAndGetHomeAsync(TestDataSeeder.KhachThueEmail);

        Assert.Contains("Hoá đơn chưa thanh toán", html);
        Assert.Contains("Phòng của tôi", html);
        Assert.DoesNotContain("Doanh thu tháng", html);
        Assert.DoesNotContain("Tài khoản chờ duyệt", html);
    }

    private async Task<string> LoginAndGetHomeAsync(string email)
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        var login = await LoginFormClient.PostFreshAsync(client, email, TestDataSeeder.Password);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var home = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);

        // Razor mã hóa ký tự ngoài ASCII thành tham chiếu số; giải mã để so khớp chuỗi VI/EN.
        return WebUtility.HtmlDecode(await home.Content.ReadAsStringAsync());
    }
}
