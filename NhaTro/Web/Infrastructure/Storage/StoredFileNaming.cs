using System.Buffers;
using System.Globalization;
using Web.Infrastructure.Time;

namespace Web.Infrastructure.Storage;

/// <summary>Tên tệp và đường dẫn do server sinh (VHT-12 mục 6): không dùng tên client gửi lên.</summary>
public static class StoredFileNaming
{
    /// <summary>Tên lưu trên server: uuid (không dấu gạch) + đuôi đã chuẩn hoá, ví dụ <c>a1b2...f9.pdf</c>.</summary>
    public static string NewStoredFileName(Guid id, string extension) => string.Concat(id.ToString("N"), extension);

    /// <summary>
    /// Đường dẫn tương đối theo năm/tháng giờ Việt Nam, ví dụ <c>2026/10/a1b2...f9.pdf</c>.
    /// Dùng dấu <c>/</c> ổn định trên mọi nền tảng; <c>Path.Combine</c> hiểu cả hai.
    /// </summary>
    public static string BuildRelativePath(DateTimeOffset uploadedAtUtc, string storedFileName)
    {
        var vietnam = TimeZoneInfo.ConvertTime(uploadedAtUtc, VietnamTime.TimeZone);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{vietnam.Year:D4}/{vietnam.Month:D2}/{storedFileName}");
    }
}

/// <summary>Đọc stream upload vào bộ nhớ với giới hạn dung lượng ngay khi đọc, không nạp vô hạn.</summary>
public static class LimitedStreamReader
{
    private const int BufferSize = 81920;

    /// <summary>
    /// Đọc toàn bộ <paramref name="source"/> nhưng dừng ngay khi vượt <paramref name="maxBytes"/>.
    /// Ném <see cref="StorageFileTooLargeException"/> hoặc <see cref="StorageEmptyFileException"/>.
    /// </summary>
    public static async Task<byte[]> ReadAllBytesAsync(
        Stream source,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);

        using var buffer = new MemoryStream(capacity: (int)Math.Min(maxBytes, 1024 * 1024));
        var chunk = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(chunk.AsMemory(0, chunk.Length), cancellationToken)) > 0)
            {
                total += read;
                if (total > maxBytes)
                {
                    throw new StorageFileTooLargeException(maxBytes);
                }

                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }

            if (total == 0)
            {
                throw new StorageEmptyFileException();
            }

            return buffer.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
        }
    }
}
