using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.Entities;

/// <summary>
/// PLAN-006: lời mời một khách thuê tham gia hệ thống. Chỉ lưu hash của token;
/// token thô chỉ nằm trong liên kết gửi qua email.
/// </summary>
public class TenantInvitation
{
    public int Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    /// <summary>Chủ trọ sẽ sở hữu hồ sơ sau khi khách chấp nhận.</summary>
    public string OwnerId { get; set; } = string.Empty;

    /// <summary>Người gửi lời mời (Chủ trọ hoặc Quản lý).</summary>
    public string InvitedByUserId { get; set; } = string.Empty;

    /// <summary>PLAN-024: Email hoặc QR.</summary>
    public TenantInvitationChannel Channel { get; set; } = TenantInvitationChannel.Email;

    /// <summary>PLAN-024: khách mới tự đăng ký hoặc khách đã có tài khoản.</summary>
    public TenantInvitationKind Kind { get; set; } = TenantInvitationKind.NewAccount;

    /// <summary>PLAN-024: mốc hạn đã chọn (phút), server tính ExpiresAt.</summary>
    public int ExpiryPresetMinutes { get; set; } = 1440;

    /// <summary>Phạm vi QR (nullable nếu lời mời chỉ gắn chủ trọ).</summary>
    public int? PropertyId { get; set; }

    public int? RoomId { get; set; }

    /// <summary>Khách đã có tài khoản được mời (Kind = ExistingAccount).</summary>
    public string? TargetUserId { get; set; }

    public string? CreatedByUserId { get; set; }

    /// <summary>SHA-256 (Base64) của token trong liên kết mời.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime? AcceptedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    /// <summary>Hồ sơ khách được tạo khi lời mời được chấp nhận.</summary>
    public int? TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public byte[] RowVersion { get; set; } = [];
}
