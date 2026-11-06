using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;

namespace Web.Infrastructure.Email;

/// <summary>
/// Gửi thư thật qua SMTP (mặc định Gmail + App Password). Đăng ký thay cho
/// <c>IdentityNoOpEmailSender</c> khi cấu hình "Email:Smtp" đã đủ tài khoản/mật khẩu.
/// </summary>
/// <remarks>
/// Lỗi SMTP chỉ ghi log, không ném ra ngoài: trang "Quên mật khẩu" luôn trả cùng một
/// thông báo để không lộ email có tồn tại hay không, và người dùng không thấy lỗi 500.
/// </remarks>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    EmailRecipientResolver recipientResolver,
    ILogger<SmtpEmailSender> logger) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var recipient = recipientResolver.Resolve(email);
        var content = EmailTemplates.ConfirmAccount(user.FullName, email, recipient, confirmationLink);
        return SendAsync(recipient, content);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var recipient = recipientResolver.Resolve(email);
        var content = EmailTemplates.PasswordReset(user.FullName, email, recipient, resetLink);
        return SendAsync(recipient, content);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var recipient = recipientResolver.Resolve(email);
        var content = EmailTemplates.PasswordResetCode(user.FullName, email, recipient, resetCode);
        return SendAsync(recipient, content);
    }

    private async Task SendAsync(EmailRecipient recipient, EmailTemplates.EmailContent content)
    {
        var smtp = options.Value.Smtp;

        if (string.IsNullOrWhiteSpace(recipient.Address))
        {
            logger.LogWarning("Bỏ qua thư '{Subject}': không có địa chỉ nhận.", content.Subject);
            return;
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(smtp.EffectiveFromAddress, smtp.FromName, Encoding.UTF8),
                Subject = content.Subject,
                SubjectEncoding = Encoding.UTF8,
                Body = content.Html,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true,
            };
            message.To.Add(recipient.Address);

            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.UseStartTls,
                Timeout = smtp.TimeoutSeconds * 1000,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(smtp.UserName, smtp.EffectivePassword),
            };

            // Log rõ trước khi gửi để nhìn terminal là biết thư đã đi chưa.
            logger.LogInformation(
                "[MAIL] Đang gửi '{Subject}' tới {To} (gốc: {Original}) qua {Host}:{Port}...",
                content.Subject, recipient.Address, recipient.OriginalAddress, smtp.Host, smtp.Port);

            await client.SendMailAsync(message);

            logger.LogInformation(
                "[MAIL] Gửi thành công '{Subject}' tới {To}.",
                content.Subject, recipient.Address);
        }
        catch (SmtpException ex)
        {
            // Không ném lại: người dùng chỉ thấy thông báo chung, lỗi thật nằm trong log.
            logger.LogError(
                ex,
                "[MAIL] Gửi thất bại '{Subject}' tới {To}: {Status} - {Message}",
                content.Subject, recipient.Address, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "[MAIL] Gửi thất bại '{Subject}' tới {To} (SMTP {Host}:{Port}).",
                content.Subject, recipient.Address, smtp.Host, smtp.Port);
        }
    }
}
