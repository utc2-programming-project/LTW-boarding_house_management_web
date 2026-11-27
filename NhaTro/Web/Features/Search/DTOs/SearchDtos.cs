namespace Web.Features.Search.DTOs;
public sealed record SearchHitDto(string GroupKey, string Icon, string Title, string? Subtitle, string Href);
public sealed record SearchGroupResult(string GroupKey, string Icon, IReadOnlyList<SearchHitDto> Hits, bool HasMore);
public sealed record SearchResponse(IReadOnlyList<SearchGroupResult> Groups);
