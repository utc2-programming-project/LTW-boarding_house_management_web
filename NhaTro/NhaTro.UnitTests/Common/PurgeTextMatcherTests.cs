using Web.Common.SoftDelete;

namespace NhaTro.UnitTests.Common;

public class PurgeTextMatcherTests
{
    [Theory]
    [InlineData("Phòng 101", "Phòng 101")]
    [InlineData("  Phòng 101  ", "Phòng 101")]
    [InlineData("phòng 101", "Phòng 101")]
    public void Khop_sau_khi_bo_khoang_trang_va_khong_phan_biet_hoa_thuong(string input, string expected)
    {
        Assert.True(PurgeTextMatcher.Matches(input, expected));
    }

    [Theory]
    [InlineData("Phong 101", "Phòng 101")]
    [InlineData("", "Phòng 101")]
    [InlineData(null, "Phòng 101")]
    public void Khong_khop_khi_sai_noi_dung_hoac_rong(string? input, string expected)
    {
        Assert.False(PurgeTextMatcher.Matches(input, expected));
    }

    [Theory]
    [InlineData("XÓA TẤT CẢ", "")]
    [InlineData("XÓA TẤT CẢ", null)]
    public void Chuoi_ky_vong_rong_thi_luon_false(string input, string? expected)
    {
        Assert.False(PurgeTextMatcher.Matches(input, expected));
    }
}
