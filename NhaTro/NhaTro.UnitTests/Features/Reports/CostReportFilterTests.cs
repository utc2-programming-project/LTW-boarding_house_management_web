using Web.Features.Reports.DTOs;
namespace NhaTro.UnitTests.Features.Reports;
public sealed class CostReportFilterTests
{
    [Fact] public void Normalize_ClampsPagination() { var filter=new CostReportFilter{Page=0,PageSize=101}; filter.Normalize(); Assert.Equal(1,filter.Page); Assert.Equal(100,filter.PageSize); }
}
