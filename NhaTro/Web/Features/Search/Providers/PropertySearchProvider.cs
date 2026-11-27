using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class PropertySearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "properties";
    public string Icon => "building";
    public int Order => 2;

    // O-TK-04: Khách thuê chưa được chốt nhóm Nhà trọ.
    public bool CanRun(PropertyCaller caller) => !caller.IsKhachThue;

    public async Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var properties = context.Caller.IsAdmin
            ? db.Properties.AsNoTracking().Where(x => !x.IsDeleted)
            : PropertyScope.Apply(
                db.Properties.AsNoTracking().Where(x => !x.IsDeleted),
                db.PropertyManagers.AsNoTracking(),
                context.Caller);
        var tokens = SearchDbFilter.Tokens(context.NormalizedQuery);
        var rows = await properties
            .WhereMatches(tokens, x => x.Name, x => x.Address)
            .Select(x => new { x.Id, x.Name, x.Address })
            .ToListAsync(ct);
        var matches = rows
            .Select(x => new
            {
                Item = x,
                Score = SearchRanking.Best(context.NormalizedQuery, x.Name, x.Address),
            })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Item.Name, StringComparer.CurrentCulture)
            .ThenBy(x => x.Item.Id)
            .Skip(context.Offset)
            .Take(context.Limit + 1)
            .ToArray();

        var hits = matches.Take(context.Limit)
            .Select(x => new SearchHitDto(GroupKey, Icon, x.Item.Name, x.Item.Address, AppLinks.Property(x.Item.Id)))
            .ToArray();

        return new(GroupKey, Icon, hits, matches.Length > context.Limit);
    }
}
