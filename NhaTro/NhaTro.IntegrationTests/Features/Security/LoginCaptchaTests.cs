using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>Sau 3 lần đăng nhập sai, form hiển thị CAPTCHA cục bộ.</summary>
public sealed class LoginCaptchaTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Hien_captcha_sau_3_lan_nhap_sai()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        HttpResponseMessage response = null!;
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            response = await LoginFormClient.PostFreshAsync(
                client, TestDataSeeder.AdminEmail, "SaiMatKhau123!");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Input.CaptchaAnswer", html);
        Assert.Contains("Input.CaptchaToken", html);
    }
}
