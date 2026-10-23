using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Storage.Entities;

namespace Web.Features.Storage.Services;

/// <summary>
/// Ghi audit bằng DbContext riêng để thao tác tệp không bị kéo theo transaction của nghiệp vụ khác.
/// Không ghi nội dung/tên tệp; chỉ ghi id và metadata thao tác.
/// </summary>
public sealed class FileAccessAuditWriter(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider timeProvider,
    ILogger<FileAccessAuditWriter> logger) : IFileAccessAuditWriter
{
    public async Task WriteAsync(FileAccessAuditEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            db.FileAccessAuditLogs.Add(new FileAccessAuditLog
            {
                OccurredAt = timeProvider.GetUtcNow(),
                ActorUserId = entry.ActorUserId,
                ActorRole = entry.ActorRole,
                Action = entry.Action,
                FileId = entry.FileId,
                FileCategory = entry.FileCategory,
                RelatedEntityType = entry.RelatedEntityType,
                RelatedEntityId = entry.RelatedEntityId,
                Succeeded = entry.Succeeded,
                ErrorCode = entry.ErrorCode,
            });

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Audit là yêu cầu bắt buộc nhưng không được biến lỗi hạ tầng thành lỗi nghiệp vụ.
            // Log kỹ thuật chỉ có id/action, không có nội dung giấy tờ.
            logger.LogError(
                ex,
                "Không ghi được audit truy cập tệp {FileId} cho hành động {Action}.",
                entry.FileId,
                entry.Action);
        }
    }
}
