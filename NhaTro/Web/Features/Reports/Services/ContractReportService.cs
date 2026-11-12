using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Infrastructure.Time;

namespace Web.Features.Reports.Services;

public sealed class ContractReportService(IDbContextFactory<ApplicationDbContext> factory, IManagerTogglePolicy toggles) : IContractReportService
{
    private const string ViewReportsCode = "SW-15";
    private const string ContractSectionCode = "SW-15c";
    private const string ForbiddenMessage = "Khong duoc xem bao cao hop dong.";

    public async Task<ContractReportResult> GetAsync(ContractReportFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsManager) throw new UnauthorizedAccessException(ForbiddenMessage);
        if (caller.IsQuanLy)
            foreach (var code in new[] { ViewReportsCode, ContractSectionCode })
                if (await toggles.EvaluateAsync(code, caller.Role, ct) != RoomChangeDecision.Allowed)
                    throw new UnauthorizedAccessException(ForbiddenMessage);
        filter.Normalize();
        if (filter.From > filter.To) return new ContractReportResult { Contracts = new PagedResult<ContractReportItem> { Page = filter.Page, PageSize = filter.PageSize } };
        await using var db = await factory.CreateDbContextAsync(ct);
        var properties = PropertyScope.Apply(db.Properties.IgnoreQueryFilters().AsNoTracking(), db.PropertyManagers.AsNoTracking(), caller);
        var q = from c in db.Contracts.AsNoTracking() join r in db.Rooms.IgnoreQueryFilters().AsNoTracking() on c.RoomId equals r.Id join p in properties on r.PropertyId equals p.Id select new { c, r, p };
        if (filter.PropertyId is { } pid) q = q.Where(x => x.p.Id == pid);
        if (filter.Status is { } status) q = q.Where(x => x.c.Status == status);
        if (filter.Keyword is { } key) q = q.Where(x => x.c.ContractNumber.Contains(key) || x.r.Code.Contains(key) || x.p.Name.Contains(key) || db.ContractMembers.Any(member => member.ContractId == x.c.ContractId && db.Tenants.Any(tenant => tenant.Id == member.TenantId && tenant.FullName.Contains(key))));
        if (filter.From is { } from) { var start = from.ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.c.EndDate >= start); }
        if (filter.To is { } to) { var end = to.ToDateTime(TimeOnly.MaxValue); q = q.Where(x => x.c.StartDate <= end); }
        var counts = await q.GroupBy(x => x.c.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync(ct);
        var total = counts.Sum(x => x.Count); int C(ContractStatus s) => counts.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
        var rows = await q.OrderBy(x => x.p.Name).ThenBy(x => x.r.Code).ThenBy(x => x.c.StartDate).ThenBy(x => x.c.ContractId).Skip((int)Math.Min((long)(filter.Page - 1) * filter.PageSize, int.MaxValue)).Take(filter.PageSize).ToListAsync(ct);
        var ids = rows.Select(x => x.c.ContractId).ToList(); var members = ids.Count == 0 ? new Dictionary<Guid, int>() : await db.ContractMembers.AsNoTracking().Where(x => ids.Contains(x.ContractId)).GroupBy(x => x.ContractId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var today = DateOnly.FromDateTime(VietnamTime.ToVietnam(DateTime.UtcNow));
        var activeFrom = filter.From?.ToDateTime(TimeOnly.MinValue);
        var activeTo = filter.To?.ToDateTime(TimeOnly.MaxValue);
        return new ContractReportResult { Kpis = new ContractReportKpis { Total = total, Approved = C(ContractStatus.Approved), Pending = C(ContractStatus.Pending), Rejected = C(ContractStatus.Rejected), Liquidated = C(ContractStatus.Liquidated) }, Contracts = new PagedResult<ContractReportItem> { Page = filter.Page, PageSize = filter.PageSize, TotalCount = total, Items = rows.Select(x => new ContractReportItem { ContractId=x.c.ContractId, ContractNumber=x.c.ContractNumber, RoomId=x.r.Id, RoomCode=x.r.Code, PropertyId=x.p.Id, PropertyName=x.p.Name, StartDate=DateOnly.FromDateTime(x.c.StartDate), EndDate=DateOnly.FromDateTime(x.c.EndDate), Status=x.c.Status, RentAmount=x.c.SnapshotPrice, DepositAmount=x.c.DepositAmount, MemberCount=members.GetValueOrDefault(x.c.ContractId), IsActiveInPeriod=filter.From is null && filter.To is null ? ContractLifecycleOperations.IsActionable(x.c.Status, x.c.StartDate, x.c.EndDate, today) : x.c.Status == ContractStatus.Approved && (activeFrom is null || x.c.EndDate >= activeFrom) && (activeTo is null || x.c.StartDate <= activeTo) }).ToList() } };
    }
}
