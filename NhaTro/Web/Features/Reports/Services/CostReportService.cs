using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Features.Utilities;

namespace Web.Features.Reports.Services;

/// <summary>Nguon chi phi dien nuoc P8; chi doc UtilityReading va snapshot theo ReadingId.</summary>
public sealed class CostReportService(IDbContextFactory<ApplicationDbContext> factory, IManagerTogglePolicy toggles) : ICostReportService
{
    public async Task<CostReportResult> GetAsync(CostReportFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter); ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsManager) throw new UnauthorizedAccessException("Khong duoc xem bao cao chi phi.");
        if (caller.IsQuanLy) foreach (var code in new[] { "SW-15", "SW-15d" }) if (await toggles.EvaluateAsync(code, caller.Role, ct) != RoomChangeDecision.Allowed) throw new UnauthorizedAccessException("Khong duoc xem bao cao chi phi.");
        filter.Normalize(); if (filter.From > filter.To) return new CostReportResult { Readings = new PagedResult<CostReportItem> { Page=filter.Page, PageSize=filter.PageSize } };
        await using var db = await factory.CreateDbContextAsync(ct);
        var properties = PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), caller).Where(property => !property.IsDeleted);
        var query = from reading in db.UtilityReadings.AsNoTracking() join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on reading.RoomId equals room.Id join property in properties on room.PropertyId equals property.Id select new { reading, room, property };
        if (filter.PropertyId is { } propertyId) query = query.Where(x => x.property.Id == propertyId);
        if (filter.RoomId is { } roomId) query = query.Where(x => x.room.Id == roomId);
        if (filter.From is { } from) query = query.Where(x => x.reading.PeriodEnd >= from);
        if (filter.To is { } to) query = query.Where(x => x.reading.PeriodStart <= to);

        var totals = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                ReadingCount = group.Count(),
                ElectricUsage = group.Sum(x => x.reading.ElectricEnd > x.reading.ElectricStart
                    ? x.reading.ElectricEnd - x.reading.ElectricStart
                    : 0m),
                WaterUsage = group.Sum(x => x.reading.WaterEnd > x.reading.WaterStart
                    ? x.reading.WaterEnd - x.reading.WaterStart
                    : 0m),
            })
            .SingleOrDefaultAsync(ct);

        var amounts = await (
                from snapshot in db.UtilityBillingPeriodSnapshots.AsNoTracking()
                join row in query on snapshot.BillingPeriodId equals row.reading.Id
                where snapshot.Type == TariffType.Electric || snapshot.Type == TariffType.Water
                group snapshot by snapshot.Type
                into grouped
                select new { Type = grouped.Key, Amount = grouped.Sum(item => item.Total) })
            .ToListAsync(ct);

        var missingSnapshotCount = await query.CountAsync(row =>
            (row.reading.ElectricEnd > row.reading.ElectricStart
                && !db.UtilityBillingPeriodSnapshots.Any(snapshot =>
                    snapshot.BillingPeriodId == row.reading.Id && snapshot.Type == TariffType.Electric))
            || (row.reading.WaterEnd > row.reading.WaterStart
                && !db.UtilityBillingPeriodSnapshots.Any(snapshot =>
                    snapshot.BillingPeriodId == row.reading.Id && snapshot.Type == TariffType.Water)), ct);

        var skip = (int)Math.Min((long)(filter.Page - 1) * filter.PageSize, int.MaxValue);
        var pageRows = await query
            .OrderBy(row => row.property.Name)
            .ThenBy(row => row.room.Code)
            .ThenBy(row => row.reading.PeriodStart)
            .ThenBy(row => row.reading.Id)
            .Skip(skip)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        var pageIds = pageRows.Select(row => row.reading.Id).ToArray();
        var pageRoomIds = pageRows.Select(row => row.room.Id).Distinct().ToArray();
        var snapshots = await db.UtilityBillingPeriodSnapshots.AsNoTracking()
            .Where(snapshot => pageIds.Contains(snapshot.BillingPeriodId))
            .ToListAsync(ct);
        var snapshotMap = snapshots.ToDictionary(snapshot => (snapshot.BillingPeriodId, snapshot.Type), snapshot => snapshot.Total);
        var contracts = await db.Contracts.AsNoTracking()
            .Where(contract => contract.Status == Web.Features.Contracts.Enums.ContractStatus.Approved
                && pageRoomIds.Contains(contract.RoomId))
            .ToListAsync(ct);

        var items = pageRows.Select(row =>
        {
            var electricUsage = Math.Max(0m, row.reading.ElectricEnd - row.reading.ElectricStart);
            var waterUsage = Math.Max(0m, row.reading.WaterEnd - row.reading.WaterStart);
            var electricAmount = snapshotMap.GetValueOrDefault((row.reading.Id, TariffType.Electric));
            var waterAmount = snapshotMap.GetValueOrDefault((row.reading.Id, TariffType.Water));
            var contract = contracts
                .Where(item => item.RoomId == row.room.Id
                    && item.StartDate <= row.reading.PeriodEnd.ToDateTime(TimeOnly.MaxValue)
                    && item.EndDate >= row.reading.PeriodStart.ToDateTime(TimeOnly.MinValue))
                .OrderByDescending(item => item.StartDate)
                .ThenByDescending(item => item.ContractId)
                .FirstOrDefault();
            return new CostReportItem
            {
                ReadingId = row.reading.Id,
                RoomId = row.room.Id,
                RoomCode = row.room.Code,
                PropertyId = row.property.Id,
                PropertyName = row.property.Name,
                ContractId = contract?.ContractId,
                PeriodStart = row.reading.PeriodStart,
                PeriodEnd = row.reading.PeriodEnd,
                ElectricUsage = electricUsage,
                WaterUsage = waterUsage,
                ElectricAmount = electricAmount,
                WaterAmount = waterAmount,
                TotalAmount = electricAmount + waterAmount,
                HasSnapshot = (electricUsage == 0 || snapshotMap.ContainsKey((row.reading.Id, TariffType.Electric)))
                    && (waterUsage == 0 || snapshotMap.ContainsKey((row.reading.Id, TariffType.Water))),
            };
        }).ToList();

        var electricTotal = amounts.FirstOrDefault(item => item.Type == TariffType.Electric)?.Amount ?? 0m;
        var waterTotal = amounts.FirstOrDefault(item => item.Type == TariffType.Water)?.Amount ?? 0m;
        var kpis = new CostReportKpis
        {
            ReadingCount = totals?.ReadingCount ?? 0,
            ElectricUsage = totals?.ElectricUsage ?? 0m,
            WaterUsage = totals?.WaterUsage ?? 0m,
            ElectricAmount = electricTotal,
            WaterAmount = waterTotal,
            TotalAmount = electricTotal + waterTotal,
            MissingSnapshotCount = missingSnapshotCount,
        };

        return new CostReportResult
        {
            Kpis = kpis,
            Readings = new PagedResult<CostReportItem>
            {
                Items = items,
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = kpis.ReadingCount,
            },
        };
    }
}
