using Web.Features.Storage.Enums;

namespace Web.Features.Storage.Rules;

/// <summary>
/// Quy tắc loại tệp thuần (VHT-12 mục 1): loại đuôi cho phép, cặp loại tệp – đối tượng liên quan,
/// MIME và giới hạn dung lượng mặc định. Không đọc/ghi tệp, không truy cập DB.
/// </summary>
public static class FileTypeRules
{
    private static readonly string[] PdfOnly = [".pdf"];
    private static readonly string[] ImageWithWebp = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] DocumentOrImage = [".pdf", ".jpg", ".jpeg", ".png"];

    private const long FiveMegabytes = 5L * 1024 * 1024;
    private const long TenMegabytes = 10L * 1024 * 1024;

    /// <summary>Đuôi tệp được phép theo loại tệp; danh sách rỗng nghĩa là loại không được hỗ trợ.</summary>
    public static IReadOnlyList<string> AllowedExtensions(FileCategory category) => category switch
    {
        FileCategory.RoomPhoto => ImageWithWebp,
        FileCategory.IncidentPhoto => ImageWithWebp,
        FileCategory.Contract => PdfOnly,
        FileCategory.Invoice => DocumentOrImage,
        FileCategory.IdentityDocument => DocumentOrImage,
        FileCategory.PaymentProof => DocumentOrImage,
        _ => [],
    };

    /// <summary>Đối tượng liên quan hợp lệ của từng loại tệp; <c>null</c> khi loại không được hỗ trợ.</summary>
    public static RelatedEntityType? AllowedRelatedEntity(FileCategory category) => category switch
    {
        FileCategory.RoomPhoto => RelatedEntityType.Room,
        FileCategory.Contract => RelatedEntityType.Contract,
        FileCategory.Invoice => RelatedEntityType.Invoice,
        FileCategory.IdentityDocument => RelatedEntityType.Tenant,
        FileCategory.IncidentPhoto => RelatedEntityType.MaintenanceIncident,
        FileCategory.PaymentProof => RelatedEntityType.Payment,
        _ => null,
    };

    /// <summary>Cặp loại tệp – đối tượng liên quan phải khớp bảng VHT-12 mục 1, không cho trộn.</summary>
    public static bool IsPairValid(FileCategory category, RelatedEntityType relatedEntityType) =>
        AllowedRelatedEntity(category) == relatedEntityType;

    /// <summary>Lấy đuôi tệp (kèm dấu chấm, chữ thường) từ tên gốc; trả chuỗi rỗng khi không có đuôi.</summary>
    public static string GetExtension(string? originalFileName)
    {
        if (string.IsNullOrWhiteSpace(originalFileName))
        {
            return string.Empty;
        }

        // Chỉ lấy phần sau dấu chấm cuối của tên tệp, bỏ mọi ký tự đường dẫn client gửi lên.
        var fileName = Path.GetFileName(originalFileName);
        var dot = fileName.LastIndexOf('.');
        if (dot < 0 || dot == fileName.Length - 1)
        {
            return string.Empty;
        }

        var extension = fileName[dot..].ToLowerInvariant();
        return IsSafeExtension(extension) ? extension : string.Empty;
    }

    public static bool IsExtensionAllowed(FileCategory category, string extension) =>
        !string.IsNullOrEmpty(extension) && AllowedExtensions(category).Contains(extension, StringComparer.Ordinal);

    /// <summary>Chuỗi mô tả đuôi cho thông báo lỗi, ví dụ <c>".pdf, .jpg, .jpeg, .png"</c>.</summary>
    public static string DescribeAllowedExtensions(FileCategory category) =>
        string.Join(", ", AllowedExtensions(category));

    /// <summary>MIME do server xác định từ đuôi/tên lưu, không tin <c>Content-Type</c> của client.</summary>
    public static string ContentTypeForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        ".pdf" => "application/pdf",
        _ => "application/octet-stream",
    };

    /// <summary>Giới hạn dung lượng mặc định theo VHT-12 mục 2, dùng khi cấu hình không khai báo.</summary>
    public static long DefaultMaxBytes(FileCategory category) =>
        category is FileCategory.Contract or FileCategory.IdentityDocument ? TenMegabytes : FiveMegabytes;

    /// <summary>Giới hạn mặc định lớn nhất, dùng khi server chặn request lúc chưa đọc được loại tệp.</summary>
    public static long MaxDefaultBytes() => TenMegabytes;

    /// <summary>Tên tiếng Việt của loại tệp, dùng trong thông báo lỗi hiển thị cho người dùng.</summary>
    public static string DisplayName(FileCategory category) => category switch
    {
        FileCategory.RoomPhoto => "Ảnh phòng",
        FileCategory.Contract => "Hợp đồng",
        FileCategory.Invoice => "Hóa đơn",
        FileCategory.IdentityDocument => "Giấy tờ tùy thân",
        FileCategory.IncidentPhoto => "Ảnh sự cố",
        FileCategory.PaymentProof => "Minh chứng thanh toán",
        _ => "Tệp",
    };

    /// <summary>Đổi số byte sang MB để hiển thị trong thông báo "tối đa {X} MB".</summary>
    public static string DescribeMegabytes(long bytes) =>
        (bytes / 1024d / 1024d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    // Đuôi chỉ gồm chữ, số; chặn ".php.", "..jpg", ký tự lạ trong tên.
    private static bool IsSafeExtension(string extension)
    {
        if (extension.Length < 2 || extension.Length > 6 || extension[0] != '.')
        {
            return false;
        }

        foreach (var c in extension.AsSpan(1))
        {
            if (!char.IsAsciiLetterOrDigit(c))
            {
                return false;
            }
        }

        return true;
    }
}
