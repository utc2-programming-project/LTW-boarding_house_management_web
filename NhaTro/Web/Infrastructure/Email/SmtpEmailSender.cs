// Gửi thư thật qua SMTP khi cấu hình Email:Smtp đã đủ.
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
/// PLAN-001 #16: thử 3 lần mỗi máy chủ; máy chủ chính lỗi thì chuyển sang
/// <see cref="EmailOptions.FallbackSmtp"/>. Thư do Identity phát (link xác nhận/đặt lại)
/// vẫn nuốt lỗi để trang công khai không lộ email có tồn tại; thư nghiệp vụ
/// (<see cref="SendAsync(string, string, string, CancellationToken)"/>) ném lỗi để
/// <c>OtpService</c> trả trạng thái Failed thay vì báo đã gửi.
/// </remarks>
public sealed class SmtpEmailSender(
    IOptions<EmailOptions> options,
    EmailRecipientResolver recipientResolver,
    ILogger<SmtpEmailSender> logger) : IEmailSender<ApplicationUser>, IOutboundEmailSender
{
    private const int MaxAttemptsPerProfile = 3;

    /// <summary>Gửi thư tự do (lời mời khách thuê, OTP). Ném lỗi khi cả hai SMTP đều thất bại.</summary>
    public async Task SendAsync(string toEmail, string subject, string html, CancellationToken ct = default)
    {
        var recipient = recipientResolver.Resolve(toEmail);
        await SendCoreAsync(recipient, new EmailTemplates.EmailContent(subject, html), ct);
    }

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink)
    {
        var recipient = recipientResolver.Resolve(email);
        var content = EmailTemplates.ConfirmAccount(user.FullName, email, recipient, confirmationLink);
        return SendBestEffortAsync(recipient, content);
    }

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink)
    {
        var recipient = recipientResolver.Resolve(email);
        var content = EmailTemplates.PasswordReset(user.FullName, email, recipient, resetLink);
        return SendBestEffortAsync(recipient, content);
    }

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode)
    {
        var recipient = recipientResolver.Resolve(email);
        var content = EmailTemplates.PasswordResetCode(user.FullName, email, recipient, resetCode);
        return SendBestEffortAsync(recipient, content);
    }

    private async Task SendBestEffortAsync(EmailRecipient recipient, EmailTemplates.EmailContent content)
    {
        try
        {
            await SendCoreAsync(recipient, content, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Không ném ra: trang quên mật khẩu luôn trả cùng một thông báo.
            logger.LogError(ex, "[MAIL] Bỏ qua lỗi gửi '{Subject}' tới {To}.", content.Subject, recipient.Address);
        }
    }

    private async Task SendCoreAsync(
        EmailRecipient recipient, EmailTemplates.EmailContent content, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipient.Address))
        {
            logger.LogWarning("Bỏ qua thư '{Subject}': không có địa chỉ nhận.", content.Subject);
            return;
        }

        var profiles = new List<SmtpOptions> { options.Value.Smtp };
        var fallback = options.Value.FallbackSmtp;
        if (fallback.Enabled)
        {
            profiles.Add(fallback);
        }

        Exception? lastError = null;
        for (var profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
        {
            var smtp = profiles[profileIndex];
            var isFallback = profileIndex > 0;

            if (!smtp.IsConfigured)
            {
                logger.LogWarning(
                    "[MAIL] Bỏ qua cấu hình SMTP {Host}:{Port} vì chưa đủ thông tin.", smtp.Host, smtp.Port);
                continue;
            }

            for (var attempt = 1; attempt <= MaxAttemptsPerProfile; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    // Log rõ trước khi gửi để nhìn terminal là biết thư đã đi chưa.
                    logger.LogInformation(
                        "[MAIL] Đang gửi '{Subject}' tới {To} (gốc: {Original}) qua {Host}:{Port}{Fallback} — lần {Attempt}/{Max}...",
                        content.Subject, recipient.Address, recipient.OriginalAddress,
                        smtp.Host, smtp.Port, isFallback ? " (dự phòng)" : string.Empty,
                        attempt, MaxAttemptsPerProfile);

                    await SendOnceAsync(smtp, recipient.Address, content);

                    logger.LogInformation(
                        "[MAIL] Gửi thành công '{Subject}' tới {To}{Fallback}.",
                        content.Subject, recipient.Address, isFallback ? " qua SMTP dự phòng" : string.Empty);
                    return;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    lastError = ex;
                    logger.LogWarning(
                        ex,
                        "[MAIL] Lần {Attempt}/{Max} thất bại '{Subject}' tới {To} qua {Host}:{Port}{Fallback}: {Message}",
                        attempt, MaxAttemptsPerProfile, content.Subject, recipient.Address,
                        smtp.Host, smtp.Port, isFallback ? " (dự phòng)" : string.Empty, ex.Message);

                    if (attempt < MaxAttemptsPerProfile)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), ct);
                    }
                }
            }
        }

        throw new SmtpException(
            $"Không gửi được thư '{content.Subject}' tới {recipient.Address} qua mọi cấu hình SMTP.",
            lastError);
    }

    private static async Task SendOnceAsync(
        SmtpOptions smtp, string address, EmailTemplates.EmailContent content)
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
        message.To.Add(address);

        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = smtp.UseStartTls,
            Timeout = smtp.TimeoutSeconds * 1000,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtp.UserName, smtp.EffectivePassword),
        };

        await client.SendMailAsync(message);
    }
}
