using Web.Features.Search.DTOs;
namespace Web.Features.Search.Abstractions;
public interface ISearchProvider { string GroupKey { get; } string Icon { get; } int Order { get; } Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct); }
