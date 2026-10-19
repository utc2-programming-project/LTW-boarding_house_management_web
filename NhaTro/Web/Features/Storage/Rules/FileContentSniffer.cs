using Web.Features.Storage.Enums;

namespace Web.Features.Storage.Rules;

/// <summary>
/// Nhận dạng loại nội dung thật của tệp bằng magic bytes (VHT-12 mục 1).
/// Không tin <c>Content-Type</c> và không tin đuôi tệp do client gửi.
/// </summary>
public static class FileContentSniffer
{
    /// <summary>Số byte đầu cần đọc để nhận dạng; WebP cần 12 byte (RIFF....WEBP).</summary>
    public const int RequiredPrefixLength = 12;

    /// <summary>Nhận dạng MIME từ các byte đầu; trả <c>null</c> khi không khớp định dạng nào được cho phép.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> prefix)
    {
        if (IsJpeg(prefix))
        {
            return "image/jpeg";
        }

        if (IsPng(prefix))
        {
            return "image/png";
        }

        if (IsWebp(prefix))
        {
            return "image/webp";
        }

        if (IsPdf(prefix))
        {
            return "application/pdf";
        }

        return null;
    }

    /// <summary>Nội dung thật có khớp với đuôi tệp đã chọn không.</summary>
    public static bool IsConsistentWithExtension(string extension, string? detectedContentType) =>
        detectedContentType is not null
        && detectedContentType == FileTypeRules.ContentTypeForExtension(extension);

    /// <summary>
    /// Kiểm tra nội dung đã đọc nằm trong loại tệp cho phép: đuôi phải khớp nội dung thật
    /// và loại tệp phải cho phép đuôi đó.
    /// </summary>
    public static bool IsAllowedContent(FileCategory category, string extension, ReadOnlySpan<byte> prefix)
    {
        var detected = DetectContentType(prefix);
        return FileTypeRules.IsExtensionAllowed(category, extension)
            && IsConsistentWithExtension(extension, detected);
    }

    private static bool IsJpeg(ReadOnlySpan<byte> prefix) =>
        prefix.Length >= 3 && prefix[0] == 0xFF && prefix[1] == 0xD8 && prefix[2] == 0xFF;

    private static bool IsPng(ReadOnlySpan<byte> prefix) =>
        prefix.Length >= 8
        && prefix[0] == 0x89 && prefix[1] == 0x50 && prefix[2] == 0x4E && prefix[3] == 0x47
        && prefix[4] == 0x0D && prefix[5] == 0x0A && prefix[6] == 0x1A && prefix[7] == 0x0A;

    private static bool IsWebp(ReadOnlySpan<byte> prefix) =>
        prefix.Length >= 12
        && prefix[0] == 0x52 && prefix[1] == 0x49 && prefix[2] == 0x46 && prefix[3] == 0x46
        && prefix[8] == 0x57 && prefix[9] == 0x45 && prefix[10] == 0x42 && prefix[11] == 0x50;

    private static bool IsPdf(ReadOnlySpan<byte> prefix) =>
        prefix.Length >= 4 && prefix[0] == 0x25 && prefix[1] == 0x50 && prefix[2] == 0x44 && prefix[3] == 0x46;
}
