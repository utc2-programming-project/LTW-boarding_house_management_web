using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.DTOs;

public sealed record IncidentTrashItemDto(
    int Id,
    int RoomId,
    string PropertyName,
    string RoomCode,
    string Title,
    IncidentCategory Category,
    IncidentStatus Status,
    DateTime CreatedAt,
    DateTime? DeletedAt,
    string? DeletedBy);
