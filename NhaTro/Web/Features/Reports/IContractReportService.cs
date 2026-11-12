using Web.Features.Properties;
using Web.Features.Reports.DTOs;
namespace Web.Features.Reports;
public interface IContractReportService { Task<ContractReportResult> GetAsync(ContractReportFilter filter, PropertyCaller caller, CancellationToken ct = default); }
