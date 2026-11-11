using Microsoft.EntityFrameworkCore;
using Web.Data;

namespace Web.Features.Notifications;

public sealed class NotificationService(ApplicationDbContext db, TimeProvider? timeProvider = null)
    : INotificationService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task NotifyAsync(string recipientUserId, string title, string body, string? link = null)
    {
        if (string.IsNullOrWhiteSpace(recipientUserId))
        {
            return;
        }

        db.AppNotifications.Add(new AppNotification
        {
            RecipientUserId = recipientUserId,
            Title = title,
            Body = body,
            Link = link,
            IsRead = false,
            CreatedAt = _timeProvider.GetUtcNow(),
        });
        await db.SaveChangesAsync();
    }

    public async Task<int> UnreadCountAsync(string userId) =>
        await db.AppNotifications
            .AsNoTracking()
            .CountAsync(item => item.RecipientUserId == userId && !item.IsRead);

    public async Task<IReadOnlyList<AppNotification>> ListAsync(string userId, int take = 30) =>
        await db.AppNotifications
            .AsNoTracking()
            .Where(item => item.RecipientUserId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(take)
            .ToListAsync();

    public async Task MarkReadAsync(string userId, int notificationId)
    {
        var item = await db.AppNotifications
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.RecipientUserId == userId);
        if (item is null || item.IsRead)
        {
            return;
        }

        item.IsRead = true;
        item.ReadAt = _timeProvider.GetUtcNow();
        await db.SaveChangesAsync();
    }

    public async Task MarkAllReadAsync(string userId)
    {
        var items = await db.AppNotifications
            .Where(item => item.RecipientUserId == userId && !item.IsRead)
            .ToListAsync();
        var now = _timeProvider.GetUtcNow();
        foreach (var item in items)
        {
            item.IsRead = true;
            item.ReadAt = now;
        }

        await db.SaveChangesAsync();
    }
}
