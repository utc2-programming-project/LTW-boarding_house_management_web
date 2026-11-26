using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Properties;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;

namespace Web.Features.Vehicles.Services;

public interface IVehicleAuditService
{
    Task<IReadOnlyList<VehicleAuditSessionDto>> ListSessionsAsync(int propertyId, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleAuditSessionDto?> GetSessionAsync(int sessionId, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleAuditSessionDto> StartSessionAsync(int propertyId, string? note, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleAuditSessionDto> RecordItemResultAsync(int itemId, VehicleAuditItemResult result, string? note, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleAuditSessionDto> QuickAddVehicleAsync(int sessionId, VehicleCreateRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleAuditSessionDto> CompleteSessionAsync(int sessionId, string? note, PropertyCaller caller, CancellationToken ct = default);
}

/// <summary>
/// V5 &amp; D-VH-05: Kiểm kê bãi xe định kỳ trên giao diện tối ưu di động
/// (tick Present / Missing, thêm nhanh xe mới, lưu lịch sử kiểm kê chống thất thoát phí).
/// </summary>
public sealed class VehicleAuditService(
    ApplicationDbContext db,
    IVehicleService vehicles,
    AuditLogService audit,
    TimeProvider clock) : IVehicleAuditService
{
    public async Task<IReadOnlyList<VehicleAuditSessionDto>> ListSessionsAsync(
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!await VehicleScope.CanAccessPropertyAsync(db, propertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var sessionIds = await db.VehicleAuditSessions.AsNoTracking()
            .Where(s => s.PropertyId == propertyId)
            .OrderByDescending(s => s.StartedAt)
            .Take(25)
            .Select(s => s.Id)
            .ToListAsync(ct);

        // Mục 56: nạp phiên/items/xe/tên người kiểm kê theo lô thay vì N+1 cho từng phiên.
        return await BuildSessionDtosAsync(sessionIds, ct);
    }

    public async Task<VehicleAuditSessionDto?> GetSessionAsync(
        int sessionId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var propertyId = await db.VehicleAuditSessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => (int?)s.PropertyId)
            .FirstOrDefaultAsync(ct);

        if (!propertyId.HasValue)
        {
            return null;
        }

        if (!await VehicleScope.CanAccessPropertyAsync(db, propertyId.Value, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        return await BuildSessionDtoAsync(sessionId, ct);
    }

    public async Task<VehicleAuditSessionDto> StartSessionAsync(
        int propertyId,
        string? note,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!await VehicleScope.CanManageVehiclesAsync(db, propertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        // Nếu đang có phiên InProgress của nhà này thì trả về phiên đó
        var existingInProgressId = await db.VehicleAuditSessions.AsNoTracking()
            .Where(s => s.PropertyId == propertyId && s.Status == VehicleAuditSessionStatus.InProgress)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (existingInProgressId.HasValue)
        {
            return (await BuildSessionDtoAsync(existingInProgressId.Value, ct))!;
        }

        var activeVehicles = await (from v in db.Vehicles.AsNoTracking()
                                    join r in db.Rooms.IgnoreQueryFilters().AsNoTracking() on v.RoomId equals r.Id
                                    where v.PropertyId == propertyId && v.Status == VehicleStatus.Active
                                    orderby r.Code, v.PlateNumber
                                    select new
                                    {
                                        v.Id,
                                        v.PlateNumber,
                                        v.RoomId,
                                        RoomCode = r.Code,
                                        v.VehicleType,
                                    })
            .ToListAsync(ct);

        // SPEC §2.5.2: phiên kiểm kê phải bao gồm cả xe bạn gửi nhờ đang còn hiệu lực.
        var activeGuestNotices = await (from g in db.GuestVehicleNotices.AsNoTracking()
                                        join r in db.Rooms.IgnoreQueryFilters().AsNoTracking() on g.RoomId equals r.Id
                                        where g.PropertyId == propertyId && g.Status == GuestVehicleNoticeStatus.Active
                                        orderby r.Code, g.PlateNumber
                                        select new
                                        {
                                            g.Id,
                                            g.PlateNumber,
                                            g.RoomId,
                                            RoomCode = r.Code,
                                            g.VehicleType,
                                        })
            .ToListAsync(ct);

        var items = activeVehicles.Select(v => new VehicleAuditItem
        {
            VehicleId = v.Id,
            PlateNumberSnapshot = v.PlateNumber,
            RoomIdSnapshot = v.RoomId,
            RoomCodeSnapshot = v.RoomCode,
            VehicleTypeSnapshot = v.VehicleType,
            Result = VehicleAuditItemResult.Pending,
        }).ToList();

        items.AddRange(activeGuestNotices.Select(g => new VehicleAuditItem
        {
            GuestNoticeId = g.Id,
            PlateNumberSnapshot = g.PlateNumber,
            RoomIdSnapshot = g.RoomId,
            RoomCodeSnapshot = g.RoomCode,
            VehicleTypeSnapshot = g.VehicleType,
            Result = VehicleAuditItemResult.Pending,
        }));

        var now = clock.GetUtcNow();
        var session = new VehicleAuditSession
        {
            PropertyId = propertyId,
            AuditedByUserId = caller.UserId,
            StartedAt = now,
            Status = VehicleAuditSessionStatus.InProgress,
            TotalExpected = items.Count,
            TotalPresent = 0,
            TotalMissing = 0,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Items = items,
        };

        db.VehicleAuditSessions.Add(session);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.AuditStarted",
            caller.UserId,
            propertyId,
            $"SessionId={session.Id};Expected={session.TotalExpected}",
            now,
            ct);

        return (await BuildSessionDtoAsync(session.Id, ct))!;
    }

    public async Task<VehicleAuditSessionDto> RecordItemResultAsync(
        int itemId,
        VehicleAuditItemResult result,
        string? note,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!Enum.IsDefined(result))
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidAuditResult);
        }

        var item = await db.VehicleAuditItems.FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.AuditSessionNotFound);

        var session = await db.VehicleAuditSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == item.AuditSessionId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.AuditSessionNotFound);

        if (!await VehicleScope.CanManageVehiclesAsync(db, session.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        if (session.Status == VehicleAuditSessionStatus.Completed)
        {
            throw new InvalidOperationException(VehicleErrorCodes.AuditSessionAlreadyCompleted);
        }

        item.Result = result;
        item.CheckedAt = result == VehicleAuditItemResult.Pending ? null : clock.GetUtcNow();
        if (note is not null)
        {
            item.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        }

        session.TotalExpected = session.Items.Count;
        session.TotalPresent = session.Items.Count(i => i.Result == VehicleAuditItemResult.Present);
        session.TotalMissing = session.Items.Count(i => i.Result == VehicleAuditItemResult.Missing);

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.AuditItemChecked",
            caller.UserId,
            session.PropertyId,
            $"SessionId={session.Id};ItemId={item.Id};Plate={item.PlateNumberSnapshot};Result={result}",
            clock.GetUtcNow(),
            ct);

        return (await BuildSessionDtoAsync(session.Id, ct))!;
    }

    public async Task<VehicleAuditSessionDto> QuickAddVehicleAsync(
        int sessionId,
        VehicleCreateRequest request,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        var session = await db.VehicleAuditSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.AuditSessionNotFound);

        if (!await VehicleScope.CanManageVehiclesAsync(db, session.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        if (session.Status == VehicleAuditSessionStatus.Completed)
        {
            throw new InvalidOperationException(VehicleErrorCodes.AuditSessionAlreadyCompleted);
        }

        request.PropertyId = session.PropertyId;

        // Xe mới và dòng kiểm kê phải cùng thành công hoặc cùng hủy (EF InMemory bỏ qua transaction).
        await using var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        var created = await vehicles.CreateAsync(request, caller, ct);
        if (!created.Succeeded || created.Vehicle is null)
        {
            throw new InvalidOperationException(created.ErrorCode ?? VehicleErrorCodes.SaveFailed);
        }

        var now = clock.GetUtcNow();
        session.Items.Add(new VehicleAuditItem
        {
            AuditSessionId = session.Id,
            VehicleId = created.Vehicle.Id,
            PlateNumberSnapshot = created.Vehicle.PlateNumber,
            RoomIdSnapshot = created.Vehicle.RoomId,
            RoomCodeSnapshot = created.Vehicle.RoomCode,
            VehicleTypeSnapshot = created.Vehicle.VehicleType,
            Result = VehicleAuditItemResult.Present,
            CheckedAt = now,
            Note = "Bổ sung nhanh khi kiểm kê bãi xe",
        });

        session.TotalExpected = session.Items.Count;
        session.TotalPresent = session.Items.Count(i => i.Result == VehicleAuditItemResult.Present);
        session.TotalMissing = session.Items.Count(i => i.Result == VehicleAuditItemResult.Missing);

        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        return (await BuildSessionDtoAsync(session.Id, ct))!;
    }

    public async Task<VehicleAuditSessionDto> CompleteSessionAsync(
        int sessionId,
        string? note,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var session = await db.VehicleAuditSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.AuditSessionNotFound);

        if (!await VehicleScope.CanManageVehiclesAsync(db, session.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        if (session.Status == VehicleAuditSessionStatus.Completed)
        {
            return (await BuildSessionDtoAsync(session.Id, ct))!;
        }

        var now = clock.GetUtcNow();
        // Các xe chưa được tick khi hoàn tất sẽ được ghi nhận là Missing để cảnh báo đối chiếu
        foreach (var item in session.Items.Where(i => i.Result == VehicleAuditItemResult.Pending))
        {
            item.Result = VehicleAuditItemResult.Missing;
            item.CheckedAt = now;
        }

        session.TotalExpected = session.Items.Count;
        session.TotalPresent = session.Items.Count(i => i.Result == VehicleAuditItemResult.Present);
        session.TotalMissing = session.Items.Count(i => i.Result == VehicleAuditItemResult.Missing);
        session.Status = VehicleAuditSessionStatus.Completed;
        session.CompletedAt = now;
        if (!string.IsNullOrWhiteSpace(note))
        {
            session.Note = note.Trim();
        }

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.AuditCompleted",
            caller.UserId,
            session.PropertyId,
            $"SessionId={session.Id};Expected={session.TotalExpected};Present={session.TotalPresent};Missing={session.TotalMissing}",
            now,
            ct);

        return (await BuildSessionDtoAsync(session.Id, ct))!;
    }

    private async Task<VehicleAuditSessionDto?> BuildSessionDtoAsync(int sessionId, CancellationToken ct)
    {
        var dtos = await BuildSessionDtosAsync([sessionId], ct);
        return dtos.Count > 0 ? dtos[0] : null;
    }

    /// <summary>
    /// Dựng DTO cho nhiều phiên kiểm kê bằng 4 truy vấn cố định (phiên + items, tên nhà, tên người kiểm kê, siêu dữ liệu xe).
    /// </summary>
    private async Task<IReadOnlyList<VehicleAuditSessionDto>> BuildSessionDtosAsync(
        IReadOnlyList<int> sessionIds,
        CancellationToken ct)
    {
        if (sessionIds.Count == 0)
        {
            return [];
        }

        var sessions = await db.VehicleAuditSessions.AsNoTracking()
            .Include(s => s.Items)
            .Where(s => sessionIds.Contains(s.Id))
            .ToListAsync(ct);

        if (sessions.Count == 0)
        {
            return [];
        }

        var propertyIds = sessions.Select(s => s.PropertyId).Distinct().ToArray();
        var propertyNames = await db.Properties.IgnoreQueryFilters().AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var auditorIds = sessions.Select(s => s.AuditedByUserId).Distinct().ToArray();
        var auditorNames = await db.Users.AsNoTracking()
            .Where(u => auditorIds.Contains(u.Id))
            .Select(u => new { u.Id, Name = u.FullName ?? u.Email })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var vehicleIds = sessions
            .SelectMany(s => s.Items)
            .Where(i => i.VehicleId.HasValue)
            .Select(i => i.VehicleId!.Value)
            .Distinct()
            .ToArray();
        var vehicleMeta = vehicleIds.Length == 0
            ? new Dictionary<int, VehicleMeta>()
            : await (from v in db.Vehicles.AsNoTracking()
                     join z in db.ParkingZones.AsNoTracking() on v.ParkingZoneId equals z.Id into zGroup
                     from z in zGroup.DefaultIfEmpty()
                     join s in db.ParkingSlots.AsNoTracking() on v.ParkingSlotId equals s.Id into sGroup
                     from s in sGroup.DefaultIfEmpty()
                     where vehicleIds.Contains(v.Id)
                     select new VehicleMeta(
                         v.Id,
                         v.Brand,
                         v.Color,
                         z != null ? z.Name : null,
                         s != null ? s.Code : null))
                .ToDictionaryAsync(x => x.Id, ct);

        var byId = new Dictionary<int, VehicleAuditSessionDto>(sessions.Count);
        foreach (var session in sessions)
        {
            var items = session.Items
                .OrderBy(i => i.RoomCodeSnapshot)
                .ThenBy(i => i.PlateNumberSnapshot)
                .Select(i =>
                {
                    var meta = i.VehicleId.HasValue ? vehicleMeta.GetValueOrDefault(i.VehicleId.Value) : null;
                    return new VehicleAuditItemDto(
                        i.Id,
                        i.AuditSessionId,
                        i.VehicleId,
                        i.GuestNoticeId,
                        i.PlateNumberSnapshot,
                        i.RoomIdSnapshot,
                        i.RoomCodeSnapshot,
                        i.VehicleTypeSnapshot,
                        meta?.Brand,
                        meta?.Color,
                        meta?.ZoneName,
                        meta?.SlotCode,
                        i.Result,
                        i.CheckedAt,
                        i.Note);
                })
                .ToList();

            var presentCount = items.Count(i => i.Result == VehicleAuditItemResult.Present);
            var missingCount = items.Count(i => i.Result == VehicleAuditItemResult.Missing);
            var pendingCount = items.Count(i => i.Result == VehicleAuditItemResult.Pending);

            byId[session.Id] = new VehicleAuditSessionDto(
                session.Id,
                session.PropertyId,
                propertyNames.GetValueOrDefault(session.PropertyId, string.Empty),
                session.AuditedByUserId,
                auditorNames.GetValueOrDefault(session.AuditedByUserId),
                session.StartedAt,
                session.CompletedAt,
                session.Status,
                items.Count,
                presentCount,
                missingCount,
                pendingCount,
                session.Note,
                items);
        }

        // Giữ đúng thứ tự đầu vào (mới nhất trước) và bỏ các Id không còn tồn tại.
        return sessionIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
    }

    private sealed record VehicleMeta(int Id, string? Brand, string? Color, string? ZoneName, string? SlotCode);
}
