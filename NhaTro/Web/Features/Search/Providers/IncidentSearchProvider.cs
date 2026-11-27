using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class IncidentSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "incidents";
    public string Icon => "wrench";
    public int Order => 6;
    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        IQueryable<int> roomIds;
        if (c.Caller.IsManager)
        {
            var properties = PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), c.Caller);
            roomIds = from room in db.Rooms.AsNoTracking() join property in properties on room.PropertyId equals property.Id select room.Id;
        }
        else if (c.Caller.IsKhachThue)
            roomIds = from member in db.ContractMembers join tenant in db.Tenants on member.TenantId equals tenant.Id join contract in db.Contracts on member.ContractId equals contract.ContractId where tenant.UserId == c.Caller.UserId select contract.RoomId;
        else return new(GroupKey, Icon, [], false);
        var rows = await (from incident in db.MaintenanceIncidents.AsNoTracking() join room in db.Rooms.AsNoTracking() on incident.RoomId equals room.Id where roomIds.Contains(incident.RoomId) && !incident.IsDeleted select new { incident.Id, incident.Title, room.Code }).ToListAsync(ct);
        var hits = rows.Select(x => new { x, score = SearchRanking.Best(c.NormalizedQuery, x.Title, x.Code) }).Where(x => x.score < int.MaxValue).OrderBy(x => x.score).ThenBy(x => x.x.Title).Skip(c.Offset).Take(c.Limit + 1).ToArray();
        return new(GroupKey, Icon, hits.Take(c.Limit).Select(x => new SearchHitDto(GroupKey, Icon, x.x.Title, x.x.Code, AppLinks.Incident(x.x.Id))).ToArray(), hits.Length > c.Limit);
    }
}
