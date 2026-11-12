using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Maintenance.Enums;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Infrastructure.Time;

namespace Web.Features.Reports.Services;

public sealed class IncidentReportService(IDbContextFactory<ApplicationDbContext> factory, IManagerTogglePolicy toggles) : IIncidentReportService
{
    public async Task<IncidentReportResult> GetAsync(IncidentReportFilter filter, PropertyCaller caller, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter); ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsManager) throw new UnauthorizedAccessException("Khong duoc xem bao cao su co.");
        if (caller.IsQuanLy) foreach (var code in new[] { "SW-15", "SW-15e" }) if (await toggles.EvaluateAsync(code, caller.Role, ct) != RoomChangeDecision.Allowed) throw new UnauthorizedAccessException("Khong duoc xem bao cao su co.");
        filter.Normalize(); if (filter.From > filter.To) return new IncidentReportResult { Incidents = new PagedResult<IncidentReportItem> { Page=filter.Page,PageSize=filter.PageSize } };
        await using var db=await factory.CreateDbContextAsync(ct);
        var properties=PropertyScope.Apply(db.Properties.AsNoTracking(),db.PropertyManagers.AsNoTracking(),caller).Where(property=>!property.IsDeleted);
        var query=from incident in db.MaintenanceIncidents.AsNoTracking() join room in db.Rooms.AsNoTracking() on incident.RoomId equals room.Id join property in properties on room.PropertyId equals property.Id select new { incident,room,property };
        if(filter.PropertyId is { } p) query=query.Where(x=>x.property.Id==p); if(filter.RoomId is { } r) query=query.Where(x=>x.room.Id==r); if(filter.Status is { } s) query=query.Where(x=>x.incident.Status==s); if(filter.Category is { } c) query=query.Where(x=>x.incident.Category==c); if(filter.Urgency is { } u) query=query.Where(x=>x.incident.Urgency==u);
        if(filter.From is { } from) query=query.Where(x=>x.incident.CreatedAt>=VietnamTime.DayRangeUtc(from).StartUtc); if(filter.To is { } to) query=query.Where(x=>x.incident.CreatedAt<VietnamTime.DayRangeUtc(to).EndUtc);
        var groups=await query.GroupBy(x=>x.incident.Status).Select(g=>new{g.Key,Count=g.Count()}).ToListAsync(ct); int C(IncidentStatus s)=>groups.FirstOrDefault(x=>x.Key==s)?.Count??0; var total=groups.Sum(x=>x.Count); var urgent=await query.CountAsync(x=>x.incident.Urgency==IncidentUrgency.Urgent||x.incident.Urgency==IncidentUrgency.Emergency,ct);
        var skip=(int)Math.Min((long)(filter.Page-1)*filter.PageSize,int.MaxValue); var rows=await query.OrderByDescending(x=>x.incident.CreatedAt).ThenByDescending(x=>x.incident.Id).Skip(skip).Take(filter.PageSize).Select(x=>new IncidentReportItem{IncidentId=x.incident.Id,RoomId=x.room.Id,RoomCode=x.room.Code,PropertyId=x.property.Id,PropertyName=x.property.Name,Title=x.incident.Title,Category=x.incident.Category,Urgency=x.incident.Urgency,Status=x.incident.Status,CreatedAtUtc=x.incident.CreatedAt,RepairCost=x.incident.RepairCost}).ToListAsync(ct);
        return new IncidentReportResult{Kpis=new IncidentReportKpis{Total=total,New=C(IncidentStatus.New),InProgress=C(IncidentStatus.InProgress),Completed=C(IncidentStatus.Completed),Urgent=urgent},Incidents=new PagedResult<IncidentReportItem>{Items=rows,TotalCount=total,Page=filter.Page,PageSize=filter.PageSize}};
    }
}
