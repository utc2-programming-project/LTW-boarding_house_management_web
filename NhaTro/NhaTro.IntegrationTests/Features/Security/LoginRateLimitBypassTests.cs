using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// VHT-27: đường dẫn login có dấu '/' cuối không được lách giới hạn tần suất.
/// Lớp test riêng để có app (và bộ đếm rate limit) mới.
/// </summary>
public sealed class LoginRateLimitBypassTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Duong_dan_co_dau_gach_cuoi_van_bi_gioi_han()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        for (var attempt = 1; attempt <= 12; attempt++)
        {
            var response = await client.PostAsync(
                "/Account/Login/",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = TestDataSeeder.AdminEmail,
                    ["Input.Password"] = "SaiMatKhau123!",
                }));

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return;
            }
        }

        Assert.Fail("Đường dẫn '/Account/Login/' lách được giới hạn tần suất (không nhận 429 sau 12 lần).");
    }
}
