using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;
using Web.Features.Tenants;

namespace Web.Features.Search.Providers;

public sealed class TenantSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "tenants";
    public string Icon => "users";
    public int Order => 3;

    public async Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await TenantVisibility.Apply(db, context.Caller.UserId, context.Caller.Role)
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new { x.Id, x.FullName, x.Phone })
            .ToListAsync(ct);
        var matches = rows.Select(x => new { Item = x, Score = SearchRanking.Best(context.NormalizedQuery, x.FullName, x.Phone) })
            .Where(x => x.Score != int.MaxValue)
            .OrderBy(x => x.Score).ThenBy(x => x.Item.FullName, StringComparer.CurrentCulture).ThenBy(x => x.Item.Id)
            .Skip(context.Offset).Take(context.Limit + 1).ToArray();
        var href = context.Caller.Role == Roles.KhachThue ? "/tenant/profile" : null;
        return new SearchGroupResult(GroupKey, Icon, matches.Take(context.Limit)
            .Select(x => new SearchHitDto(GroupKey, Icon, x.Item.FullName, x.Item.Phone, href ?? AppLinks.Tenant(x.Item.Id))).ToArray(), matches.Length > context.Limit);
    }
}
