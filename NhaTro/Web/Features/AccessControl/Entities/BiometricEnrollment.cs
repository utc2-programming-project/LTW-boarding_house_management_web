using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Entities;

/// <summary>
/// Đăng ký sinh trắc/passkey. Không lưu ảnh hay vector thô; chỉ lưu tham chiếu
/// đã mã hoá hoặc public key của passkey.
/// </summary>
public sealed class BiometricEnrollment
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    public int? TenantId { get; set; }

    public int? GatePassId { get; set; }

    public BiometricMethod Method { get; set; }

    /// <summary>Tham chiếu template đã mã hoá ở kho riêng (Face/Fingerprint).</summary>
    public string? TemplateRef { get; set; }

    /// <summary>Public key của WebAuthn/passkey; sinh trắc không rời thiết bị.</summary>
    public string? PublicKey { get; set; }

    public DateTimeOffset ConsentAt { get; set; }

    public string EnrolledByUserId { get; set; } = string.Empty;

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Pending;

    public string? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokeReason { get; set; }

    public DateTimeOffset? RetentionUntil { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
