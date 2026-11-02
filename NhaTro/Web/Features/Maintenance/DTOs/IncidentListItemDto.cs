using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.DTOs;

public sealed record IncidentListItemDto(
    int Id,
    int RoomId,
    int PropertyId,
    string PropertyName,
    string RoomCode,
    string Title,
    IncidentCategory Category,
    IncidentUrgency Urgency,
    IncidentStatus Status,
    DateTime CreatedAt,
    string ReportedByUserId);
