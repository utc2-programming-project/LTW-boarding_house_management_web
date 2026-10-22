namespace Web.Infrastructure.Storage;

/// <summary>Nội dung tệp vượt giới hạn cho phép khi đọc stream.</summary>
public sealed class StorageFileTooLargeException(long maxBytes)
    : Exception($"Tệp vượt quá giới hạn {maxBytes} byte.")
{
    public long MaxBytes { get; } = maxBytes;
}

/// <summary>Tệp rỗng (0 byte) không được lưu.</summary>
public sealed class StorageEmptyFileException() : Exception("Tệp không có nội dung.");

/// <summary>
/// Đường dẫn tương đối không hợp lệ hoặc trỏ ra ngoài thư mục gốc. Chỉ xảy ra với lỗi lập trình
/// (đường dẫn do server sinh), không phải dữ liệu người dùng; không trả chi tiết cho client.
/// </summary>
public sealed class StoragePathViolationException(string relativePath)
    : Exception("Đường dẫn tệp không hợp lệ.")
{
    public string RelativePath { get; } = relativePath;
}
