using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Notifications;
using Web.Features.Properties;

namespace Web.Features.AccessControl.Services;

public interface IStayReportService
{
    Task<IReadOnlyList<StayReportDto>> ListAsync(PropertyCaller caller, StayReportStatus? status = null, CancellationToken ct = default);
    Task<int> RefreshOverdueAsync(PropertyCaller caller, CancellationToken ct = default);
    Task<StayReportResult> MarkReportedAsync(int id, StayReportChannel channel, string? evidenceRef, string? note, PropertyCaller caller, CancellationToken ct = default);
    Task<StayReportResult> CancelAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default);
}

public sealed class StayReportService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    INotificationService notifications,
    AuditLogService audit) : IStayReportService
{
    public async Task<IReadOnlyList<StayReportDto>> ListAsync(
        PropertyCaller caller, StayReportStatus? status = null, CancellationToken ct = default)
    {
        if (!caller.IsManager) return [];
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = Scope(db, caller);
        if (status is { } filter) query = query.Where(report => report.Status == filter);

        return await (from report in query
                      join property in db.Properties.AsNoTracking() on report.PropertyId equals property.Id
                      join room in db.Rooms.AsNoTracking() on report.RoomId equals room.Id into rooms
                      from room in rooms.DefaultIfEmpty()
                      orderby report.DueAt
                      select new StayReportDto(
                          report.Id, report.GatePassId, property.Name, room == null ? null : room.Code,
                          report.GuestName, report.GuestDob, report.GuestIdNumber, report.StayReason,
                          report.StayFrom, report.StayTo, report.Status, report.Channel, report.DueAt,
                          report.ReportedAt, report.Note))
            .ToListAsync(ct);
    }

    public async Task<int> RefreshOverdueAsync(PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager) return 0;
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        var overdue = await Scope(db, caller)
            .Where(report => report.Status == StayReportStatus.Pending && report.DueAt < now)
            .ToListAsync(ct);
        if (overdue.Count == 0) return 0;

        foreach (var report in overdue)
        {
            report.Status = StayReportStatus.Overdue;
        }

        await db.SaveChangesAsync(ct);
        var ownerCache = new Dictionary<int, string?>();
        foreach (var propertyId in overdue.Select(item => item.PropertyId).Distinct())
        {
            ownerCache[propertyId] = await GatePassService.OwnerOfAsync(db, propertyId, ct);
        }

        foreach (var report in overdue)
        {
            await audit.RecordAsync(AuditLogCategory.Business, "StayReportOverdue", caller.UserId, report.PropertyId,
                metadata: $"report:{report.Id}", cancellationToken: ct);
            if (ownerCache.GetValueOrDefault(report.PropertyId) is { } ownerId)
            {
                await notifications.NotifyAsync(ownerId, "Quá hạn thông báo lưu trú",
                    $"Phiếu lưu trú #{report.Id} của {report.GuestName} đã quá hạn.", "/gate/stays");
            }
        }

        return overdue.Count;
    }

    public async Task<StayReportResult> MarkReportedAsync(
        int id, StayReportChannel channel, string? evidenceRef, string? note, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var report = await db.StayReports.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (report is null) return new StayReportResult(false, GateErrorCodes.InvalidStatus);
        if (!await GateScope.CanAccessAsync(db, report.PropertyId, caller, ct))
        {
            return new StayReportResult(false, GateErrorCodes.Forbidden);
        }

        if (report.Status is StayReportStatus.Reported or StayReportStatus.Cancelled)
        {
            return new StayReportResult(false, GateErrorCodes.InvalidStatus);
        }

        report.Status = StayReportStatus.Reported;
        report.Channel = channel;
        report.ReportedAt = clock.GetUtcNow();
        report.ReportedByUserId = caller.UserId;
        report.EvidenceRef = evidenceRef?.Trim();
        report.Note = note?.Trim();
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(AuditLogCategory.Business, "StayReportMarked", caller.UserId, report.PropertyId,
            metadata: $"report:{report.Id};channel:{channel}", cancellationToken: ct);
        return new StayReportResult(true);
    }

    public async Task<StayReportResult> CancelAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return new StayReportResult(false, GateErrorCodes.ReasonRequired);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var report = await db.StayReports.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (report is null) return new StayReportResult(false, GateErrorCodes.InvalidStatus);
        if (!await GateScope.CanAccessAsync(db, report.PropertyId, caller, ct))
        {
            return new StayReportResult(false, GateErrorCodes.Forbidden);
        }

        if (report.Status == StayReportStatus.Reported)
        {
            return new StayReportResult(false, GateErrorCodes.InvalidStatus);
        }

        report.Status = StayReportStatus.Cancelled;
        report.Note = reason.Trim();
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(AuditLogCategory.Business, "StayReportCancelled", caller.UserId, report.PropertyId,
            metadata: $"report:{report.Id}", cancellationToken: ct);
        return new StayReportResult(true);
    }

    private static IQueryable<StayReport> Scope(ApplicationDbContext db, PropertyCaller caller)
    {
        var reports = db.StayReports.AsNoTracking();
        if (caller.IsChuTro || caller.IsQuanLy)
        {
            var properties = GateScope.Apply(db, caller);
            return reports.Where(report => properties.Any(property => property.Id == report.PropertyId));
        }

        return reports.Where(_ => false);
    }
}
