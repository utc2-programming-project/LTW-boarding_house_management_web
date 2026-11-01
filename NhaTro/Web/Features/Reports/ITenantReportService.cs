using Web.Features.Reports.DTOs;
using Web.Features.Tenants;

namespace Web.Features.Reports;

public interface ITenantReportService
{
    Task<TenantReportKpis> GetAsync(TenantCaller caller, CancellationToken ct = default);
}
