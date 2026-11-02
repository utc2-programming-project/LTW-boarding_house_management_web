using System.Globalization;

namespace Web.Features.Dashboard;

public static class DashboardFormat
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    public static decimal? OccupancyPercent(int rented, int total, int maintenance) =>
        total - maintenance <= 0 ? null : Math.Round(100m * rented / (total - maintenance), 1, MidpointRounding.AwayFromZero);
    public static string FormatOccupancy(decimal? value) => value is null ? "—" : value.Value.ToString("F1", Vietnamese) + " %";
    public static string FormatNumber(int? value) => value?.ToString("N0", Vietnamese) ?? "—";
}
