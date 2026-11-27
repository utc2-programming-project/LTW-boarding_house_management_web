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

    // O-TK-04: Khách thuê chưa được chốt nhóm Dịch vụ.
    public bool CanRun(PropertyCaller caller) => !caller.IsKhachThue;

    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var catalogs = db.ServiceCatalogs.AsNoTracking().Where(x => !x.IsDeleted);

        if (!c.Caller.IsAdmin)
        {
            // PropertyScope lọc đúng nhà của chủ trọ hoặc nhà quản lý được phân công.
            var propertyIds = PropertyScope
                .Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), c.Caller)
                .Select(x => x.Id);
            catalogs = catalogs.Where(x => x.PropertyId == null || propertyIds.Contains(x.PropertyId.Value));
        }

        // D01: lọc tên dịch vụ phía DB. Tên hiển thị là bản ghi giá mới nhất, nhưng để tập DB
        // không hẹp hơn khớp C#, chấp nhận khớp bất kỳ bản ghi tên nào (tập cha) rồi ranking lọc lại.
        var tokens = SearchDbFilter.Tokens(c.NormalizedQuery);
        var matchingIds = await (
            from price in db.ServicePriceHistories.AsNoTracking()
            join catalog in catalogs on price.ServiceCatalogId equals catalog.Id
            select new { catalog.Id, price.Name })
            .WhereMatchesAnyToken(tokens, x => x.Name)
            .Select(x => x.Id)
            .Distinct()
            .ToListAsync(ct);

        var rows = await catalogs
            .Where(x => matchingIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                Name = x.PriceHistory
                    .OrderByDescending(p => p.EffectiveDate)
                    .Select(p => p.Name)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var hits = rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => new { Item = x, Score = SearchRanking.Score(x.Name, c.NormalizedQuery) })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Item.Name)
            .Skip(c.Offset)
            .Take(c.Limit + 1)
            .ToArray();

        return new(
            GroupKey,
            Icon,
            hits.Take(c.Limit)
                .Select(x => new SearchHitDto(GroupKey, Icon, x.Item.Name!, null, AppLinks.ServicePrices(x.Item.Id)))
                .ToArray(),
            hits.Length > c.Limit);
    }
}
