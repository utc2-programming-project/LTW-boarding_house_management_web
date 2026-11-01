using Web.Features.Dashboard;
using Web.Features.Dashboard.Models;
using Microsoft.Extensions.Time.Testing;

namespace NhaTro.UnitTests.Features.Dashboard;

public sealed class DashboardFormatTests
{
    [Theory]
    [InlineData(5, 8, 2, "83,3 %")]
    [InlineData(0, 0, 0, "—")]
    [InlineData(1, 1, 2, "—")]
    [InlineData(0, 5, 1, "0,0 %")]
    public void Occupancy_excludes_maintenance(int rented, int total, int maintenance, string expected) =>
        Assert.Equal(expected, DashboardFormat.FormatOccupancy(DashboardFormat.OccupancyPercent(rented, total, maintenance)));

    [Fact]
    public void Reporting_period_uses_Vietnam_calendar()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 3, 31, 18, 0, 0, TimeSpan.Zero));
        var range = DashboardPeriodRange.Resolve(DashboardPeriod.Quarter, clock);
        Assert.Equal(new DateOnly(2026, 4, 1), range.Start);
        Assert.Equal(new DateOnly(2026, 6, 30), range.End);
    }
}
