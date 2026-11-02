using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.Entities;

public sealed class MaintenanceIncidentHistory
{
    public long Id { get; set; }
    public int IncidentId { get; set; }
    public IncidentHistoryAction Action { get; set; }
    public IncidentStatus? FromStatus { get; set; }
    public IncidentStatus ToStatus { get; set; }
    public string ActorUserId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
