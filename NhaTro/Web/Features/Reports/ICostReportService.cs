using Web.Features.Properties;
using Web.Features.Reports.DTOs;
namespace Web.Features.Reports;
public interface ICostReportService { Task<CostReportResult> GetAsync(CostReportFilter filter, PropertyCaller caller, CancellationToken ct = default); }
