using Web.Features.Reports.DTOs;
namespace NhaTro.UnitTests.Features.Reports;
public sealed class IncidentReportFilterTests
{
    [Fact] public void Normalize_UsesDefaultPageSize() { var filter=new IncidentReportFilter{PageSize=0}; filter.Normalize(); Assert.Equal(20,filter.PageSize); }
}
