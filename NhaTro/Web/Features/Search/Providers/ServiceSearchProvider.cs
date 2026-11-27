using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class ServiceSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "services";
    public string Icon => "settings";
    public int Order => 7;
    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        if (!c.Caller.IsManager) return new(GroupKey, Icon, [], false);
        await using var db = await factory.CreateDbContextAsync(ct);
        var propertyIds = PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), c.Caller).Select(x => x.Id);
        var rows = await db.ServiceCatalogs.AsNoTracking().Where(x => !x.IsDeleted && (x.PropertyId == null || propertyIds.Contains(x.PropertyId.Value))).Select(x => new { x.Id, Name = x.PriceHistory.OrderByDescending(p => p.EffectiveDate).Select(p => p.Name).FirstOrDefault() }).ToListAsync(ct);
        var hits = rows.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => new { x, score = SearchRanking.Score(x.Name, c.NormalizedQuery) }).Where(x => x.score < int.MaxValue).OrderBy(x => x.score).ThenBy(x => x.x.Name).Skip(c.Offset).Take(c.Limit + 1).ToArray();
        return new(GroupKey, Icon, hits.Take(c.Limit).Select(x => new SearchHitDto(GroupKey, Icon, x.x.Name!, null, AppLinks.ServicePrices(x.x.Id))).ToArray(), hits.Length > c.Limit);
    }
}
