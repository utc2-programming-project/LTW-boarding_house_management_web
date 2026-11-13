using Web.Features.Dashboard.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Reports.DTOs;

namespace Web.Features.Reports.Services;

/// <summary>
/// VHT-140 - điểm gọi chung cho các nguồn báo cáo P8. Mỗi lần tải chỉ lấy một kết quả
/// nguồn cho KPI, bảng, biểu đồ và nội dung thay thế; không tự viết lại truy vấn nguồn.
/// </summary>
public sealed class ReportsPageService(
    P2CallerProvider callers,
    IPropertyService properties,
    IRoomReportService rooms,
    ITenantReportService tenants,
    RevenueReportService revenue,
    IContractReportService contracts,
    ICostReportService costs,
    IIncidentReportService incidents,
    ReportExportService export)
{
    public async Task<string> GetRoleAsync() => (await callers.GetCallerAsync()).Role;

    public async Task<bool> IsOwnerAsync() =>
        string.Equals(await GetRoleAsync(), Web.Features.Accounts.Roles.ChuTro, StringComparison.Ordinal);

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

    public async Task<RoomReportResult> GetRoomReportAsync(RoomReportFilter filter, CancellationToken ct = default) =>
        await rooms.GetAsync(filter, await callers.GetCallerAsync(), ct);

    public async Task<OccupancyReportResult> GetOccupancyAsync(OccupancyFilter filter, CancellationToken ct = default) =>
        await rooms.GetOccupancyAsync(filter, await callers.GetCallerAsync(), ct);

    public async Task<TenantReportResult> GetTenantReportAsync(TenantReportFilter filter, CancellationToken ct = default)
    {
        var caller = await callers.GetCallerAsync();
        return await tenants.GetAsync(filter, new Web.Features.Tenants.TenantCaller(caller.UserId, caller.Role), ct);
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(RevenueReportFilter filter, CancellationToken ct = default) =>
        await revenue.GetAsync(filter, await callers.GetCallerAsync(), ct);

    public async Task<ContractReportResult> GetContractReportAsync(ContractReportFilter filter, CancellationToken ct = default) =>
        await contracts.GetAsync(filter, await callers.GetCallerAsync(), ct);

    public async Task<CostReportResult> GetCostReportAsync(CostReportFilter filter, CancellationToken ct = default) =>
        await costs.GetAsync(filter, await callers.GetCallerAsync(), ct);

    public async Task<IncidentReportResult> GetIncidentReportAsync(IncidentReportFilter filter, CancellationToken ct = default) =>
        await incidents.GetAsync(filter, await callers.GetCallerAsync(), ct);

    public async Task<byte[]> ExportRevenueExcelAsync(RevenueReportFilter filter, CancellationToken ct = default) =>
        await export.ExportRevenueExcelAsync(await callers.GetCallerAsync(), filter, ct);
}