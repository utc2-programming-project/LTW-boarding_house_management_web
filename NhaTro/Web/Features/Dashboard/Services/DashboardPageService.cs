using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;
using Web.Features.Tenants;

namespace Web.Features.Dashboard.Services;

public sealed record DashboardPropertyOption(int Id, string Name, bool IsActive);

public sealed class DashboardPageService(P2CallerProvider callers, IPropertyService properties,
    IRoomReportService rooms, ITenantReportService tenants, TimeProvider time)
{
    // Policy công tắc đang dùng DbContext scoped: tránh hai nguồn đọc đồng thời cùng context.
    private readonly SemaphoreSlim reportGate = new(1, 1);
    public async Task<string> GetRoleAsync() => (await callers.GetCallerAsync()).Role;

    public async Task<IReadOnlyList<DashboardPropertyOption>> ListPropertiesAsync(CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        var result = new List<DashboardPropertyOption>();
        for (var page = 1; ; page++)
        {
            var batch = await properties.ListAsync(new PropertyFilter { Page = page, PageSize = 100 }, caller, ct);
            result.AddRange(batch.Items.Select(p => new DashboardPropertyOption(p.Id, p.Name, p.IsActive)));
            if (page * 100L >= batch.TotalCount) break;
        }
        return result;
    }

    public async Task<RoomReportKpis> GetRoomKpisAsync(int? propertyId, CancellationToken ct = default)
    {
        await reportGate.WaitAsync(ct);
        try
        {
            return (await rooms.GetAsync(new RoomReportFilter { PropertyId = propertyId, IsActive = true, PageSize = 1 },
                await callers.GetCallerAsync(), ct)).Kpis;
        }
        finally { reportGate.Release(); }
    }

    public async Task<TenantReportKpis> GetTenantKpisAsync(CancellationToken ct = default)
    {
        await reportGate.WaitAsync(ct);
        try
        {
            var caller = await callers.GetCallerAsync();
            return await tenants.GetAsync(new TenantCaller(caller.UserId, caller.Role), ct);
        }
        finally { reportGate.Release(); }
    }

    public DateTime GetLastRefreshedAtUtc() => time.GetUtcNow().UtcDateTime;
}
