using Web.Features.Storage.Enums;

namespace Web.Features.Storage.DTOs;

public sealed record StoredFileTrashItemDto(
    Guid Id,
    string FileName,
    FileCategory FileCategory,
    RelatedEntityType RelatedEntityType,
    string RelatedEntityId,
    long FileSizeBytes,
    DateTimeOffset UploadedAt,
    DateTime? DeletedAt,
    string? DeletedBy);

public sealed record StorageTrashResult(bool Succeeded, string? ErrorCode)
{
    public static StorageTrashResult Success() => new(true, null);

    public static StorageTrashResult Failure(string errorCode) => new(false, errorCode);
}
