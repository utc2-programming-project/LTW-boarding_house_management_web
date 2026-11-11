namespace Web.Features.Tenants.Entities;

/// <summary>PLAN-024: khách đã nhận QR/lời mời cho một phòng, chờ lập hợp đồng.</summary>
public sealed class PendingRoomAssignment
{
    public int Id { get; set; }

    public int TenantId { get; set; }

    public int PropertyId { get; set; }

    public int RoomId { get; set; }

    public int InvitationId { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PendingRoomAssignmentStatus Status { get; set; } = PendingRoomAssignmentStatus.Pending;
}

public enum PendingRoomAssignmentStatus
{
    Pending = 0,
    Completed = 1,
    Cancelled = 2,
}
