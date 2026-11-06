using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// Luồng quên mật khẩu: gửi thư chứa liên kết → mở liên kết → đặt mật khẩu mới → đăng nhập được.
/// Dùng sender giả để bắt liên kết, không phụ thuộc SMTP/hộp thư thật.
/// </summary>
public sealed class ForgotPasswordFlowTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Gui_lien_ket_dat_lai_mat_khau_va_doi_duoc_mat_khau()
    {
        var capture = new CapturingEmailSender();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender<ApplicationUser>>();
                services.AddSingleton<IEmailSender<ApplicationUser>>(capture);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        // 1. Mở form và gửi yêu cầu quên mật khẩu.
        var (token, handler) = await LoginFormClient.GetAnyFormAsync(client, "/Account/ForgotPassword");

        var fields = new Dictionary<string, string>
        {
            ["Input.Email"] = TestDataSeeder.AdminEmail,
            ["__RequestVerificationToken"] = token,
        };
        if (!string.IsNullOrEmpty(handler))
        {
            fields["_handler"] = handler;
        }

        var request = await client.PostAsync("/Account/ForgotPassword", new FormUrlEncodedContent(fields));
        Assert.Equal(HttpStatusCode.Redirect, request.StatusCode);
        Assert.Contains("ForgotPasswordConfirmation", request.Headers.Location!.ToString());

        // 2. Thư phải chứa liên kết đặt lại mật khẩu.
        Assert.NotNull(capture.ResetLink);
        var resetUrl = WebUtility.HtmlDecode(capture.ResetLink!);
        Assert.Contains("/Account/ResetPassword", resetUrl);
        Assert.Contains("code=", resetUrl);

        // 3. Mở liên kết: phải ra trang nhập mật khẩu mới, không phải trang lỗi.
        var relative = new Uri(resetUrl).PathAndQuery;
        var page = await client.GetAsync(relative);
        page.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync());
        Assert.Contains("reset-password", html);

        // 4. Gửi mật khẩu mới.
        var (resetToken, resetHandler) = await LoginFormClient.GetAnyFormAsync(client, relative);
        var code = new Uri(resetUrl).Query
            .TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && pair[0] == "code")
            .Select(pair => WebUtility.UrlDecode(pair[1]))
            .First();

        Assert.False(string.IsNullOrWhiteSpace(code));
        const string newPassword = "MatKhauMoi123!";

        var resetFields = new Dictionary<string, string>
        {
            ["Input.Email"] = TestDataSeeder.AdminEmail,
            ["Input.Password"] = newPassword,
            ["Input.ConfirmPassword"] = newPassword,
            ["Input.Code"] = code,
            ["__RequestVerificationToken"] = resetToken,
        };
        if (!string.IsNullOrEmpty(resetHandler))
        {
            resetFields["_handler"] = resetHandler;
        }

        var resetResponse = await client.PostAsync(relative, new FormUrlEncodedContent(resetFields));
        Assert.Equal(HttpStatusCode.Redirect, resetResponse.StatusCode);
        Assert.Contains("ResetPasswordConfirmation", resetResponse.Headers.Location!.ToString());

        // 5. Mật khẩu mới phải dùng được thật.
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(TestDataSeeder.AdminEmail);
        Assert.NotNull(admin);
        Assert.True(await users.CheckPasswordAsync(admin, newPassword));
    }

    private sealed class CapturingEmailSender : IEmailSender<ApplicationUser>
    {
        public string? ResetLink { get; private set; }

        public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
            Task.CompletedTask;

        public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
        {
            ResetLink = resetLink;
            return Task.CompletedTask;
        }

        public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
            Task.CompletedTask;
    }
}
