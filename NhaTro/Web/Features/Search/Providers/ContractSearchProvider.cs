using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class ContractSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "contracts";
    public string Icon => "file";
    public int Order => 4;

    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.Contracts.AsNoTracking();
        if (c.Caller.IsKhachThue)
            query = from contract in query join member in db.ContractMembers on contract.ContractId equals member.ContractId join tenant in db.Tenants on member.TenantId equals tenant.Id where tenant.UserId == c.Caller.UserId && contract.Status != ContractStatus.Draft select contract;
        else if (c.Caller.IsChuTro)
            query = from contract in query join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id where property.OwnerId == c.Caller.UserId select contract;
        else if (c.Caller.IsQuanLy)
            query = from contract in query join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id join assignment in db.PropertyManagers on room.PropertyId equals assignment.PropertyId where assignment.ManagerUserId == c.Caller.UserId && assignment.RevokedAt == null select contract;
        else return new(GroupKey, Icon, [], false);
        var rows = await query.Select(x => new { x.ContractId, x.ContractNumber }).ToListAsync(ct);
        var hits = rows.Select(x => new { x, score = SearchRanking.Score(x.ContractNumber, c.NormalizedQuery) }).Where(x => x.score < int.MaxValue).OrderBy(x => x.score).ThenBy(x => x.x.ContractNumber).Skip(c.Offset).Take(c.Limit + 1).ToArray();
        return new(GroupKey, Icon, hits.Take(c.Limit).Select(x => new SearchHitDto(GroupKey, Icon, x.x.ContractNumber, null, AppLinks.Contract(x.x.ContractId))).ToArray(), hits.Length > c.Limit);
    }
}
