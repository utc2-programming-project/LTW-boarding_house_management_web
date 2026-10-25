using Web.Features.Storage.Enums;

namespace Web.Features.Storage.Entities;

/// <summary>
/// Nhật ký truy cập tệp (VHT-26). Bảng chỉ chứa metadata thao tác:
/// người gọi, vai trò, hành động, id tệp/đối tượng liên quan, thời điểm và kết quả.
/// <b>Không</b> chứa tên tệp, đường dẫn lưu, MIME, nội dung hay bất kỳ dữ liệu nào của giấy tờ.
/// </summary>
public class FileAccessAuditLog
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public FileAccessAction Action { get; set; }

    /// <summary>Id bản ghi tệp; null khi thao tác thất bại trước khi xác định được tệp.</summary>
    public Guid? FileId { get; set; }

    public FileCategory? FileCategory { get; set; }

    public RelatedEntityType? RelatedEntityType { get; set; }

    public string? RelatedEntityId { get; set; }

    public bool Succeeded { get; set; }

    /// <summary>Mã lỗi nghiệp vụ (StorageErrorCodes) khi thất bại; không chứa nội dung tệp.</summary>
    public string? ErrorCode { get; set; }
}
