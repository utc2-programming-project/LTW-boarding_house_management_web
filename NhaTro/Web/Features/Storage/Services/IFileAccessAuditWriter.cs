using Web.Features.Storage.Enums;

namespace Web.Features.Storage.Services;

/// <summary>Dữ liệu một dòng audit truy cập tệp; chỉ metadata, không có nội dung/tên tệp.</summary>
public sealed record FileAccessAuditEntry(
    string ActorUserId,
    string ActorRole,
    FileAccessAction Action,
    Guid? FileId,
    FileCategory? FileCategory,
    RelatedEntityType? RelatedEntityType,
    string? RelatedEntityId,
    bool Succeeded,
    string? ErrorCode);

/// <summary>
/// Ghi audit truy cập tệp vào SQL Server (tech-stack: lịch sử thao tác nghiệp vụ lưu trong SQL,
/// không dùng log kỹ thuật thay cho dữ liệu). Lỗi ghi audit không làm hỏng thao tác nghiệp vụ.
/// </summary>
public interface IFileAccessAuditWriter
{
    Task WriteAsync(FileAccessAuditEntry entry, CancellationToken cancellationToken = default);
}
