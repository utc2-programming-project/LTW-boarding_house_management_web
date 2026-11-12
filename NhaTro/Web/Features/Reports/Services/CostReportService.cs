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
        var all = await query.ToListAsync(ct);
        var ids = all.Select(x => x.reading.Id).ToList();
        var snapshots = await db.UtilityBillingPeriodSnapshots.AsNoTracking().Where(x => ids.Contains(x.BillingPeriodId)).ToListAsync(ct);
        var map = snapshots.ToDictionary(x => (x.BillingPeriodId, x.Type), x => x.Total);
        var rows = all.OrderBy(x => x.property.Name).ThenBy(x => x.room.Code).ThenBy(x => x.reading.PeriodStart).ToList();
        var contracts = await db.Contracts.AsNoTracking().Where(contract => contract.Status == Web.Features.Contracts.Enums.ContractStatus.Approved && all.Select(row => row.room.Id).Contains(contract.RoomId)).ToListAsync(ct);
        var items=rows.Select(x => { var e=Math.Max(0m,x.reading.ElectricEnd-x.reading.ElectricStart); var w=Math.Max(0m,x.reading.WaterEnd-x.reading.WaterStart); var ea=map.TryGetValue((x.reading.Id,TariffType.Electric),out var electric)?electric:0m; var wa=map.TryGetValue((x.reading.Id,TariffType.Water),out var water)?water:0m; var contract=contracts.Where(c=>c.RoomId==x.room.Id&&c.StartDate<=x.reading.PeriodEnd.ToDateTime(TimeOnly.MaxValue)&&c.EndDate>=x.reading.PeriodStart.ToDateTime(TimeOnly.MinValue)).OrderByDescending(c=>c.StartDate).ThenByDescending(c=>c.ContractId).FirstOrDefault(); return new CostReportItem { ReadingId=x.reading.Id,RoomId=x.room.Id,RoomCode=x.room.Code,PropertyId=x.property.Id,PropertyName=x.property.Name,ContractId=contract?.ContractId,PeriodStart=x.reading.PeriodStart,PeriodEnd=x.reading.PeriodEnd,ElectricUsage=e,WaterUsage=w,ElectricAmount=ea,WaterAmount=wa,TotalAmount=ea+wa,HasSnapshot=(e==0||map.ContainsKey((x.reading.Id,TariffType.Electric)))&&(w==0||map.ContainsKey((x.reading.Id,TariffType.Water))) }; }).ToList(); var kpis=new CostReportKpis { ReadingCount=items.Count, ElectricUsage=items.Sum(x=>x.ElectricUsage), WaterUsage=items.Sum(x=>x.WaterUsage), ElectricAmount=items.Sum(x=>x.ElectricAmount), WaterAmount=items.Sum(x=>x.WaterAmount), TotalAmount=items.Sum(x=>x.TotalAmount), MissingSnapshotCount=items.Count(x=>!x.HasSnapshot) };
        var skip=(int)Math.Min((long)(filter.Page-1)*filter.PageSize,int.MaxValue);
        return new CostReportResult { Kpis=kpis, Readings=new PagedResult<CostReportItem>{Items=items.Skip(skip).Take(filter.PageSize).ToList(),Page=filter.Page,PageSize=filter.PageSize,TotalCount=items.Count} };
    }
}
