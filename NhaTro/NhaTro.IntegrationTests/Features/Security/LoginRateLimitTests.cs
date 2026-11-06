using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// Giới hạn tần suất đăng nhập: POST vượt ngưỡng được chuyển về form đăng nhập kèm cờ
/// <c>tooManyRequests=1</c> để hiện thông báo, không trả text thô trên trang trắng.
/// </summary>
public sealed class LoginRateLimitTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Dang_nhap_qua_nhieu_lan_dua_ve_form_co_thong_bao()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        // Ngưỡng mặc định 10 lần/phút; gửi dư để chắc chắn chạm giới hạn.
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            var response = await client.PostAsync(
                "/Account/Login?ReturnUrl=%2F",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = TestDataSeeder.AdminEmail,
                    ["Input.Password"] = "SaiMatKhau123!",
                }));

            if (response.StatusCode == HttpStatusCode.SeeOther)
            {
                var location = response.Headers.Location?.ToString();
                Assert.NotNull(location);
                Assert.StartsWith("/login?tooManyRequests=1", location);
                Assert.Contains("ReturnUrl=%2F", location);

                // Đường dẫn có dấu '/' cuối không được lách giới hạn (cùng IP, cùng bộ đếm).
                var bypass = await client.PostAsync(
                    "/Account/Login/",
                    new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        ["Input.Email"] = TestDataSeeder.AdminEmail,
                        ["Input.Password"] = "SaiMatKhau123!",
                    }));

                Assert.Equal(HttpStatusCode.SeeOther, bypass.StatusCode);
                return;
            }
        }

        Assert.Fail("Không nhận được chuyển hướng sau 12 lần đăng nhập liên tiếp.");
    }
}
