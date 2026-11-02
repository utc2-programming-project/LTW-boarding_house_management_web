using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.DTOs;

public sealed record IncidentHistoryDto(
    long Id,
    IncidentHistoryAction Action,
    IncidentStatus? FromStatus,
    IncidentStatus ToStatus,
    string? ActorUserId,
    string? Note,
    DateTime CreatedAt);

public sealed record IncidentDetailDto(
    int Id,
    int RoomId,
    int PropertyId,
    string PropertyName,
    string RoomCode,
    string ReportedByUserId,
    string Title,
    string Description,
    IncidentCategory Category,
    IncidentUrgency Urgency,
    IncidentStatus Status,
    string? AssigneeName,
    string? TechnicalNotes,
    decimal? RepairCost,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    byte[] RowVersion,
    IReadOnlyList<IncidentHistoryDto> History);
