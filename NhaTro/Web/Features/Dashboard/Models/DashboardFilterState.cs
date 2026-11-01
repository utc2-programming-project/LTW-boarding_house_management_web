using Web.Infrastructure.Time;

namespace Web.Features.Dashboard.Models;

public enum DashboardPeriod { Month, Quarter, Year }
public enum DashboardLoadState { Loading, Loaded, Denied, Error }

public sealed class DashboardFilterState
{
    public int? PropertyId { get; set; }
    public DashboardPeriod Period { get; set; }
}

public sealed record DashboardPeriodRange(DateOnly Start, DateOnly End)
{
    public static DashboardPeriodRange Resolve(DashboardPeriod period, TimeProvider time)
    {
        var today = time.TodayVietnam();
        var month = period switch
        {
            DashboardPeriod.Year => 1,
            DashboardPeriod.Quarter => (today.Month - 1) / 3 * 3 + 1,
            _ => today.Month,
        };
        var start = new DateOnly(today.Year, month, 1);
        var months = period switch { DashboardPeriod.Year => 12, DashboardPeriod.Quarter => 3, _ => 1 };
        return new(start, start.AddMonths(months).AddDays(-1));
    }
}
