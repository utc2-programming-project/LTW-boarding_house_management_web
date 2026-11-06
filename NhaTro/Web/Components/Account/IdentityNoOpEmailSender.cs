// Sender email dự phòng (NoOp) dùng khi chưa cấu hình SMTP.
using Microsoft.AspNetCore.Identity;
using Web.Data;

namespace Web.Components.Account;

/// <summary>
/// Sender dự phòng khi chưa cấu hình SMTP. Ở Development, nội dung thư (kèm liên kết đặt lại
/// mật khẩu) được ghi ra terminal để vẫn test được luồng quên mật khẩu mà không cần hộp thư thật.
/// </summary>
/// <remarks>
/// Xóa nhánh <c>else if (EmailSender is IdentityNoOpEmailSender)</c> trong RegisterConfirmation.razor
/// khi đã có sender thật.
/// </remarks>
internal sealed class IdentityNoOpEmailSender(
    IHostEnvironment environment,
    ILogger<IdentityNoOpEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirm your email",
            $"Please confirm your account by <a href='{confirmationLink}'>clicking here</a>.");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Reset your password",
            $"Please reset your password by <a href='{resetLink}'>clicking here</a>.");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Reset your password",
            $"Please reset your password using the following code: {resetCode}");

    private Task SendAsync(string email, string subject, string html)
    {
        if (environment.IsDevelopment())
        {
            // Chỉ ở Development: in cả nội dung để lấy được link đặt lại mật khẩu từ terminal.
            logger.LogWarning(
                "[MAIL][NoOp] Chưa cấu hình SMTP, không gửi thật. Tới: {Email} | Tiêu đề: {Subject} | Nội dung: {Body}",
                email, subject, html);
        }
        else
        {
            logger.LogWarning(
                "[MAIL][NoOp] Bỏ qua thư '{Subject}' tới {Email}: chưa cấu hình SMTP.", subject, email);
        }

        return Task.CompletedTask;
    }
}
