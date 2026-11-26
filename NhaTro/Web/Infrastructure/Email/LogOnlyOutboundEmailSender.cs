using Microsoft.Extensions.Options;
using Web.Configuration.Options;

namespace Web.Infrastructure.Email;

/// <summary>
/// Sender dự phòng khi chưa cấu hình SMTP: ghi nội dung ra log để vẫn test được luồng mời.
/// Không ghi body (OTP/link) trừ khi bật rõ ràng ở Development.
/// </summary>
public sealed class LogOnlyOutboundEmailSender(
    IHostEnvironment environment,
    IOptions<EmailOptions> options,
    ILogger<LogOnlyOutboundEmailSender> logger)
    : IOutboundEmailSender
{
    public Task SendAsync(string toEmail, string subject, string html, CancellationToken ct = default)
    {
        if (environment.IsDevelopment() && options.Value.LogBodyInDevelopment)
        {
            logger.LogWarning("[MAIL][NoOp] Tới: {Email} | Tiêu đề: {Subject} | Nội dung: {Body}", toEmail, subject, html);
        }
        else if (environment.IsDevelopment())
        {
            logger.LogWarning("[MAIL][NoOp] Tới: {Email} | Tiêu đề: {Subject} | Body ẩn.", toEmail, subject);
        }
        else
        {
            logger.LogWarning("[MAIL][NoOp] Bỏ qua thư '{Subject}' tới {Email}: chưa cấu hình SMTP.", subject, toEmail);
        }

        return Task.CompletedTask;
    }
}
