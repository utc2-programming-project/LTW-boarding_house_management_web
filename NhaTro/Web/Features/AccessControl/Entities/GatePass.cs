using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Entities;

/// <summary>
/// Phiếu ra vào: cư dân, khách của cư dân, hoặc người phục vụ định kỳ.
/// Chỉ lưu hash token; token thô trả về đúng một lần khi tạo.
/// </summary>
public sealed class GatePass
{
    public int Id { get; set; }

    public int PropertyId { get; set; }

    public int? RoomId { get; set; }

    public int? TenantId { get; set; }

    /// <summary>Trỏ tới <c>Contract.ContractId</c> (Guid).</summary>
    public Guid? ContractId { get; set; }

    public GatePassType PassType { get; set; }

    public GateStayType StayType { get; set; }

    /// <summary>SHA-256 hex của token QR; không lưu token thô.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>SHA-256 hex của mã 6 số dự phòng; null nếu không phát hành.</summary>
    public string? CodeHash { get; set; }

    public string VisitorName { get; set; } = string.Empty;

    public string? VisitorPhone { get; set; }

    public string? VisitorIdNumber { get; set; }

    public DateOnly? VisitorDob { get; set; }

    public string? StayReason { get; set; }

    public string? VisitorPhotoRef { get; set; }

    public string? Note { get; set; }

    public GatePassStatus Status { get; set; } = GatePassStatus.Pending;

    public GateMethod AllowedMethods { get; set; } = GateMethod.Qr | GateMethod.Code;

    public int MaxUses { get; set; } = 1;

    public int UsedCount { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public GateIssuerRole IssuerRole { get; set; }

    public string IssuedByUserId { get; set; } = string.Empty;

    public string? ApprovedByUserId { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public bool AutoApproved { get; set; }

    public string? RejectedReason { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedReason { get; set; }

    public string? RevokedByUserId { get; set; }

    public int? StayReportId { get; set; }

    /// <summary>Khác null khi phiếu được tạo từ link khách tự điền.</summary>
    public int? GatePassInviteId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
