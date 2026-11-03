using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.DTOs;

public sealed class IncidentUpdateRequest
{
    public IncidentStatus Status { get; init; }
    public string? AssigneeName { get; init; }
    public string? TechnicalNotes { get; init; }
    public decimal? RepairCost { get; init; }
}
