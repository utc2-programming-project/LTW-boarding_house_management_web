namespace Web.Features.Storage.DTOs;

/// <summary>
/// Kết quả tải file: stream + metadata.
/// </summary>
public class FileDownloadResponse
{
    public required Stream FileStream { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
    public required long FileSizeBytes { get; init; }
}
