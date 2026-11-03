using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.DTOs;

public sealed record IncidentCreatePayload(
    string Title, string Description, IncidentCategory Category, IncidentUrgency Urgency, int ExpectedPhotoCount = 0);

public sealed record IncidentResultPhotoPayload(int ExpectedPhotoCount);

public sealed record IncidentUpdatePayload(
    IncidentStatus Status, string? TechnicalNotes, string? AssigneeName, decimal? RepairCost);
