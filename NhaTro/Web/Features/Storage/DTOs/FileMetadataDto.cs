namespace Web.Features.Storage.DTOs;

/// <summary>
/// Metadata file trả về sau khi upload hoặc khi truy vấn.
/// </summary>
public class FileMetadataDto
{
    public required string Id { get; init; }
    public required string FileName { get; init; }
    public required string StoredFileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
    public required string UploadedByUserId { get; init; }
    public required DateTimeOffset UploadedAt { get; init; }
    public required string RelatedEntityId { get; init; }
    public required string RelatedEntityType { get; init; }
    public string? Description { get; init; }
    public required bool IsDeleted { get; init; }
}
