using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Vehicles.Services;

/// <summary>
/// Thống kê xe cho trang Tổng quan (SPEC §10): tổng xe Active theo loại, tỷ lệ lấp đầy khu đỗ
/// và doanh thu phí gửi xe của tháng hiện tại.
/// </summary>
public sealed record VehicleDashboardKpis(
    int ActiveTotal,
    int ElectricMotorbike,
    int ManualMotorbike,
    int Scooter,
    int ZoneCapacity,
    int ZoneOccupied,
    decimal MonthlyParkingRevenue);

public interface IVehicleDashboardService
{
    Task<VehicleDashboardKpis> GetKpisAsync(int? propertyId, PropertyCaller caller, CancellationToken ct = default);
}

public sealed class VehicleDashboardService(ApplicationDbContext db, TimeProvider clock) : IVehicleDashboardService
{
    public async Task<VehicleDashboardKpis> GetKpisAsync(
        int? propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var scopedProperties = VehicleScope.ApplyProperties(db, caller);
        List<int> propertyIds;
        if (propertyId is { } id)
        {
            if (!await scopedProperties.AnyAsync(p => p.Id == id, ct))
            {
                throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
            }

            propertyIds = [id];
        }
        else
        {
            propertyIds = await scopedProperties.Select(p => p.Id).ToListAsync(ct);
        }

        if (propertyIds.Count == 0)
        {
            return new VehicleDashboardKpis(0, 0, 0, 0, 0, 0, 0m);
        }

        var activeByType = await db.Vehicles.AsNoTracking()
            .Where(v => v.Status == VehicleStatus.Active && propertyIds.Contains(v.PropertyId))
            .GroupBy(v => v.VehicleType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(VehicleType type) => activeByType.FirstOrDefault(x => x.Type == type)?.Count ?? 0;

        var electric = CountOf(VehicleType.ElectricMotorbike);
        var manual = CountOf(VehicleType.ManualMotorbike);
        var scooter = CountOf(VehicleType.Scooter);

        var zoneCapacity = await db.ParkingZones.AsNoTracking()
            .Where(z => z.IsActive && propertyIds.Contains(z.PropertyId))
            .SumAsync(z => (int?)z.Capacity, ct) ?? 0;

        var activeZoneIds = db.ParkingZones.AsNoTracking()
            .Where(z => z.IsActive && propertyIds.Contains(z.PropertyId))
            .Select(z => z.Id);

        var zoneOccupied = await db.Vehicles.AsNoTracking()
            .CountAsync(v => v.Status == VehicleStatus.Active
                && v.ParkingZoneId != null
                && activeZoneIds.Contains(v.ParkingZoneId.Value), ct);

        var revenue = await (from item in db.InvoiceItems.AsNoTracking()
                             join invoice in db.Invoices.AsNoTracking() on item.InvoiceId equals invoice.Id
                             join contract in db.Contracts.AsNoTracking() on invoice.ContractId equals contract.ContractId
                             join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
                             where invoice.Status != InvoiceStatus.Draft
                                 && !invoice.IsDeleted
                                 && propertyIds.Contains(room.PropertyId)
                                 && (item.Description.StartsWith(VehicleRules.ParkingInvoiceLinePrefix)
                                     || item.Description.StartsWith(VehicleRules.ParkingInvoiceLinePrefixEn))
                                 && invoice.PeriodStart <= MonthEnd(Today)
                                 && invoice.PeriodEnd >= MonthStart(Today)
                             select item.Amount)
            .SumAsync(amount => (decimal?)amount, ct) ?? 0m;

        return new VehicleDashboardKpis(
            electric + manual + scooter,
            electric,
            manual,
            scooter,
            zoneCapacity,
            zoneOccupied,
            revenue);
    }

    private DateOnly Today => clock.TodayVietnam();

    private static DateOnly MonthStart(DateOnly day) => new(day.Year, day.Month, 1);

    private static DateOnly MonthEnd(DateOnly day) => MonthStart(day).AddMonths(1).AddDays(-1);
}