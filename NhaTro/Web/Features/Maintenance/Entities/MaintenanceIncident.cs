using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.Entities;

public sealed class MaintenanceIncident
{
    public int Id { get; set; }
    public int RoomId { get; set; }
    public string ReportedByUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentCategory Category { get; set; }
    public IncidentUrgency Urgency { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.New;
    public string? AssigneeName { get; set; }
    public string? TechnicalNotes { get; set; }
    public decimal? RepairCost { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public ICollection<MaintenanceIncidentHistory> History { get; set; } = new List<MaintenanceIncidentHistory>();
}
