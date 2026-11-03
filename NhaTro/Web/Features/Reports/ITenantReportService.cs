using Web.Features.Reports.DTOs;
using Web.Features.Tenants;

namespace Web.Features.Reports;

/// <summary>
/// Nguồn khách thuê cho báo cáo P8 (VHT-46). Mọi hàm tự kiểm vai trò/phạm vi/công tắc.
/// </summary>
public interface ITenantReportService
{
    /// <summary>KPI + danh sách trên cùng bộ lọc, để số đếm luôn khớp danh sách.</summary>
    Task<TenantReportResult> GetAsync(
        TenantReportFilter filter, TenantCaller caller, CancellationToken ct = default);

    /// <summary>
    /// KPI mặc định cho dashboard trong lúc chờ VHT-128 ghép bộ lọc nhà trọ.
    /// Tương đương <c>GetAsync(new TenantReportFilter(), caller)</c>.
    /// </summary>
    Task<TenantReportKpis> GetAsync(TenantCaller caller, CancellationToken ct = default);
}
