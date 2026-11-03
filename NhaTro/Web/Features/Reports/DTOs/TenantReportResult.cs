using Web.Common.Models;

namespace Web.Features.Reports.DTOs;

/// <summary>
/// Kết quả mục báo cáo Khách thuê P8: KPI + trang danh sách cùng phạm vi/bộ lọc.
/// </summary>
public sealed class TenantReportResult
{
    public TenantReportKpis Kpis { get; init; } = new();

    public PagedResult<TenantReportItem> Tenants { get; init; } = new();
}
