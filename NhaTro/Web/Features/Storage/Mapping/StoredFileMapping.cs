using Web.Features.Storage.DTOs;
using Web.Features.Storage.Entities;

namespace Web.Features.Storage.Mapping;

/// <summary>Mapping thủ công entity nội bộ → DTO trả client (không dùng AutoMapper).</summary>
public static class StoredFileMapping
{
    /// <summary>
    /// Chỉ trả các trường client được biết; <c>StoredFileName</c>, <c>RelativePath</c> và
    /// <c>IsDeleted</c> ở lại phía server (VHT-12 mục 7.2).
    /// </summary>
    public static FileMetadataDto ToMetadata(StoredFile file) => new()
    {
        Id = file.Id,
        FileName = file.FileName,
        ContentType = file.ContentType,
        FileSizeBytes = file.FileSizeBytes,
        FileCategory = file.FileCategory,
        UploadedByUserId = file.UploadedByUserId,
        UploadedAt = file.UploadedAt,
        RelatedEntityId = file.RelatedEntityId,
        RelatedEntityType = file.RelatedEntityType,
        Description = file.Description,
    };
}
