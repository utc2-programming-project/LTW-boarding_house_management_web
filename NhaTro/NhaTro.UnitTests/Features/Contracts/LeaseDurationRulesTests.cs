using Web.Features.Contracts.Rules;

namespace NhaTro.UnitTests.Features.Contracts;

public class LeaseDurationRulesTests
{
    [Theory]
    [InlineData(2026, 1, 1, 2026, 12, 31, 12)] // cả năm: hiệu tháng thuần túy chỉ ra 11
    [InlineData(2026, 1, 31, 2026, 3, 1, 1)]    // 31/01–01/03 là hơn 1 tháng, không phải 2
    [InlineData(2026, 1, 1, 2026, 1, 31, 1)]
    [InlineData(2026, 1, 1, 2026, 6, 30, 6)]
    [InlineData(2026, 2, 1, 2027, 1, 31, 12)]
    [InlineData(2026, 1, 15, 2026, 2, 14, 1)]
    public void FullMonths_tinh_theo_ngay(int sy, int sm, int sd, int ey, int em, int ed, int expected) =>
        Assert.Equal(expected, LeaseDurationRules.FullMonths(new DateTime(sy, sm, sd), new DateTime(ey, em, ed)));

    [Theory]
    [InlineData(2026, 1, 1, 2026, 1, 15)]  // dưới một tháng
    [InlineData(2026, 1, 15, 2026, 2, 13)] // thiếu một ngày so với tròn tháng
    [InlineData(2026, 5, 10, 2026, 3, 10)] // ngày kết thúc trước ngày bắt đầu
    public void FullMonths_tra_ve_null_khi_khong_du_mot_thang(int sy, int sm, int sd, int ey, int em, int ed) =>
        Assert.Null(LeaseDurationRules.FullMonths(new DateTime(sy, sm, sd), new DateTime(ey, em, ed)));

    [Fact]
    public void FullMonths_bo_qua_thoi_gian_trong_ngay()
    {
        var start = new DateTime(2026, 1, 1, 8, 0, 0);
        var end = new DateTime(2026, 12, 31, 23, 59, 59);
        Assert.Equal(12, LeaseDurationRules.FullMonths(start, end));
    }
}
