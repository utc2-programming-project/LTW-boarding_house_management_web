using Web.Common.Utils;

namespace NhaTro.UnitTests.Common;

public class TextSearchNormalizerTests
{
    [Theory]
    [InlineData("CCCD mặt trước", "cccd mat truoc")]
    [InlineData("Tạm trú (CT07)", "tam tru (ct07)")]
    [InlineData("Đinh Văn A", "dinh van a")]
    [InlineData("  Hợp đồng ", "hop dong")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalize_bo_dau_va_lower(string? input, string expected) =>
        Assert.Equal(expected, TextSearchNormalizer.Normalize(input));

    [Theory]
    [InlineData("CCCD mặt trước", "cccd mat truoc", true)]
    [InlineData("CCCD mặt trước", "MAT TRUOC", true)]
    [InlineData("Hợp đồng thuê", "hop dong", true)]
    [InlineData("Hợp đồng thuê", "Tạm trú", false)]
    [InlineData("Tạm trú CT08", "", true)]
    public void Contains_khop_khi_ke_gõ_khong_dau(string source, string keyword, bool expected) =>
        Assert.Equal(expected, TextSearchNormalizer.Contains(source, keyword));

    [Fact]
    public void Contains_gon_dau_va_hoa_thuong()
    {
        Assert.True(TextSearchNormalizer.Contains("Ảnh CCCD mặt sau.JPG", "mat sau"));
        Assert.True(TextSearchNormalizer.Contains("cccd mat sau.jpg", "CCCD MAT SAU"));
        Assert.False(TextSearchNormalizer.Contains("Ảnh CCCD mặt sau.JPG", "ct07"));
    }
}
