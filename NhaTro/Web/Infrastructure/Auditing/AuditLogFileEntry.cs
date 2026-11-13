using Web.Features.Audit.Entities;

namespace Web.Infrastructure.Auditing;

/// <summary>
/// Một dòng nhật ký lưu trong file JSONL (VHT-104). Không chứa mật khẩu, token hay nội dung giấy tờ.
/// </summary>
public sealed record AuditLogFileEntry(
    Guid Id,
    AuditLogCategory Category,
    string Action,
    string? ActorUserId,
    int? PropertyId,
    string? OwnerId,
    string? Metadata,
    DateTimeOffset OccurredAt);
