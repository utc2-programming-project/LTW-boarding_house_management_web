namespace Web.Features.Audit.DTOs;

public sealed record AuditLogEntryDto(
    int Id,
    string Category,
    string Action,
    string? ActorUserId,
    int? PropertyId,
    string? Metadata,
    DateTimeOffset OccurredAt);

public sealed record AuditLogQuery(int? PropertyId = null, int Take = 100);
