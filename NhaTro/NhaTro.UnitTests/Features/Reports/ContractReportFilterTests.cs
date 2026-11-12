using Web.Features.Reports.DTOs;
namespace NhaTro.UnitTests.Features.Reports;
public sealed class ContractReportFilterTests
{
    [Fact] public void Normalize_ClampsAndTrims() { var filter=new ContractReportFilter{Page=0,PageSize=101,Keyword="  HD-01  "}; filter.Normalize(); Assert.Equal(1,filter.Page); Assert.Equal(100,filter.PageSize); Assert.Equal("HD-01",filter.Keyword); }
    [Fact] public void Normalize_BlankKeywordIsNull() { var filter=new ContractReportFilter{Keyword=" "}; filter.Normalize(); Assert.Null(filter.Keyword); }
}
