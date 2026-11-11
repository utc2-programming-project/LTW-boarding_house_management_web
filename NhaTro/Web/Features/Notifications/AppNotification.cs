namespace Web.Features.Notifications;

/// <summary>
/// Thông báo trong hệ thống giữa các vai trò (đăng ký dịch vụ, hóa đơn, thanh toán).
/// Đọc/chưa đọc qua <see cref="IsRead"/>.
/// </summary>
public sealed class AppNotification
{
    public int Id { get; set; }

    public string RecipientUserId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string? Link { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ReadAt { get; set; }
}

public interface INotificationService
{
    Task NotifyAsync(string recipientUserId, string title, string body, string? link = null);

    Task<int> UnreadCountAsync(string userId);

    Task<IReadOnlyList<AppNotification>> ListAsync(string userId, int take = 30);

    Task MarkReadAsync(string userId, int notificationId);

    Task MarkAllReadAsync(string userId);
}
