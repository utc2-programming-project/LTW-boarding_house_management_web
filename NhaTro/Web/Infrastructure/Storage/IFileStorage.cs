namespace Web.Infrastructure.Storage;

/// <summary>Kết quả ghi một tệp xuống đĩa.</summary>
/// <param name="StoredFileName">Tên do server sinh (uuid + đuôi).</param>
/// <param name="RelativePath">Đường dẫn tương đối trong thư mục gốc; chỉ dùng nội bộ.</param>
/// <param name="SizeBytes">Số byte đã ghi.</param>
public sealed record StoredFileWriteResult(string StoredFileName, string RelativePath, long SizeBytes);

/// <summary>
/// I/O tệp ngoài <c>wwwroot</c> (VHT-13). Không trả đường dẫn vật lý ra ngoài; chỉ làm việc với
/// đường dẫn tương đối do server sinh.
/// </summary>
public interface IFileStorage
{
    /// <summary>Ghi tệp mới với tên do server sinh, trả tên và đường dẫn tương đối.</summary>
    Task<StoredFileWriteResult> SaveAsync(
        ReadOnlyMemory<byte> content,
        string extension,
        DateTimeOffset uploadedAt,
        CancellationToken cancellationToken = default);

    /// <summary>Mở luồng đọc tệp; trả <c>null</c> khi tệp vật lý không còn (metadata mồ côi).</summary>
    Stream? TryOpenRead(string relativePath);

    /// <summary>Tệp vật lý còn tồn tại không.</summary>
    bool Exists(string relativePath);

    /// <summary>Xóa tệp vật lý; bỏ qua khi tệp đã không còn.</summary>
    void Delete(string relativePath);
}
