using System.Net;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// PLAN-038: hai form gửi mail bị giới hạn tần suất theo IP; vượt ngưỡng được chuyển về
/// chính trang đó kèm cờ <c>tooManyRequests=1</c> thay vì trả 429 text thô.
/// Dùng lớp riêng để không lấn bộ đếm với test đăng nhập/upload.
/// </summary>
public sealed class ForgotPasswordRateLimitTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Quen_mat_khau_qua_nguong_dua_ve_form_co_thong_bao()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        // Ngưỡng mặc định 5 lần/15 phút; gửi dư để chắc chắn chạm giới hạn. Các lần đầu
        // bị antiforgery trả 400 nên chỉ dừng khi thấy chuyển hướng của rate limiter.
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var response = await client.PostAsync(
                "/Account/ForgotPassword",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = TestDataSeeder.AdminEmail,
                }));

            if (response.StatusCode == HttpStatusCode.SeeOther)
            {
                Assert.Equal(
                    "/Account/ForgotPassword?tooManyRequests=1",
                    response.Headers.Location?.ToString());
                return;
            }
        }

        Assert.Fail("Không nhận được chuyển hướng sau 10 lần POST /Account/ForgotPassword.");
    }

    [WebAppFact]
    public async Task Gui_lai_xac_nhan_email_qua_nguong_dua_ve_form_co_thong_bao()
    {
        using var client = fixture.CreateClient(allowAutoRedirect: false);

        for (var attempt = 1; attempt <= 10; attempt++)
        {
            var response = await client.PostAsync(
                "/Account/ResendEmailConfirmation",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["Input.Email"] = TestDataSeeder.AdminEmail,
                }));

            if (response.StatusCode == HttpStatusCode.SeeOther)
            {
                Assert.Equal(
                    "/Account/ResendEmailConfirmation?tooManyRequests=1",
                    response.Headers.Location?.ToString());
                return;
            }
        }

        Assert.Fail("Không nhận được chuyển hướng sau 10 lần POST /Account/ResendEmailConfirmation.");
    }
}
