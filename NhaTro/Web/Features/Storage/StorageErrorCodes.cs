namespace Web.Features.Storage;

/// <summary>
/// Mã lỗi storage dùng chung giữa service và endpoint (VHT-12 mục 5).
/// Giữ khớp với cấu trúc JSON của <c>UploadResult</c>.
/// </summary>
public static class StorageErrorCodes
{
    public const string FileSizeExceeded = "FileSizeExceeded";
    public const string InvalidFileType = "InvalidFileType";
    public const string InvalidRequest = "InvalidRequest";
    public const string UploadFailed = "UploadFailed";
    public const string FileNotFound = "FileNotFound";
    public const string Forbidden = "Forbidden";
    public const string Unauthorized = "Unauthorized";
    public const string EmptyFile = "EmptyFile";
    public const string RelatedEntityNotFound = "RelatedEntityNotFound";
    public const string FileCountExceeded = "FileCountExceeded";
}
