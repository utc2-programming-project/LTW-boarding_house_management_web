namespace Web.Infrastructure.Time;

public static class TimeProviderExtensions
{
    public static DateTime UtcNow(this TimeProvider time) => time.GetUtcNow().UtcDateTime;

    /// <summary>Ngày hiện tại theo lịch Việt Nam (dùng cho hạn thanh toán, kỳ hóa đơn, hiệu lực hợp đồng).</summary>
    public static DateOnly TodayVietnam(this TimeProvider time) =>
        DateOnly.FromDateTime(VietnamTime.ToVietnam(time.UtcNow()));
}