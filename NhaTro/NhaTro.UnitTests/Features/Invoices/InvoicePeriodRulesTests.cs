using Web.Features.Invoices.Rules;

namespace NhaTro.UnitTests.Features.Invoices;

public class InvoicePeriodRulesTests
{
    [Fact]
    public void Ky_thang_chuan_hoa_ngay_1_va_khoang_nua_mo()
    {
        Assert.Equal(new DateOnly(2026, 10, 1), InvoicePeriodRules.Normalize(new DateOnly(2026, 10, 21)));
        Assert.True(InvoicePeriodRules.IsNormalized(new DateOnly(2026, 10, 1)));
        Assert.False(InvoicePeriodRules.IsNormalized(new DateOnly(2026, 10, 21)));

        var period = new DateOnly(2026, 10, 1);
        Assert.Equal(new DateOnly(2026, 10, 1), InvoicePeriodRules.Start(period));
        Assert.Equal(new DateOnly(2026, 11, 1), InvoicePeriodRules.EndExclusive(period));
        Assert.Equal(new DateOnly(2026, 10, 31), InvoicePeriodRules.EndInclusive(period));
    }

    [Fact]
    public void Kiem_tra_ngay_thuoc_ky_thang_theo_ngay_thuan()
    {
        var period = new DateOnly(2026, 10, 1);

        Assert.True(InvoicePeriodRules.Contains(period, new DateOnly(2026, 10, 1)));
        Assert.True(InvoicePeriodRules.Contains(period, new DateOnly(2026, 10, 31)));
        Assert.False(InvoicePeriodRules.Contains(period, new DateOnly(2026, 9, 30)));
        Assert.False(InvoicePeriodRules.Contains(period, new DateOnly(2026, 11, 1)));
    }

    [Fact]
    public void Thang_hai_nam_nhuan_va_nam_thuong_tinh_dung_ngay_cuoi()
    {
        Assert.Equal(
            new DateOnly(2028, 2, 29),
            InvoicePeriodRules.EndInclusive(new DateOnly(2028, 2, 1)));
        Assert.Equal(
            new DateOnly(2027, 2, 28),
            InvoicePeriodRules.EndInclusive(new DateOnly(2027, 2, 1)));
    }

    [Fact]
    public void Ky_truoc_va_ky_sau_qua_nam_dung()
    {
        Assert.Equal(
            new DateOnly(2027, 1, 1),
            InvoicePeriodRules.Next(new DateOnly(2026, 12, 1)));
        Assert.Equal(
            new DateOnly(2025, 12, 1),
            InvoicePeriodRules.Previous(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Khoang_ky_le_hop_le_va_tinh_ca_hai_dau()
    {
        var start = new DateOnly(2026, 10, 15);
        var end = new DateOnly(2026, 11, 14);

        Assert.True(InvoicePeriodRules.IsValidRange(start, end));
        Assert.True(InvoicePeriodRules.Contains(start, end, start));
        Assert.True(InvoicePeriodRules.Contains(start, end, end));
        Assert.False(InvoicePeriodRules.Contains(start, end, start.AddDays(-1)));
        Assert.False(InvoicePeriodRules.Contains(start, end, end.AddDays(1)));
        Assert.Equal(31, InvoicePeriodRules.DayCount(start, end));
    }

    [Fact]
    public void Khoang_ky_nguoc_khong_hop_le()
    {
        var start = new DateOnly(2026, 11, 1);
        var end = new DateOnly(2026, 10, 31);

        Assert.False(InvoicePeriodRules.IsValidRange(start, end));
        Assert.False(InvoicePeriodRules.Contains(start, end, new DateOnly(2026, 10, 20)));
        Assert.Equal(0, InvoicePeriodRules.DayCount(start, end));
    }
}
