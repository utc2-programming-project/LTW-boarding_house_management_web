using Web.Features.Storage.Enums;

namespace Web.Features.Tenants.Documents;

/// <summary>
/// Metadata giấy tờ trả cho UI P3 (VHT-36). Chỉ có trường client được biết:
/// không có tên lưu nội bộ, đường dẫn, cờ xóa hay nội dung tệp.
/// </summary>
public sealed class TenantDocumentMetadataDto
{
    public required Guid Id { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required long FileSizeBytes { get; init; }

    public required FileCategory FileCategory { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }

    public required string UploadedByUserId { get; init; }

    public string? Description { get; init; }
}
