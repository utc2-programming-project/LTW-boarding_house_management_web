namespace Web.Features.Invoices.Rules;

/// <summary>
/// Quy tắc kỳ hóa đơn P6: kỳ là một khoảng ngày thuần <c>[PeriodStart, PeriodEnd]</c>
/// do P5 chốt, không cộng trừ múi giờ. Kỳ tháng đủ dùng các helper tháng bên dưới
/// (ngày 1 đến ngày cuối tháng); kỳ lẻ giữ nguyên hai mốc P5 gửi, task này không tự tính tỷ lệ ngày.
/// </summary>
public static class InvoicePeriodRules
{
    /// <summary>Đưa một ngày bất kỳ về ngày 1 của tháng đó.</summary>
    public static DateOnly Normalize(DateOnly date) => new(date.Year, date.Month, 1);

    /// <summary>Kỳ tháng đã đúng quy ước ngày 1 hay chưa.</summary>
    public static bool IsNormalized(DateOnly period) => period.Day == 1;

    /// <summary>Ngày đầu kỳ tháng (bao gồm).</summary>
    public static DateOnly Start(DateOnly period) => Normalize(period);

    /// <summary>Ngày đầu kỳ tháng kế tiếp (không bao gồm) — dùng cho khoảng nửa mở.</summary>
    public static DateOnly EndExclusive(DateOnly period) => Normalize(period).AddMonths(1);

    /// <summary>Ngày cuối cùng của kỳ tháng (bao gồm).</summary>
    public static DateOnly EndInclusive(DateOnly period) => EndExclusive(period).AddDays(-1);

    /// <summary>Một ngày có thuộc kỳ tháng hay không; so sánh theo ngày thuần, không theo giờ.</summary>
    public static bool Contains(DateOnly period, DateOnly date) =>
        date >= Start(period) && date < EndExclusive(period);

    /// <summary>Kỳ tháng kế tiếp, đã chuẩn hóa về ngày 1.</summary>
    public static DateOnly Next(DateOnly period) => Normalize(period).AddMonths(1);

    /// <summary>Kỳ tháng trước, đã chuẩn hóa về ngày 1.</summary>
    public static DateOnly Previous(DateOnly period) => Normalize(period).AddMonths(-1);

    /// <summary>Khoảng kỳ hợp lệ khi ngày đầu không sau ngày cuối; hai đầu đều được tính.</summary>
    public static bool IsValidRange(DateOnly start, DateOnly end) => start <= end;

    /// <summary>Một ngày có thuộc khoảng kỳ <c>[start, end]</c> hay không.</summary>
    public static bool Contains(DateOnly start, DateOnly end, DateOnly date) =>
        IsValidRange(start, end) && date >= start && date <= end;

    /// <summary>Số ngày của kỳ, tính cả ngày đầu và ngày cuối.</summary>
    public static int DayCount(DateOnly start, DateOnly end) =>
        IsValidRange(start, end) ? end.DayNumber - start.DayNumber + 1 : 0;
}
