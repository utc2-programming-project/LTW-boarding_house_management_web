using Web.Common.Utils;

namespace NhaTro.UnitTests.Common;

public class PaginationHelperTests
{
    [Theory]
    [InlineData(0, 10, 1)]
    [InlineData(1, 10, 1)]
    [InlineData(10, 10, 1)]
    [InlineData(11, 10, 2)]
    [InlineData(45, 10, 5)]
    public void TotalPages_lam_tron_len_va_toi_thieu_1(int total, int pageSize, int expected) =>
        Assert.Equal(expected, PaginationHelper.TotalPages(total, pageSize));

    [Fact]
    public void TotalPages_page_size_khong_hop_le_nem_loi() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PaginationHelper.TotalPages(10, 0));

    [Theory]
    [InlineData(0, 5, 1)]
    [InlineData(3, 5, 3)]
    [InlineData(9, 5, 5)]
    public void ClampPage_giu_trong_khoang_hop_le(int page, int totalPages, int expected) =>
        Assert.Equal(expected, PaginationHelper.ClampPage(page, totalPages));

    [Theory]
    [InlineData(1, 3, "1,2,3")]
    [InlineData(1, 10, "1,2,3,4,5")]
    [InlineData(5, 10, "3,4,5,6,7")]
    [InlineData(6, 10, "4,5,6,7,8")]
    [InlineData(10, 10, "6,7,8,9,10")]
    public void PageWindow_gom_toi_da_5_so_quanh_trang_hien_tai(int current, int total, string expected) =>
        Assert.Equal(expected, string.Join(",", PaginationHelper.PageWindow(current, total)));

    [Theory]
    [InlineData(1, 10, 45, 1, 10)]
    [InlineData(5, 10, 45, 41, 45)]
    [InlineData(1, 10, 0, 0, 0)]
    public void ItemRange_tinh_dung_bien_trang(int page, int pageSize, int total, int from, int to) =>
        Assert.Equal((from, to), PaginationHelper.ItemRange(page, pageSize, total));
}
