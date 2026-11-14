using Microsoft.Extensions.Caching.Memory;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Services;
using Web.Features.Tenants;

namespace Web.Features.Dashboard.Services;

public sealed record DashboardPropertyOption(int Id, string Name, bool IsActive);

public sealed class DashboardPageService(
    P2CallerProvider callers, IPropertyService properties,
    IRoomReportService rooms, ITenantReportService tenants, RevenueReportService revenue,
    IIncidentReportService incidents, TimeProvider time, IMemoryCache? cache = null)
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(45);
    private long _cacheVersion;

    /// <summary>Nút Làm mới chuyển sang thế hệ cache mới, bỏ qua toàn bộ cache cũ.</summary>
    public void InvalidateCache() => Interlocked.Increment(ref _cacheVersion);

    public async Task<string> GetRoleAsync() => (await callers.GetCallerAsync()).Role;

    public async Task<IReadOnlyList<DashboardPropertyOption>> ListPropertiesAsync(CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await CacheAsync($"properties:{caller.UserId}:{caller.Role}", async () =>
        {
            var result = new List<DashboardPropertyOption>();
            for (var page = 1; ; page++)
            {
                var batch = await properties.ListAsync(new PropertyFilter { Page = page, PageSize = 100 }, caller, ct);
                result.AddRange(batch.Items.Select(p => new DashboardPropertyOption(p.Id, p.Name, p.IsActive)));
                if (page * 100L >= batch.TotalCount) break;
            }
            return (IReadOnlyList<DashboardPropertyOption>)result;
        }, ct);
    }

    public async Task<RoomReportKpis> GetRoomKpisAsync(int? propertyId, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await CacheAsync($"rooms:{caller.UserId}:{caller.Role}:{propertyId}", async () =>
            (await rooms.GetAsync(new RoomReportFilter { PropertyId = propertyId, PageSize = 1 }, caller, ct)).Kpis, ct);
    }

    public async Task<RoomReportResult> GetRoomReportAsync(RoomReportFilter filter, CancellationToken ct = default)
        => await rooms.GetAsync(filter, await callers.GetCallerAsync(), ct);

    public async Task<TenantReportKpis> GetTenantKpisAsync(int? propertyId, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await CacheAsync($"tenants:{caller.UserId}:{caller.Role}:{propertyId}", async () =>
            (await tenants.GetAsync(
                new TenantReportFilter { PropertyId = propertyId, PageSize = 1 },
                new TenantCaller(caller.UserId, caller.Role), ct)).Kpis, ct);
    }

    public async Task<TenantReportResult> GetTenantReportAsync(TenantReportFilter filter, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await tenants.GetAsync(filter, new TenantCaller(caller.UserId, caller.Role), ct);
    }

    public DateTime GetLastRefreshedAtUtc() => time.GetUtcNow().UtcDateTime;

    /// <summary>VHT-140: doanh thu/công nợ theo kỳ và nhà trọ; service nguồn tự chặn vai trò D5.</summary>
    public async Task<RevenueReportDto> GetRevenueReportAsync(RevenueReportFilter filter, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        var key = $"revenue:{caller.UserId}:{caller.Role}:{filter.From}:{filter.To}:{filter.PropertyId}:{filter.DateBasis}";
        return await CacheAsync(key, () => revenue.GetSummaryAsync(filter, caller, ct), ct);
    }

    public async Task<IncidentReportKpis> GetIncidentKpisAsync(
        DateOnly from, DateOnly to, int? propertyId, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await CacheAsync($"incidents:{caller.UserId}:{caller.Role}:{from}:{to}:{propertyId}", async () =>
            (await incidents.GetAsync(new IncidentReportFilter
            {
                From = from, To = to, PropertyId = propertyId, PageSize = 1,
            }, caller, ct)).Kpis, ct);
    }

    public async Task<IncidentReportResult> GetIncidentReportAsync(IncidentReportFilter filter, CancellationToken ct = default)
        => await incidents.GetAsync(filter, await callers.GetCallerAsync(), ct);

    private async Task<T> CacheAsync<T>(string key, Func<Task<T>> load, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        key = $"{key}:v{Volatile.Read(ref _cacheVersion)}";
        if (cache is not null
            && cache.TryGetValue(key, out T? cached)
            && cached is not null)
        {
            return cached;
        }

        var value = await load();
        cache?.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CacheLifetime,
        });
        return value;
    }
}
