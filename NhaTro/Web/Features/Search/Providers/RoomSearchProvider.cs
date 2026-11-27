using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Rooms;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class RoomSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "rooms";
    public string Icon => "door";
    public int Order => 1;

    public async Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct)
    {
        if (!context.Caller.IsManager) return new(GroupKey, Icon, [], false);
        await using var db = await factory.CreateDbContextAsync(ct);
        var rooms = RoomScope.Apply(db.Rooms.AsNoTracking().Where(x => !x.IsDeleted), db.Properties.AsNoTracking().Where(x => !x.IsDeleted), db.PropertyManagers.AsNoTracking(), context.Caller);
        var rows = await (from room in rooms join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id select new { room.Id, room.Code, Property = property.Name }).ToListAsync(ct);
        var matches = rows.Select(x => new { Item = x, Score = SearchRanking.Best(context.NormalizedQuery, x.Code, x.Property) }).Where(x => x.Score < int.MaxValue).OrderBy(x => x.Score).ThenBy(x => x.Item.Code, StringComparer.CurrentCulture).ThenBy(x => x.Item.Id).Skip(context.Offset).Take(context.Limit + 1).ToArray();
        return new(GroupKey, Icon, matches.Take(context.Limit).Select(x => new SearchHitDto(GroupKey, Icon, x.Item.Code, x.Item.Property, AppLinks.Room(x.Item.Id))).ToArray(), matches.Length > context.Limit);
    }
}
