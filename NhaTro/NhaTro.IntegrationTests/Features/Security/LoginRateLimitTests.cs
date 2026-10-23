using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>Giới hạn tần suất đăng nhập trả 429 (mỗi lớp test có app riêng nên không ảnh hưởng test khác).</summary>
public sealed class LoginRateLimitTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Dang_nhap_qua_nhieu_lan_tra_429()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        // Ngưỡng mặc định 10 lần/phút; gửi dư để chắc chắn chạm giới hạn.
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            var response = await client.PostAsync(
                "/Account/Login",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = TestDataSeeder.AdminEmail,
                    ["Input.Password"] = "SaiMatKhau123!",
                }));

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.Equal("Bạn thao tác quá nhanh. Vui lòng thử lại sau.",
                    (await response.Content.ReadAsStringAsync()).Trim());
                return;
            }
        }

        Assert.Fail("Không nhận được 429 sau 12 lần đăng nhập liên tiếp.");
    }
}
