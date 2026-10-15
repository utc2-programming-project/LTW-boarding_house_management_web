namespace Web.Features.Storage.DTOs;

/// <summary>
/// Kết quả upload: thành công hoặc thất bại.
/// </summary>
public class UploadResult
{
    public required bool Succeeded { get; init; }
    public FileMetadataDto? File { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static UploadResult Success(FileMetadataDto file) =>
        new() { Succeeded = true, File = file };

    public static UploadResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
