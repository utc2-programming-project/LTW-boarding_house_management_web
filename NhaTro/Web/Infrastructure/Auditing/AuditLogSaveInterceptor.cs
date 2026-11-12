using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Web.Data;
using Web.Features.Audit.Entities;

namespace Web.Infrastructure.Auditing;

/// <summary>
/// Chặn ghi nhật ký thao tác xuống DB (VHT-104): gỡ các <see cref="AuditLogEntry"/> đang Added
/// khỏi change tracker và chỉ ghi file sau khi DB đã lưu thành công.
/// Nhờ vậy các service hiện có giữ nguyên điểm gọi, không phải sửa constructor.
/// </summary>
public sealed class AuditLogSaveInterceptor(
    AuditLogFileStore store,
    ILogger<AuditLogSaveInterceptor> logger) : SaveChangesInterceptor
{
    private static readonly AsyncLocal<Stack<List<AuditLogFileEntry>>> Pending = new();

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushSync();
        return base.SavedChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        return FlushAsync(cancellationToken, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => Pending.Value?.Clear();

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        Pending.Value?.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static void Capture(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries<AuditLogEntry>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        // Tra chủ trọ trước khi DB xóa dòng: purge nhà xóa luôn Property sau SaveChanges.
        var propertyIds = entries
            .Where(entry => entry.Category == AuditLogCategory.Business && entry.PropertyId is not null)
            .Select(entry => entry.PropertyId!.Value)
            .Distinct()
            .ToList();

        var ownerByProperty = propertyIds.Count == 0 || context is not ApplicationDbContext db
            ? new Dictionary<int, string>()
            : db.Properties
                .AsNoTracking()
                .Where(property => propertyIds.Contains(property.Id))
                .Select(property => new { property.Id, property.OwnerId })
                .ToDictionary(item => item.Id, item => item.OwnerId);

        var captured = entries.Select(entry => new AuditLogFileEntry(
            Guid.NewGuid(),
            entry.Category,
            entry.Action,
            entry.ActorUserId,
            entry.PropertyId,
            entry.Category == AuditLogCategory.Business && entry.PropertyId is { } propertyId
                ? ownerByProperty.GetValueOrDefault(propertyId)
                : null,
            entry.Metadata,
            entry.OccurredAt)).ToList();

        foreach (var tracked in context.ChangeTracker.Entries<AuditLogEntry>())
        {
            tracked.State = EntityState.Detached;
        }

        var stack = Pending.Value ??= new Stack<List<AuditLogFileEntry>>();
        stack.Push(captured);
    }

    private void FlushSync()
    {
        if (TryPop() is not { } entries)
        {
            return;
        }

        try
        {
            store.AppendMany(entries);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không ghi được nhật ký thao tác vào file.");
        }
    }

    private async ValueTask<int> FlushAsync(CancellationToken cancellationToken, int result)
    {
        if (TryPop() is not { } entries)
        {
            return result;
        }

        try
        {
            await store.AppendManyAsync(entries, cancellationToken);
        }
        catch (Exception ex)
        {
            // Nhật ký là best-effort sau commit DB; lỗi file không được làm hỏng nghiệp vụ.
            logger.LogError(ex, "Không ghi được nhật ký thao tác vào file.");
        }

        return result;
    }

    private static List<AuditLogFileEntry>? TryPop() =>
        Pending.Value is { Count: > 0 } stack ? stack.Pop() : null;
}
