using System.Globalization;
using Microsoft.Extensions.Time.Testing;
using Web.Common.Utils;
using Web.Infrastructure.Time;

namespace NhaTro.UnitTests.Common;

public class MoneyFormatterTests
{
    [Theory]
    [InlineData(1500000, "1.500.000 ₫")]
    [InlineData(0, "0 ₫")]
    [InlineData(999, "999 ₫")]
    [InlineData(-2500000, "-2.500.000 ₫")]
    public void Format_dung_dinh_dang(decimal amount, string expected) =>
        Assert.Equal(expected, MoneyFormatter.Format(amount));

    [Theory]
    [InlineData(1234.5, 1235)]
    [InlineData(1234.4, 1234)]
    [InlineData(0.5, 1)]
    [InlineData(-0.5, -1)]
    public void RoundVnd_lam_tron_ra_xa_so_khong(decimal input, decimal expected) =>
        Assert.Equal(expected, MoneyFormatter.RoundVnd(input));

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public void Format_khong_doi_khi_doi_ngon_ngu(string culture)
    {
        var oldCulture = CultureInfo.CurrentCulture;
        var oldUi = CultureInfo.CurrentUICulture;
        try
        {
            var c = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentCulture = c;
            CultureInfo.CurrentUICulture = c;
            Assert.Equal("1.500.000 ₫", MoneyFormatter.Format(1_500_000m));
        }
        finally
        {
            CultureInfo.CurrentCulture = oldCulture;
            CultureInfo.CurrentUICulture = oldUi;
        }
    }

    [Fact]
    public void Format_du_lieu_trong_tra_ve_gach() =>
        Assert.Equal("—", MoneyFormatter.Format((decimal?)null));

    [Theory]
    [InlineData("1.500.000", 1500000)]
    [InlineData("1500000", 1500000)]
    [InlineData(" 1.500.000 ₫ ", 1500000)]
    [InlineData("2.000.000 vnd", 2000000)]
    public void TryParse_hop_le(string text, decimal expected)
    {
        Assert.True(MoneyFormatter.TryParse(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.500,50")]
    public void TryParse_khong_hop_le(string? text) =>
        Assert.False(MoneyFormatter.TryParse(text, out _));
}

public class DateAndTimeTests
{
    [Fact]
    public void TodayVietnam_qua_ngay_dung_moc_17h_UTC()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 10, 16, 59, 59, TimeSpan.Zero));
        Assert.Equal(new DateOnly(2026, 10, 10), time.TodayVietnam());

        time.Advance(TimeSpan.FromSeconds(1)); // 17:00:00 UTC = 00:00 ngày 11 giờ VN
        Assert.Equal(new DateOnly(2026, 10, 11), time.TodayVietnam());
    }

    [Fact]
    public void FormatDateTime_doi_UTC_sang_gio_VN_va_qua_ngay()
    {
        var utc = new DateTime(2026, 10, 10, 17, 30, 0, DateTimeKind.Utc);
        Assert.Equal("11/10/2026 00:30", DateFormatter.FormatDateTime(utc));
    }

    [Fact]
    public void ToVietnam_coi_Unspecified_la_UTC()
    {
        var fromDb = new DateTime(2026, 10, 10, 17, 30, 0, DateTimeKind.Unspecified);
        Assert.Equal(new DateTime(2026, 10, 11, 0, 30, 0), VietnamTime.ToVietnam(fromDb));
    }

    [Fact]
    public void ToVietnam_tu_choi_Local() =>
        Assert.Throws<ArgumentException>(() =>
            VietnamTime.ToVietnam(new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Local)));

    [Fact]
    public void DayRangeUtc_la_khoang_nua_mo_17h_den_17h()
    {
        var (start, end) = VietnamTime.DayRangeUtc(new DateOnly(2026, 10, 11));
        Assert.Equal(new DateTime(2026, 10, 10, 17, 0, 0, DateTimeKind.Utc), start);
        Assert.Equal(new DateTime(2026, 10, 11, 17, 0, 0, DateTimeKind.Utc), end);
    }

    [Fact]
    public void Format_ngay_hop_dong_khong_bi_doi_mui_gio()
    {
        Assert.Equal("01/11/2026", DateFormatter.Format(new DateOnly(2026, 11, 1)));
        Assert.Equal("11/2026", DateFormatter.FormatMonth(new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Du_lieu_trong_tra_ve_gach()
    {
        Assert.Equal("—", DateFormatter.Format(null));
        Assert.Equal("—", DateFormatter.FormatDateTime(null));
    }
}