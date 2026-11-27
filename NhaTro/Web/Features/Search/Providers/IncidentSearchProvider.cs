using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;
using Web.Infrastructure.Time;

namespace Web.Features.Search.Providers;

public sealed class IncidentSearchProvider(
    IDbContextFactory<ApplicationDbContext> factory,
    TimeProvider clock) : ISearchProvider
{
    public string GroupKey => "incidents";
    public string Icon => "wrench";
    public int Order => 6;

    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);

        IQueryable<Web.Features.Maintenance.Entities.MaintenanceIncident> scoped;
        if (c.Caller.IsAdmin)
        {
            scoped = db.MaintenanceIncidents.AsNoTracking().Where(incident => !incident.IsDeleted);
        }
        else if (c.Caller.IsManager || c.Caller.IsChuTro)
        {
            // PropertyScope.Apply lọc đúng theo chủ trọ hoặc quản lý được phân công.
            var properties = PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), c.Caller);
            var roomIds = from room in db.Rooms.AsNoTracking()
                          join property in properties on room.PropertyId equals property.Id
                          select room.Id;
            scoped = db.MaintenanceIncidents.AsNoTracking().Where(incident => roomIds.Contains(incident.RoomId));
        }
        else if (c.Caller.IsKhachThue)
        {
            // A08: chỉ sự cố của hợp đồng đã duyệt, còn hiệu lực, phòng còn hoạt động
            // — giống IncidentService.InScopeIncidents.
            var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
            var ownTenantIds = db.Tenants.AsNoTracking()
                .Where(tenant => tenant.UserId == c.Caller.UserId)
                .Select(tenant => tenant.Id);
            var roomIds = from member in db.ContractMembers.AsNoTracking()
                          join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                          join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                          where ownTenantIds.Contains(member.TenantId)
                              && contract.Status == ContractStatus.Approved
                              && contract.StartDate <= today && contract.EndDate >= today
                              && room.IsActive && !room.IsDeleted
                          select room.Id;
            scoped = db.MaintenanceIncidents.AsNoTracking().Where(incident => roomIds.Contains(incident.RoomId));
        }
        else
        {
            return new(GroupKey, Icon, [], false);
        }

        var tokens = SearchDbFilter.Tokens(c.NormalizedQuery);
        var rows = await (
            from incident in scoped
            join room in db.Rooms.AsNoTracking() on incident.RoomId equals room.Id
            where !incident.IsDeleted
            select new { incident.Id, incident.Title, room.Code })
            .WhereMatches(tokens, x => x.Title, x => x.Code)
            .ToListAsync(ct);

        var hits = rows
            .Select(x => new { x, Score = SearchRanking.Best(c.NormalizedQuery, x.Title, x.Code) })
            .Where(x => x.Score < int.MaxValue)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.x.Title)
            .Skip(c.Offset)
            .Take(c.Limit + 1)
            .ToArray();

        return new(
            GroupKey,
            Icon,
            hits.Take(c.Limit).Select(x => new SearchHitDto(GroupKey, Icon, x.x.Title, x.x.Code, AppLinks.Incident(x.x.Id))).ToArray(),
            hits.Length > c.Limit);
    }
}
