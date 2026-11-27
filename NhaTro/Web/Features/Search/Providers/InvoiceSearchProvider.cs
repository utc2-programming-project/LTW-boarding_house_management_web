using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Invoices.Enums;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace Web.Features.Search.Providers;

public sealed class InvoiceSearchProvider(IDbContextFactory<ApplicationDbContext> factory) : ISearchProvider
{
    public string GroupKey => "invoices";
    public string Icon => "receipt";
    public int Order => 5;
    public async Task<SearchGroupResult> SearchAsync(SearchContext c, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        IQueryable<Guid> contractIds;
        if (c.Caller.IsKhachThue)
            contractIds = from member in db.ContractMembers join tenant in db.Tenants on member.TenantId equals tenant.Id where tenant.UserId == c.Caller.UserId select member.ContractId;
        else if (c.Caller.IsChuTro)
            contractIds = from contract in db.Contracts join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id where property.OwnerId == c.Caller.UserId select contract.ContractId;
        else if (c.Caller.IsQuanLy)
            contractIds = from contract in db.Contracts join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id join assignment in db.PropertyManagers on room.PropertyId equals assignment.PropertyId where assignment.ManagerUserId == c.Caller.UserId && assignment.RevokedAt == null select contract.ContractId;
        else return new(GroupKey, Icon, [], false);
        var rows = await db.Invoices.AsNoTracking().Where(x => !x.IsDeleted && contractIds.Contains(x.ContractId) && (!c.Caller.IsKhachThue || x.Status != InvoiceStatus.Draft)).Select(x => new { x.Id, x.PeriodStart }).ToListAsync(ct);
        var hits = rows.Select(x => new { x, title = $"HD{x.Id}", period = x.PeriodStart.ToString("MM/yyyy"), score = SearchRanking.Best(c.NormalizedQuery, $"HD{x.Id}", x.PeriodStart.ToString("MM/yyyy"), x.PeriodStart.ToString("yyyy-MM")) }).Where(x => x.score < int.MaxValue).OrderBy(x => x.score).ThenBy(x => x.x.Id).Skip(c.Offset).Take(c.Limit + 1).ToArray();
        return new(GroupKey, Icon, hits.Take(c.Limit).Select(x => new SearchHitDto(GroupKey, Icon, x.title, x.period, AppLinks.Invoice(x.x.Id, c.Caller.IsKhachThue ? "/my/invoices" : "/invoices"))).ToArray(), hits.Length > c.Limit);
    }
}
