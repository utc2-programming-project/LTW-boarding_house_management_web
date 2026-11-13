namespace Web.Configuration.Options;

/// <summary>
/// Cấu hình nhật ký thao tác dạng file (VHT-104). Bind từ section "Audit".
/// Để trống <see cref="Directory"/> thì dùng thư mục con <c>audit</c> trong thư mục log hiện có.
/// </summary>
public sealed class AuditOptions
{
    public const string SectionName = "Audit";

    /// <summary>Thư mục gốc chứa file nhật ký; hỗ trợ biến môi trường và đường dẫn tương đối.</summary>
    public string Directory { get; set; } = string.Empty;

    /// <summary>Số ngày giữ log; dòng cũ hơn bị bỏ khi đọc và bị job dọn xóa.</summary>
    public int RetentionDays { get; set; } = 3;

    /// <summary>Chu kỳ job dọn log (giờ).</summary>
    public int CleanupIntervalHours { get; set; } = 6;
}
