namespace Web.Features.Audit.Entities;

/// <summary>
/// D16 — nhật ký thao tác tối giản: chỉ ghi ai / làm gì / lúc nào và phạm vi tòa nhà.
/// Không lưu mật khẩu, token, số CCCD hay nội dung giấy tờ; metadata chỉ là mô tả ngắn.
/// </summary>
public enum AuditLogCategory
{
    /// <summary>Sổ hệ thống/tài khoản — chỉ Admin đọc.</summary>
    Account = 0,

    /// <summary>Sổ nghiệp vụ theo tòa nhà — Chủ trọ đọc trong phạm vi của mình.</summary>
    Business = 1,
}

public sealed class AuditLogEntry
{
    public int Id { get; set; }

    public AuditLogCategory Category { get; set; }

    /// <summary>Mã hành động ổn định, ví dụ AccountLocked hoặc MeterReadingAdjusted.</summary>
    public string Action { get; set; } = string.Empty;

    public string? ActorUserId { get; set; }

    /// <summary>Bắt buộc với sổ nghiệp vụ; null với sổ hệ thống.</summary>
    public int? PropertyId { get; set; }

    /// <summary>Metadata đã lược thông tin nhạy cảm (không CCCD/mật khẩu/token).</summary>
    public string? Metadata { get; set; }

    public DateTimeOffset OccurredAt { get; set; }
}
