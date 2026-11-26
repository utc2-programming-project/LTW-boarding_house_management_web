using Web.Infrastructure.Time;

namespace Web.Common.Queries;

/// <summary>
/// Quy ước khoảng nửa mở [startUtc, endUtcExclusive) theo ngày Việt Nam,
/// thay cho TimeZoneInfo.Local hoặc so sánh ngày trần trên cột UTC.
/// </summary>
public static class VietnamDateRange
{
    public const int DefaultReportMonths = 12;

    public static (DateTime StartUtc, DateTime EndUtcExclusive) Day(DateOnly day) =>
        VietnamTime.DayRangeUtc(day);

    public static (DateTime StartUtc, DateTime EndUtcExclusive) Month(DateOnly month)
    {
        var start = new DateOnly(month.Year, month.Month, 1);
        return (VietnamTime.DayRangeUtc(start).StartUtc, VietnamTime.DayRangeUtc(start.AddMonths(1)).StartUtc);
    }

    /// <summary>Kỳ báo cáo: dùng From/To nếu có, ngược lại lấy <paramref name="defaultMonths"/> tháng gần nhất.</summary>
    public static (DateOnly From, DateOnly To) ReportPeriod(DateOnly today, DateOnly? from, DateOnly? to, int defaultMonths = DefaultReportMonths)
    {
        var end = to ?? today;
        var start = from ?? new DateOnly(end.Year, end.Month, 1).AddMonths(-(Math.Max(1, defaultMonths) - 1));
        return (start, end);
    }
}
