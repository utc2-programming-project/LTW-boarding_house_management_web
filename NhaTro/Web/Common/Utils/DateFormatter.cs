using System.Globalization;
using Web.Infrastructure.Time;

namespace Web.Common.Utils;

public static class DateFormatter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Ngày thuần (hợp đồng, hạn, kỳ hóa đơn): không đổi múi giờ.</summary>
    public static string Format(DateOnly? date, string empty = "—") =>
        date?.ToString("dd/MM/yyyy", Inv) ?? empty;

    public static string FormatMonth(DateOnly? date, string empty = "—") =>
        date?.ToString("MM/yyyy", Inv) ?? empty;

    /// <summary>Thời điểm thao tác: nhận UTC, hiển thị giờ VN.</summary>
    public static string FormatDateTime(DateTime? utc, string empty = "—") =>
        utc is null ? empty : VietnamTime.ToVietnam(utc.Value).ToString("dd/MM/yyyy HH:mm", Inv);
}