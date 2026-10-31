using Web.Features.Reports.DTOs;

namespace NhaTro.UnitTests.Features.Reports;

public class RoomReportFilterTests
{
    [Fact]
    public void Mac_dinh_chi_loc_phong_dang_hoat_dong_va_trang_dau()
    {
        var filter = new RoomReportFilter();

        Assert.True(filter.IsActive);
        Assert.Equal(1, filter.Page);
        Assert.Equal(20, filter.PageSize);
        Assert.Null(filter.Keyword);
        Assert.Null(filter.PropertyId);
        Assert.Null(filter.Status);
    }

    [Fact]
    public void Normalize_dua_trang_va_kich_thuoc_ve_gioi_han()
    {
        var filter = new RoomReportFilter { Page = 0, PageSize = 500 };

        filter.Normalize();

        Assert.Equal(1, filter.Page);
        Assert.Equal(100, filter.PageSize);
    }

    [Fact]
    public void Normalize_kich_thuoc_am_ve_mac_dinh()
    {
        var filter = new RoomReportFilter { Page = -3, PageSize = 0 };

        filter.Normalize();

        Assert.Equal(1, filter.Page);
        Assert.Equal(20, filter.PageSize);
    }

    [Fact]
    public void Normalize_cat_khoang_trang_tu_khoa_va_de_null_khi_rong()
    {
        var filter = new RoomReportFilter { Keyword = "  A10  " };
        filter.Normalize();
        Assert.Equal("A10", filter.Keyword);

        filter.Keyword = "   ";
        filter.Normalize();
        Assert.Null(filter.Keyword);
    }

    [Fact]
    public void Normalize_giu_nguyen_gia_tri_hop_le()
    {
        var filter = new RoomReportFilter { Page = 2, PageSize = 50, Keyword = "A1" };

        filter.Normalize();

        Assert.Equal(2, filter.Page);
        Assert.Equal(50, filter.PageSize);
        Assert.Equal("A1", filter.Keyword);
    }
}
