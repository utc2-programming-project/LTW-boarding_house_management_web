using Web.Features.Reports.DTOs;

namespace NhaTro.UnitTests.Features.Reports;

public class TenantReportFilterTests
{
    [Fact]
    public void Mac_dinh_la_tong_ho_so_va_trang_dau()
    {
        var filter = new TenantReportFilter();

        Assert.Null(filter.IsActive);
        Assert.Null(filter.HasActiveContract);
        Assert.Null(filter.HasAccount);
        Assert.Null(filter.PropertyId);
        Assert.Equal(1, filter.Page);
        Assert.Equal(20, filter.PageSize);
        Assert.Null(filter.Keyword);
    }

    [Fact]
    public void Normalize_dua_trang_va_kich_thuoc_ve_gioi_han()
    {
        var filter = new TenantReportFilter { Page = 0, PageSize = 500 };

        filter.Normalize();

        Assert.Equal(1, filter.Page);
        Assert.Equal(100, filter.PageSize);
    }

    [Fact]
    public void Normalize_kich_thuoc_am_ve_mac_dinh()
    {
        var filter = new TenantReportFilter { Page = -3, PageSize = 0 };

        filter.Normalize();

        Assert.Equal(1, filter.Page);
        Assert.Equal(20, filter.PageSize);
    }

    [Fact]
    public void Normalize_cat_khoang_trang_tu_khoa_va_de_null_khi_rong()
    {
        var filter = new TenantReportFilter { Keyword = "  An  " };
        filter.Normalize();
        Assert.Equal("An", filter.Keyword);

        filter.Keyword = "   ";
        filter.Normalize();
        Assert.Null(filter.Keyword);
    }

    [Fact]
    public void Normalize_giu_nguyen_gia_tri_hop_le()
    {
        var filter = new TenantReportFilter { Page = 2, PageSize = 50, Keyword = "A1", IsActive = true };

        filter.Normalize();

        Assert.Equal(2, filter.Page);
        Assert.Equal(50, filter.PageSize);
        Assert.Equal("A1", filter.Keyword);
        Assert.True(filter.IsActive);
    }
}
