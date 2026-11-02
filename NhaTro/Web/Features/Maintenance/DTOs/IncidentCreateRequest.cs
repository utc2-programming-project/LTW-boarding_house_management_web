using System.ComponentModel.DataAnnotations;
using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.DTOs;

public sealed record IncidentCreateRequest
{
    public int RoomId { get; init; }

    [Required, StringLength(200, MinimumLength = 5)]
    public string Title { get; init; } = string.Empty;

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; init; } = string.Empty;

    public IncidentCategory Category { get; init; }

    public IncidentUrgency Urgency { get; init; }
}
