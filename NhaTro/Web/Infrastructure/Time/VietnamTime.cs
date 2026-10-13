namespace Web.Infrastructure.Time;

/// <summary>Quy ước: lưu UTC, hiển thị giờ Việt Nam (UTC+7, không có giờ mùa hè).</summary>
public static class VietnamTime
{
    private static readonly string[] ZoneIds = ["SE Asia Standard Time", "Asia/Ho_Chi_Minh"];

    public static TimeZoneInfo TimeZone { get; } = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in ZoneIds)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        // Dự phòng khi máy không có dữ liệu múi giờ (VN không có DST).
        return TimeZoneInfo.CreateCustomTimeZone("VN", TimeSpan.FromHours(7), "Vietnam", "Vietnam");
    }

    /// <summary>UTC -> giờ VN. Kind Unspecified (EF đọc từ SQL Server) coi là UTC; Local bị từ chối.</summary>
    public static DateTime ToVietnam(DateTime utc)
    {
        if (utc.Kind == DateTimeKind.Local)
            throw new ArgumentException("Cần giá trị UTC, không phải Local.", nameof(utc));
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), TimeZone);
    }

    /// <summary>Giờ VN (người dùng nhập) -> UTC để lưu.</summary>
    public static DateTime ToUtc(DateTime vietnam)
    {
        if (vietnam.Kind == DateTimeKind.Utc)
            throw new ArgumentException("Cần giá trị giờ VN, không phải UTC.", nameof(vietnam));
        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(vietnam, DateTimeKind.Unspecified), TimeZone);
    }

    /// <summary>Khoảng [bắt đầu, hết) theo UTC của một ngày lịch VN, dùng để lọc "trong ngày".</summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly day) =>
        (ToUtc(day.ToDateTime(TimeOnly.MinValue)), ToUtc(day.AddDays(1).ToDateTime(TimeOnly.MinValue)));
}