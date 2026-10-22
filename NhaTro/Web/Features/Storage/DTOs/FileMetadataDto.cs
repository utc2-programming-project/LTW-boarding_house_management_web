using Web.Features.Storage.Enums;

namespace Web.Features.Storage.DTOs;

/// <summary>
/// Metadata file trả về client. Không chứa đường dẫn nội bộ (StoredFileName) hay cờ xóa;
/// các trường đó thuộc entity/model nội bộ.
/// </summary>
public class FileMetadataDto
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required FileCategory FileCategory { get; init; }
    public required string UploadedByUserId { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public required string RelatedEntityId { get; init; }
    public required RelatedEntityType RelatedEntityType { get; init; }
    public string? Description { get; init; }
}
