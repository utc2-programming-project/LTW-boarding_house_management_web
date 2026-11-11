// Test tích hợp luồng quên mật khẩu qua HTTP.
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Infrastructure.Email;

namespace NhaTro.IntegrationTests.Features.Security;

/// <summary>
/// Luồng quên mật khẩu: gửi yêu cầu → nhận mã OTP qua thư → xác thực mã → đặt mật khẩu mới → đăng nhập được.
/// Dùng sender giả để bắt mã, không phụ thuộc SMTP/hộp thư thật.
/// </summary>
public sealed class ForgotPasswordFlowTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Gui_ma_otp_dat_lai_mat_khau_va_doi_duoc_mat_khau()
    {
        var capture = new CapturingOutboundEmailSender();
        using var factory = fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IOutboundEmailSender>();
                services.AddSingleton<IOutboundEmailSender>(capture);
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
        var otpUrl = request.Headers.Location!.OriginalString;
        Assert.Contains("/Account/VerifyOtp", otpUrl);
        Assert.Contains("purpose=PasswordReset", otpUrl);

        // 2. Thư phải chứa mã OTP 6 số.
        var code = capture.ExtractCode();
        Assert.Matches(@"^\d{6}$", code);

        // 3. Mở trang nhập mã và gửi OTP.
        var (otpToken, otpHandler) = await LoginFormClient.GetAnyFormAsync(client, otpUrl);
        var otpFields = new Dictionary<string, string>
        {
            ["Input.Code"] = code,
            ["__RequestVerificationToken"] = otpToken,
        };
        if (!string.IsNullOrEmpty(otpHandler))
        {
            otpFields["_handler"] = otpHandler;
        }

        var otpResponse = await client.PostAsync(otpUrl, new FormUrlEncodedContent(otpFields));
        Assert.Equal(HttpStatusCode.Redirect, otpResponse.StatusCode);
        var resetUrl = otpResponse.Headers.Location!.OriginalString;
        Assert.Contains("/Account/ResetPassword", resetUrl);
        Assert.Contains("code=", resetUrl);

        // 4. Mở trang đặt mật khẩu mới và gửi mật khẩu mới.
        var (resetToken, resetHandler) = await LoginFormClient.GetAnyFormAsync(client, resetUrl);
        const string newPassword = "MatKhauMoi123!";

        var resetFields = new Dictionary<string, string>
        {
            ["Input.Email"] = TestDataSeeder.AdminEmail,
            ["Input.Password"] = newPassword,
            ["Input.ConfirmPassword"] = newPassword,
            ["__RequestVerificationToken"] = resetToken,
        };
        if (!string.IsNullOrEmpty(resetHandler))
        {
            resetFields["_handler"] = resetHandler;
        }

        var resetResponse = await client.PostAsync(resetUrl, new FormUrlEncodedContent(resetFields));
        Assert.Equal(HttpStatusCode.Redirect, resetResponse.StatusCode);
        Assert.Contains("ResetPasswordConfirmation", resetResponse.Headers.Location!.ToString());

        // 5. Mật khẩu mới phải dùng được thật.
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(TestDataSeeder.AdminEmail);
        Assert.NotNull(admin);
        Assert.True(await users.CheckPasswordAsync(admin, newPassword));
    }

    private sealed class CapturingOutboundEmailSender : IOutboundEmailSender
    {
        public string? Html { get; private set; }

        public Task SendAsync(string toEmail, string subject, string html, CancellationToken ct = default)
        {
            Html = html;
            return Task.CompletedTask;
        }

        public string ExtractCode()
        {
            var match = Regex.Match(Html ?? string.Empty, @"letter-spacing:8px[^>]*>\s*(\d{6})");
            Assert.True(match.Success, "Thư OTP không chứa mã 6 số.");
            return match.Groups[1].Value;
        }
    }
}
