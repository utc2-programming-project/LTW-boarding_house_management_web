using Web.Features.Maintenance.Enums;

namespace Web.Features.Maintenance.DTOs;

public sealed record IncidentListQuery(
    string? Title = null,
    string? Room = null,
    IncidentCategory? Category = null,
    IncidentUrgency? Urgency = null,
    IncidentStatus? Status = null,
    DateOnly? CreatedOn = null,
    int Page = 1,
    int PageSize = 20,
    string? SortKey = null,
    bool SortDescending = false);

public sealed record IncidentListPage(
    IReadOnlyList<IncidentListItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int Total,
    int NewCount,
    int InProgressCount,
    int CompletedCount);
