using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Web.Common.Queries;
using Web.Features.Contracts.Enums;
using Web.Features.Dashboard.Models;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Services;
using Web.Features.Tenants;
using Web.Infrastructure.Time;

namespace Web.Features.Dashboard.Services;

public sealed record DashboardPropertyOption(int Id, string Name, bool IsActive);

public sealed class DashboardPageService(
    P2CallerProvider callers, IPropertyService properties,
    IRoomReportService rooms, ITenantReportService tenants, RevenueReportService revenue,
    IIncidentReportService incidents, IContractReportService contracts,
    TimeProvider time, IMemoryCache? cache = null)
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
            var options = await properties.ListOptionsAsync(caller, ct);
            return (IReadOnlyList<DashboardPropertyOption>)options
                .Select(p => new DashboardPropertyOption(p.Id, p.Name, p.IsActive))
                .ToList();
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

    /// <summary>Xu hướng lấp đầy 6 tháng gần nhất, cùng phạm vi với báo cáo phòng.</summary>
    public async Task<OccupancyReportResult> GetOccupancyAsync(int? propertyId, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await CacheAsync($"occupancy:{caller.UserId}:{caller.Role}:{propertyId}",
            () => rooms.GetOccupancyAsync(new OccupancyFilter { PropertyId = propertyId }, caller, ct), ct);
    }

    /// <summary>Công nợ theo tuổi nợ. Chỉ Chủ trọ; nguồn giới hạn 100 hoá đơn đầu.</summary>
    public async Task<DebtAgingDto> GetDebtAgingAsync(int? propertyId, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await CacheAsync($"aging:{caller.UserId}:{caller.Role}:{propertyId}", async () =>
        {
            var report = await revenue.GetAsync(new RevenueReportFilter(null, null, propertyId)
            {
                OutstandingOnly = true,
                PageSize = 100,
            }, caller, ct);
            var today = time.TodayVietnam();
            decimal under = 0, between = 0, over = 0;
            foreach (var line in report.DebtLines)
            {
                if (line.Outstanding <= 0) continue;
                var days = line.DueDate is { } due ? today.DayNumber - due.DayNumber : 0;
                if (days <= 30) under += line.Outstanding;
                else if (days <= 60) between += line.Outstanding;
                else over += line.Outstanding;
            }
            return new DebtAgingDto(under, between, over);
        }, ct);
    }

    /// <summary>Số hợp đồng Approved hết hạn trong 12 tháng tới, nhóm theo tháng.</summary>
    public async Task<IReadOnlyList<ContractExpiryMonthDto>> GetContractExpiryAsync(
        int? propertyId, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await CacheAsync($"contract-expiry:{caller.UserId}:{caller.Role}:{propertyId}", async () =>
        {
            var today = time.TodayVietnam();
            var result = await contracts.GetAsync(new ContractReportFilter
            {
                From = today,
                To = today.AddMonths(12),
                PropertyId = propertyId,
                Status = ContractStatus.Approved,
                Page = 1,
                PageSize = 100,
            }, caller, ct);
            var lastMonth = today.AddMonths(12);
            return (IReadOnlyList<ContractExpiryMonthDto>)result.Contracts.Items
                .Where(item => item.EndDate >= today && item.EndDate <= lastMonth)
                .GroupBy(item => new { item.EndDate.Year, item.EndDate.Month })
                .Select(group => new ContractExpiryMonthDto(
                    new DateOnly(group.Key.Year, group.Key.Month, 1).ToString("MM/yyyy", CultureInfo.InvariantCulture),
                    group.Count(),
                    group.Key.Year * 12 + group.Key.Month))
                .OrderBy(item => item.SortKey)
                .ToList();
        }, ct);
    }

    private async Task<T> CacheAsync<T>(string key, Func<Task<T>> load, CancellationToken ct)
        where T : class
    {
        ct.ThrowIfCancellationRequested();
        key = $"{key}:v{Volatile.Read(ref _cacheVersion)}";
        if (cache is null)
        {
            return await load();
        }

        // ListCache chống stampede theo key: nhiều card mở cùng lúc chỉ chạy factory một lần.
        return await ListCache.GetOrCreateAsync<T>(cache, key, CacheLifetime,
            async _ => (T?)await load(), ct) ?? await load();
    }
}
