using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Entities;

/// <summary>
/// Link/QR mời khách vãng lai tự điền phiếu. Token chỉ lưu SHA-256, dùng một lần,
/// gắn đúng một nhà + một phòng và người tạo (khách thuê/chủ trọ/quản lý).
/// </summary>
public sealed class GatePassInvite
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    public int RoomId { get; set; }

    public int? TenantId { get; set; }

    /// <summary>Trỏ tới <c>Contract.ContractId</c> (Guid) khi người tạo là khách thuê.</summary>
    public Guid? ContractId { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public GateIssuerRole IssuerRole { get; set; }

    /// <summary>SHA-256 hex của token trong link; không lưu token thô.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public int? UsedGatePassId { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedByUserId { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
