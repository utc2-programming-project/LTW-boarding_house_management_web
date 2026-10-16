using Web.Features.Storage.Enums;

namespace Web.Features.Storage.Entities;

/// <summary>
/// Metadata tệp lưu trong SQL (VHT-12 mục 3). Là model nội bộ, không trả thẳng cho client:
/// <see cref="StoredFileName"/> và <see cref="RelativePath"/> không bao giờ xuất hiện trên UI/log.
/// </summary>
public class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Tên tệp gốc do người dùng đặt (đã làm sạch ký tự đường dẫn), dùng cho Content-Disposition.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Tên lưu trên server: uuid + đuôi. Chỉ dùng nội bộ.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>Đường dẫn tương đối trong <c>Storage:RootPath</c> (ví dụ <c>2026/10/uuid.pdf</c>). Chỉ dùng nội bộ.</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>MIME do server xác định từ nội dung thật, không lấy từ client.</summary>
    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public FileCategory FileCategory { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; set; }

    public string RelatedEntityId { get; set; } = string.Empty;

    public RelatedEntityType RelatedEntityType { get; set; }

    public string? Description { get; set; }

    public bool IsDeleted { get; set; }
}
