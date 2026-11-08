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
