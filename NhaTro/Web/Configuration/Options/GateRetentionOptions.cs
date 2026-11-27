namespace Web.Configuration.Options;

/// <summary>
/// Hạn lưu dữ liệu vận hành cổng: nhật ký, phiếu vào cổng, liên kết khách tự điền.
/// Đặt <see cref="RetentionDays"/> = 0 để tắt job dọn.
/// </summary>
public sealed class GateRetentionOptions
{
    public const string SectionName = "GateRetention";

    /// <summary>Số ngày giữ dữ liệu cổng; mặc định 30 ngày.</summary>
    public int RetentionDays { get; set; } = 30;

    /// <summary>Chu kỳ job dọn, tính bằng giờ; áp trong khoảng 1–168.</summary>
    public int CleanupIntervalHours { get; set; } = 24;
}
