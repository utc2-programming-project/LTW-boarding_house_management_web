using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// Sai quá ngưỡng phải chặn theo IP và báo kèm thời gian, không được hiện nhầm thông báo
/// "thao tác quá nhanh" của rate limiter; tài khoản mục tiêu cũng không bị khoá.
/// </summary>
public sealed class LoginIpBlockTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Chan_ip_hien_dung_thong_bao_va_khong_khoa_tai_khoan()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        // Ngưỡng mặc định 10 lần sai/15 phút; limiter cho 10 POST/phút nên cả 10 lần đều tới trang.
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var response = await LoginFormClient.PostFreshAsync(
                client, TestDataSeeder.AdminEmail, "SaiMatKhau123!");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var page = await client.GetAsync("/Account/Login");
        page.EnsureSuccessStatusCode();
        var html = await page.Content.ReadAsStringAsync();
        var text = System.Net.WebUtility.HtmlDecode(html);

        Assert.Contains("nhập sai quá nhiều lần", text);
        Assert.DoesNotContain("thao tác quá nhanh", text);

        // Không được khoá tài khoản: kẻ tấn công chỉ chặn được chính IP của mình.
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(TestDataSeeder.AdminEmail);

        Assert.NotNull(admin);
        Assert.Null(admin.LockoutEnd);
    }
}
