using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Contracts.Enums;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Vehicles.Services;

public interface IGuestVehicleNoticeService
{
    Task<IReadOnlyList<GuestVehicleNoticeDto>> ListAsync(int? propertyId, GuestVehicleNoticeStatus? status, PropertyCaller caller, CancellationToken ct = default);
    Task<GuestVehicleNoticeDto> CreateAsync(GuestVehicleNoticeCreateRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task UpdateStatusAsync(int noticeId, GuestVehicleNoticeStatus newStatus, PropertyCaller caller, CancellationToken ct = default);
}

/// <summary>
/// V4 &amp; D-VH-13: Báo xe bạn gửi nhờ / qua đêm (tối đa 3 phiếu Active cùng lúc / phòng, không tính phí tháng).
/// </summary>
public sealed class GuestVehicleNoticeService(
    ApplicationDbContext db,
    INotificationService notifications,
    AuditLogService audit,
    TimeProvider clock,
    IStringLocalizer<VehicleResource>? localizer = null) : IGuestVehicleNoticeService
{
    public async Task<IReadOnlyList<GuestVehicleNoticeDto>> ListAsync(
        int? propertyId,
        GuestVehicleNoticeStatus? status,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        List<int> allowedPropertyIds;
        List<int>? allowedRoomIds = null;

        if (caller.IsKhachThue)
        {
            var today = clock.TodayVietnam();
            allowedRoomIds = await VehicleScope.GetTenantRoomIdsAsync(db, caller, today, ct);
            allowedPropertyIds = await VehicleScope.GetTenantPropertyIdsAsync(db, caller, today, ct);
        }
        else
        {
            allowedPropertyIds = await VehicleScope.ApplyProperties(db, caller)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        if (propertyId.HasValue)
        {
            allowedPropertyIds = allowedPropertyIds.Where(id => id == propertyId.Value).ToList();
        }

        if (allowedPropertyIds.Count == 0)
        {
            return [];
        }

        await RefreshExpiredNoticesAsync(allowedPropertyIds, ct);

        var query = from n in db.GuestVehicleNotices.AsNoTracking()
                    join r in db.Rooms.IgnoreQueryFilters().AsNoTracking() on n.RoomId equals r.Id
                    join p in db.Properties.IgnoreQueryFilters().AsNoTracking() on n.PropertyId equals p.Id
                    join t in db.Tenants.IgnoreQueryFilters().AsNoTracking() on n.TenantId equals t.Id into tGroup
                    from t in tGroup.DefaultIfEmpty()
                    where allowedPropertyIds.Contains(n.PropertyId)
                        && (allowedRoomIds == null || allowedRoomIds.Contains(n.RoomId))
                        && (!status.HasValue || n.Status == status.Value)
                    orderby n.Status, n.ExpectedTo descending
                    select new GuestVehicleNoticeDto(
                        n.Id,
                        n.PropertyId,
                        p.Name,
                        n.RoomId,
                        r.Code,
                        n.TenantId,
                        t != null ? t.FullName : null,
                        n.PlateNumber,
                        n.VehicleType,
                        n.GuestName,
                        n.ExpectedFrom,
                        n.ExpectedTo,
                        n.Note,
                        n.Status,
                        n.CreatedAt);

        return await query.ToListAsync(ct);
    }

    public async Task<GuestVehicleNoticeDto> CreateAsync(
        GuestVehicleNoticeCreateRequest request,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        var room = await db.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.RoomNotFound);

        var today = clock.TodayVietnam();
        if (caller.IsKhachThue)
        {
            var tenantRooms = await VehicleScope.GetTenantRoomIdsAsync(db, caller, today, ct);
            if (!tenantRooms.Contains(room.Id))
            {
                throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
            }
        }
        else if (!await VehicleScope.CanAccessPropertyAsync(db, room.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var todayDate = today.ToDateTime(TimeOnly.MinValue);
        var activeContractId = await db.Contracts.AsNoTracking()
            .Where(c => c.RoomId == room.Id
                && c.Status == ContractStatus.Approved
                && c.StartDate <= todayDate
                && c.EndDate >= todayDate)
            .OrderByDescending(c => c.StartDate)
            .Select(c => (Guid?)c.ContractId)
            .FirstOrDefaultAsync(ct);
        if (activeContractId is null)
        {
            throw new InvalidOperationException(VehicleErrorCodes.NoActiveContract);
        }

        // TenantId là bắt buộc: khách thuê lấy từ tài khoản, chủ trọ/quản lý lấy từ hợp đồng của phòng.
        int? tenantId;
        if (caller.IsKhachThue)
        {
            tenantId = await db.Tenants.AsNoTracking()
                .Where(t => t.UserId == caller.UserId)
                .Select(t => (int?)t.Id)
                .FirstOrDefaultAsync(ct);
        }
        else
        {
            tenantId = await db.ContractMembers.AsNoTracking()
                .Where(m => m.ContractId == activeContractId.Value)
                .Select(m => (int?)m.TenantId)
                .FirstOrDefaultAsync(ct);
        }

        if (tenantId is null)
        {
            throw new InvalidOperationException(VehicleErrorCodes.TenantNotInRoom);
        }

        var now = clock.GetUtcNow();
        var setting = await VehicleSettingService.EnsureEntityAsync(db, room.PropertyId, now, ct);
        if (caller.IsKhachThue && !setting.AllowTenantGuestNotice)
        {
            throw new InvalidOperationException(VehicleErrorCodes.GuestNoticeDisabled);
        }

        var normalizedPlate = VehicleRules.NormalizePlateNumber(request.PlateNumber);
        if (!VehicleRules.IsValidPlateNumber(normalizedPlate))
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidPlateNumber);
        }

        // Chặn khoảng thời gian vô lý: bắt đầu quá khứ xa / quá xa tương lai, hoặc kéo dài quá 7 ngày.
        if (request.ExpectedTo <= request.ExpectedFrom
            || request.ExpectedTo <= now
            || request.ExpectedFrom < now.AddHours(-1)
            || request.ExpectedFrom > now.AddDays(7)
            || request.ExpectedTo > request.ExpectedFrom.AddDays(7))
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidTimeRange);
        }

        await RefreshExpiredNoticesAsync([room.PropertyId], ct);

        // D-VH-13: Tối đa 3 phiếu GuestVehicleNotice đang Active cùng lúc cho mỗi phòng
        var activeInRoom = await db.GuestVehicleNotices.AsNoTracking()
            .CountAsync(g => g.RoomId == room.Id && g.Status == GuestVehicleNoticeStatus.Active, ct);
        if (activeInRoom >= VehicleRules.MaxActiveGuestNoticesPerRoom)
        {
            throw new InvalidOperationException(VehicleErrorCodes.GuestNoticeLimitReached);
        }

        var notice = new GuestVehicleNotice
        {
            PropertyId = room.PropertyId,
            RoomId = room.Id,
            TenantId = tenantId.Value,
            CreatedByUserId = caller.UserId,
            PlateNumber = normalizedPlate,
            VehicleType = request.VehicleType,
            GuestName = string.IsNullOrWhiteSpace(request.GuestName) ? null : request.GuestName.Trim(),
            ExpectedFrom = request.ExpectedFrom,
            ExpectedTo = request.ExpectedTo,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            Status = GuestVehicleNoticeStatus.Active,
            CreatedAt = now,
        };

        db.GuestVehicleNotices.Add(notice);
        await db.SaveChangesAsync(ct);

        // SPEC §2.4.2: thông báo cho Chủ trọ và Quản lý được giao (trừ người tạo).
        var ownerId = await db.Properties.AsNoTracking()
            .Where(p => p.Id == room.PropertyId)
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync(ct);

        var managerIds = await db.PropertyManagers.AsNoTracking()
            .Where(m => m.PropertyId == room.PropertyId && m.RevokedAt == null)
            .Select(m => m.ManagerUserId)
            .ToListAsync(ct);

        var recipients = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            recipients.Add(ownerId);
        }

        foreach (var managerId in managerIds)
        {
            if (!string.IsNullOrWhiteSpace(managerId))
            {
                recipients.Add(managerId);
            }
        }

        recipients.Remove(caller.UserId);

        var arrivedAt = VietnamTime.ToVietnam(notice.ExpectedFrom.UtcDateTime);
        foreach (var recipientId in recipients)
        {
            await notifications.NotifyAsync(
                recipientId,
                VehicleLocalization.T(localizer, "GuestNoticeNotifTitle",
                    "Phòng {0} báo xe bạn gửi nhờ ({1})", room.Code, VehicleRules.FormatPlateForDisplay(notice.PlateNumber)),
                VehicleLocalization.T(localizer, "GuestNoticeNotifBody",
                    "Biển số: {0}. Dự kiến đến: {1}.", notice.PlateNumber, arrivedAt.ToString("dd/MM/yyyy HH:mm")),
                "/vehicles/guests");
        }

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.GuestNoticeCreated",
            caller.UserId,
            room.PropertyId,
            $"Plate={notice.PlateNumber};Room={room.Code};ExpectedTo={notice.ExpectedTo:O}",
            now,
            ct);

        var all = await ListAsync(room.PropertyId, null, caller, ct);
        return all.First(g => g.Id == notice.Id);
    }

    public async Task UpdateStatusAsync(
        int noticeId,
        GuestVehicleNoticeStatus newStatus,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var notice = await db.GuestVehicleNotices.FirstOrDefaultAsync(n => n.Id == noticeId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.GuestNoticeNotFound);

        if (caller.IsKhachThue)
        {
            var tenantRooms = await VehicleScope.GetTenantRoomIdsAsync(db, caller, clock.TodayVietnam(), ct);
            if (!tenantRooms.Contains(notice.RoomId) || notice.CreatedByUserId != caller.UserId)
            {
                throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
            }

            // Khách thuê chỉ được hủy; đánh dấu "đã rời đi" (Completed) thuộc Chủ trọ/Quản lý.
            if (newStatus == GuestVehicleNoticeStatus.Completed)
            {
                throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
            }
        }
        else if (!await VehicleScope.CanAccessPropertyAsync(db, notice.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        // SPEC §3: chỉ phiếu đang Active mới chuyển được sang Completed / Cancelled.
        if (notice.Status != GuestVehicleNoticeStatus.Active
            || newStatus is not (GuestVehicleNoticeStatus.Completed or GuestVehicleNoticeStatus.Cancelled))
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidStatus);
        }

        notice.Status = newStatus;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.GuestNoticeStatusChanged",
            caller.UserId,
            notice.PropertyId,
            $"NoticeId={notice.Id};Plate={notice.PlateNumber};Status={newStatus}",
            clock.GetUtcNow(),
            ct);
    }

    private async Task RefreshExpiredNoticesAsync(IReadOnlyCollection<int> propertyIds, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var overdue = await db.GuestVehicleNotices
            .Where(g => propertyIds.Contains(g.PropertyId)
                && g.Status == GuestVehicleNoticeStatus.Active
                && g.ExpectedTo < now)
            .ToListAsync(ct);

        if (overdue.Count == 0)
        {
            return;
        }

        foreach (var item in overdue)
        {
            item.Status = GuestVehicleNoticeStatus.Expired;
        }

        await db.SaveChangesAsync(ct);

        // Tác vụ hệ thống (không có actor): ghi nhận số phiếu tự hết hạn theo từng khu nhà.
        foreach (var group in overdue.GroupBy(n => n.PropertyId))
        {
            var metadata = string.Join(",", group.Select(n => $"NoticeId={n.Id};Plate={n.PlateNumber}"));
            await audit.RecordAsync(
                AuditLogCategory.Business,
                "Vehicle.GuestNoticeExpired",
                actorUserId: null,
                group.Key,
                $"ExpiredCount={group.Count()};{metadata}",
                now,
                ct);
        }
    }
}
