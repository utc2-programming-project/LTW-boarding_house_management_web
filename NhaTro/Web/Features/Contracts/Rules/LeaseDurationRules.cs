namespace Web.Features.Contracts.Rules;

/// <summary>
/// Thời hạn hợp đồng tính theo ngày thay vì chỉ theo tháng của lịch. EndDate là ngày cuối còn
/// hiệu lực, nên độ dài đã cho thuê được đo đến EndDate + 1 ngày; nếu chỉ hiệu hai tháng dương lịch
/// thì 01/01–31/12 bị tính non một tháng và 31/01–01/03 bị tính quá một tháng.
/// </summary>
public static class LeaseDurationRules
{
    /// <summary>Số tháng tròn đã thuê; null khi dưới một tháng hoặc ngày kết thúc trước ngày bắt đầu.</summary>
    public static int? FullMonths(DateTime start, DateTime inclusiveEnd)
    {
        var startDate = start.Date;
        var exclusiveEnd = inclusiveEnd.Date.AddDays(1);

        var months = (exclusiveEnd.Year - startDate.Year) * 12 + exclusiveEnd.Month - startDate.Month;
        if (exclusiveEnd.Day < startDate.Day)
        {
            months--;
        }

        return months > 0 ? months : null;
    }
}
