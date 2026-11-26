using Microsoft.AspNetCore.Identity;
using Web.Data;
using Web.Features.Notifications;
using Web.Infrastructure.Email;

namespace Web.Features.Accounts.Security;

/// <summary>
/// PLAN-001 #11: cảnh báo bảo mật best-effort (email + thông báo trong ứng dụng).
/// Nơi gọi không được rollback hành động chính nếu gửi lỗi.
/// </summary>
public sealed class SecurityNotifier(
    IOutboundEmailSender emailSender,
    INotificationService notifications,
    UserManager<ApplicationUser> users,
    ILogger<SecurityNotifier> logger)
{
    /// <summary>Gửi email + thông báo trong ứng dụng cho một hành động bảo mật.</summary>
    public async Task NotifyAsync(
        string userId, string action, string? detail = null, string? emailOverride = null, bool inApp = true)
    {
        try
        {
            var user = await users.FindByIdAsync(userId);
            if (user is null)
            {
                return;
            }

            var address = emailOverride ?? user.Email;
            if (!string.IsNullOrWhiteSpace(address))
            {
                var content = EmailTemplates.SecurityAlert(
                    user.FullName, address, new EmailRecipient(address, address), action, detail);
                await emailSender.SendAsync(address, content.Subject, content.Html);
            }

            // Đổi thông tin liên hệ: chỉ báo hộp thư cũ, không tạo thông báo cho tài khoản đích.
            if (inApp && emailOverride is null)
            {
                await notifications.NotifyAsync(
                    userId,
                    "Cảnh báo bảo mật",
                    detail is null ? action : $"{action} ({detail})",
                    "/Account/Manage/Sessions");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không gửi được thông báo bảo mật '{Action}' cho {UserId}.", action, userId);
        }
    }

    /// <summary>Đăng nhập từ thiết bị/IP mới: chỉ tạo thông báo trong ứng dụng (email gửi nền riêng).</summary>
    public async Task NotifyNewLoginInAppAsync(string userId, string? ip, string? userAgent)
    {
        try
        {
            await notifications.NotifyAsync(
                userId,
                "Đăng nhập mới",
                $"Tài khoản vừa đăng nhập từ {Describe(ip, userAgent)}.",
                "/Account/Manage/Sessions");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không tạo được thông báo đăng nhập mới cho {UserId}.", userId);
        }
    }

    /// <summary>Email đăng nhập mới (chạy nền, không tạo thêm thông báo trong ứng dụng).</summary>
    public Task SendNewLoginEmailAsync(string userId, string? ip, string? userAgent, CancellationToken ct = default) =>
        NotifyAsync(userId, "Đăng nhập từ thiết bị mới", Describe(ip, userAgent), emailOverride: null, inApp: false);

    private static string Describe(string? ip, string? userAgent)
    {
        var ua = string.IsNullOrWhiteSpace(userAgent) ? "thiết bị không rõ" : Shorten(userAgent, 120);
        return $"IP {ip ?? "không rõ"} · {ua}";
    }

    private static string Shorten(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
