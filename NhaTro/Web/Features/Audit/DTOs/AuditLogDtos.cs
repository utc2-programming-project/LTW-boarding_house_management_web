namespace Web.Features.Audit.DTOs;

public sealed record AuditLogEntryDto(
    Guid Id,
    string Category,
    string Action,
    string? ActorUserId,
    string? ActorDisplayName,
    int? PropertyId,
    string? PropertyName,
    string? Metadata,
    DateTimeOffset OccurredAt);

public sealed record AuditLogQuery(
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? ActorUserId = null,
    string? Action = null,
    int? PropertyId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record AuditLogPageDto(
    IReadOnlyList<AuditLogEntryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record AuditFilterActorDto(string UserId, string DisplayName);

public sealed record AuditLogFilterOptionsDto(
    IReadOnlyList<AuditFilterActorDto> Actors,
    IReadOnlyList<string> Actions);
