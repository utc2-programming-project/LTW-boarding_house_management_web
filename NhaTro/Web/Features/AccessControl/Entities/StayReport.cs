using Web.Features.AccessControl.Enums;

namespace Web.Features.AccessControl.Entities;

/// <summary>
/// Phiếu thông báo lưu trú cho khách ở qua đêm (nghĩa vụ pháp lý).
/// </summary>
public sealed class StayReport
{
    public int Id { get; set; }

    public int? GatePassId { get; set; }

    public int PropertyId { get; set; }

    public int? RoomId { get; set; }

    public Guid? ContractId { get; set; }

    public string GuestName { get; set; } = string.Empty;

    public DateOnly? GuestDob { get; set; }

    public string? GuestIdNumber { get; set; }

    public string? StayReason { get; set; }

    public DateTimeOffset StayFrom { get; set; }

    public DateTimeOffset StayTo { get; set; }

    public string AddressSnapshot { get; set; } = string.Empty;

    public StayReportStatus Status { get; set; } = StayReportStatus.Pending;

    public StayReportChannel? Channel { get; set; }

    public DateTimeOffset DueAt { get; set; }

    public DateTimeOffset? ReportedAt { get; set; }

    public string? ReportedByUserId { get; set; }

    public string? EvidenceRef { get; set; }

    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
