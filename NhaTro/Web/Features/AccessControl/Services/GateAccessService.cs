using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.AccessControl.Gates;
using Web.Features.AccessControl.Rules;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Infrastructure.Time;

namespace Web.Features.AccessControl.Services;

public interface IGateAccessService
{
    Task<GateVerifyResult> VerifyAndRecordAsync(GateVerifyRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task<GateVerifyResult> ManualOpenAsync(int propertyId, int? gateDeviceId, string reason, PropertyCaller caller, CancellationToken ct = default);
    Task<IReadOnlyList<GateLogDto>> LogsAsync(PropertyCaller caller, GateLogFilter filter, CancellationToken ct = default);
}

public sealed class GateAccessService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    IGateTokenService tokens,
    IGateController gateController,
    AuditLogService audit,
    IOptions<GateRetentionOptions> retention) : IGateAccessService
{
    public async Task<GateVerifyResult> VerifyAndRecordAsync(
        GateVerifyRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager) return Fail(GateErrorCodes.Forbidden, "Chỉ chủ trọ/quản lý được vận hành cổng.");
        if (string.IsNullOrWhiteSpace(request.Token)) return Fail(GateErrorCodes.TokenInvalid, "Thiếu mã.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await GateScope.CanAccessAsync(db, request.PropertyId, caller, ct))
        {
            return Fail(GateErrorCodes.Forbidden, "Ngoài phạm vi nhà trọ.");
        }

        var now = clock.GetUtcNow();
        var token = request.Token.Trim();
        var hash = tokens.Hash(token);
        var policy = await GatePassService.LoadPolicyAsync(db, request.PropertyId, ct);
        var pass = await db.GatePasses.FirstOrDefaultAsync(item =>
            item.PropertyId == request.PropertyId && (item.TokenHash == hash || item.CodeHash == hash), ct);

        if (pass is null)
        {
            // QR động của cư dân (ký HMAC theo cửa sổ 60 giây) — không lưu token trong DB.
            if (request.Method == GateMethod.Qr &&
                tokens.TryVerifyResidentToken(token, now, out var residentUserId, out var tokenPropertyId) &&
                tokenPropertyId == request.PropertyId)
            {
                var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
                var resident = await (from member in db.ContractMembers.AsNoTracking()
                                      join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
                                      join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                                      join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                                      where tenant.UserId == residentUserId
                                          && room.PropertyId == request.PropertyId
                                          && contract.Status == ContractStatus.Approved
                                          && contract.StartDate <= today && contract.EndDate >= today
                                      orderby member.IsRepresentative descending
                                      select new { TenantId = tenant.Id, tenant.FullName }).FirstOrDefaultAsync(ct);

                if (resident is not null)
                {
                    var lastResident = await db.GateLogs.AsNoTracking()
                        .Where(log => log.UserId == residentUserId && log.PropertyId == request.PropertyId && log.IsSuccess)
                        .OrderByDescending(log => log.OccurredAt)
                        .Select(log => (GateDirection?)log.Direction)
                        .FirstOrDefaultAsync(ct);

                    if (policy.AntiPassbackEnabled)
                    {
                        if (request.Direction == GateDirection.In && lastResident == GateDirection.In)
                        {
                            await WriteLogAsync(db, request, caller, null, false, GateErrorCodes.AlreadyInside, resident.FullName, now, ct);
                            return Fail(GateErrorCodes.AlreadyInside, "Người này đang ở trong nhà.");
                        }

                        if (request.Direction == GateDirection.Out && lastResident != GateDirection.In)
                        {
                            await WriteLogAsync(db, request, caller, null, false, GateErrorCodes.NotInside, resident.FullName, now, ct);
                            return Fail(GateErrorCodes.NotInside, "Chưa ghi nhận lượt vào.");
                        }
                    }

                    var residentLog = await WriteLogAsync(db, request, caller, null, true, null, resident.FullName, now, ct);
                    residentLog.UserId = residentUserId;
                    residentLog.TenantId = resident.TenantId;
                    await db.SaveChangesAsync(ct);
                    await audit.RecordAsync(AuditLogCategory.Business, "GateAccessGranted", caller.UserId, request.PropertyId,
                        metadata: $"resident:{residentUserId};log:{residentLog.Id}", cancellationToken: ct);
                    var residentOpened = await TryOpenAsync(request.PropertyId, request.GateDeviceId,
                        $"resident:{residentUserId}", caller.UserId, ct);
                    return new GateVerifyResult(true, null, $"Hợp lệ — {resident.FullName}", null, resident.FullName)
                    { CanOpen = residentOpened };
                }
            }

            await WriteLogAsync(db, request, caller, null, false, GateErrorCodes.TokenInvalid, null, now, ct);
            return Fail(GateErrorCodes.TokenInvalid, "Mã không hợp lệ.");
        }

        var reason = Validate(pass, request, now, policy);
        if (reason is not null)
        {
            await WriteLogAsync(db, request, caller, pass, false, reason, pass.VisitorName, now, ct);
            return Fail(reason, MessageFor(reason));
        }

        if (policy.AntiPassbackEnabled)
        {
            var last = await db.GateLogs.AsNoTracking()
                .Where(log => log.GatePassId == pass.Id && log.IsSuccess)
                .OrderByDescending(log => log.OccurredAt)
                .Select(log => (GateDirection?)log.Direction)
                .FirstOrDefaultAsync(ct);
            if (request.Direction == GateDirection.In && last == GateDirection.In)
            {
                await WriteLogAsync(db, request, caller, pass, false, GateErrorCodes.AlreadyInside, pass.VisitorName, now, ct);
                return Fail(GateErrorCodes.AlreadyInside, "Người này đang ở trong nhà.");
            }

            if (request.Direction == GateDirection.Out && last != GateDirection.In)
            {
                await WriteLogAsync(db, request, caller, pass, false, GateErrorCodes.NotInside, pass.VisitorName, now, ct);
                return Fail(GateErrorCodes.NotInside, "Chưa ghi nhận lượt vào.");
            }
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        pass.UsedCount++;
        if (pass.UsedCount >= pass.MaxUses)
        {
            pass.Status = GatePassStatus.Used;
        }

        var log = await WriteLogAsync(db, request, caller, pass, true, null, pass.VisitorName, now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        await audit.RecordAsync(AuditLogCategory.Business, "GateAccessGranted", caller.UserId, pass.PropertyId,
            metadata: $"pass:{pass.Id};log:{log.Id};method:{request.Method}", cancellationToken: ct);

        var opened = await TryOpenAsync(pass.PropertyId, request.GateDeviceId, $"pass:{pass.Id}", caller.UserId, ct);
        return new GateVerifyResult(true, null, $"Hợp lệ — {pass.VisitorName}", pass.Id, pass.VisitorName) { CanOpen = opened };
    }

    public async Task<GateVerifyResult> ManualOpenAsync(
        int propertyId, int? gateDeviceId, string reason, PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsManager) return Fail(GateErrorCodes.Forbidden, "Chỉ chủ trọ/quản lý được mở tay.");
        if (string.IsNullOrWhiteSpace(reason)) return Fail(GateErrorCodes.ReasonRequired, "Bắt buộc nhập lý do.");

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await GateScope.CanAccessAsync(db, propertyId, caller, ct))
        {
            return Fail(GateErrorCodes.Forbidden, "Ngoài phạm vi nhà trọ.");
        }

        var policy = await GatePassService.LoadPolicyAsync(db, propertyId, ct);
        if (!policy.AllowManualOpen) return Fail(GateErrorCodes.ManualOpenDisabled, "Chính sách không cho mở tay.");

        var request = new GateVerifyRequest
        {
            PropertyId = propertyId,
            GateDeviceId = gateDeviceId,
            Method = GateMethod.Manual,
            Direction = GateDirection.In,
        };
        var now = clock.GetUtcNow();
        await WriteLogAsync(db, request, caller, null, true, null, $"Mở tay: {reason.Trim()}", now, ct);
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(AuditLogCategory.Business, "GateManualOpen", caller.UserId, propertyId,
            metadata: $"reason:{reason.Trim()}", cancellationToken: ct);

        var opened = await TryOpenAsync(propertyId, gateDeviceId, reason.Trim(), caller.UserId, ct);
        return new GateVerifyResult(true, null, "Đã ghi nhận mở tay.", null, null) { CanOpen = opened };
    }

    public async Task<IReadOnlyList<GateLogDto>> LogsAsync(
        PropertyCaller caller, GateLogFilter filter, CancellationToken ct = default)
    {
        if (!caller.IsManager) return [];
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var properties = GateScope.Apply(db, caller);
        var query = db.GateLogs.AsNoTracking()
            .Where(log => properties.Any(property => property.Id == log.PropertyId));

        if (filter.From is { } from) query = query.Where(log => log.OccurredAt >= from);

        // Hạn lưu 30 ngày: không hiển thị nhật ký cũ hơn cutoff dù bộ lọc không đặt From.
        var retentionDays = retention.Value.RetentionDays;
        if (retentionDays > 0)
        {
            var cutoff = clock.GetUtcNow().AddDays(-retentionDays);
            if (filter.From is null || filter.From < cutoff)
            {
                query = query.Where(log => log.OccurredAt >= cutoff);
            }
        }
        if (filter.To is { } to) query = query.Where(log => log.OccurredAt <= to);
        if (filter.Success is { } success) query = query.Where(log => log.IsSuccess == success);
        if (filter.Direction is { } direction) query = query.Where(log => log.Direction == direction);

        return await (from log in query.OrderByDescending(log => log.OccurredAt).Take(Math.Clamp(filter.Take, 1, 500))
                      join property in db.Properties.AsNoTracking() on log.PropertyId equals property.Id
                      join device in db.GateDevices.AsNoTracking() on log.GateDeviceId equals device.Id into devices
                      from device in devices.DefaultIfEmpty()
                      select new GateLogDto(log.Id, property.Name, device == null ? null : device.Name, log.Direction,
                          log.Method, log.IsSuccess, log.Reason, log.SubjectName, log.OccurredAt))
            .ToListAsync(ct);
    }

    private static string? Validate(GatePass pass, GateVerifyRequest request, DateTimeOffset now, GatePolicyDto policy)
    {
        if (pass.Status == GatePassStatus.Revoked) return GateErrorCodes.PassRevoked;
        if (pass.Status is GatePassStatus.Rejected or GatePassStatus.Expired) return GateErrorCodes.PassNotApproved;
        if (!pass.AllowedMethods.HasFlag(request.Method)) return GateErrorCodes.MethodNotAllowed;
        if (request.Method == GateMethod.Manual && !policy.AllowManualOpen) return GateErrorCodes.ManualOpenDisabled;

        // Lượt ra vẫn phải quẹt được sau khi phiếu đã dùng đủ lượt hoặc hết hạn,
        // nếu không nhật ký sẽ thiếu lượt ra và anti-passback sai.
        if (request.Direction == GateDirection.Out)
        {
            return pass.Status is GatePassStatus.Approved or GatePassStatus.Used
                ? null
                : GateErrorCodes.PassNotApproved;
        }

        if (pass.Status == GatePassStatus.Used || pass.UsedCount >= pass.MaxUses) return GateErrorCodes.PassUsedUp;
        if (pass.Status != GatePassStatus.Approved) return GateErrorCodes.PassNotApproved;
        if (pass.ExpiresAt < now) return GateErrorCodes.PassExpired;
        if (pass.StayType == GateStayType.Visit && !GatePassRules.IsInsideDayVisitWindow(policy, now))
        {
            return GateErrorCodes.OutsideAllowedHours;
        }

        return null;
    }

    private async Task<GateLog> WriteLogAsync(
        ApplicationDbContext db, GateVerifyRequest request, PropertyCaller caller, GatePass? pass,
        bool success, string? reason, string? subjectName, DateTimeOffset now, CancellationToken ct)
    {
        var log = new GateLog
        {
            PropertyId = request.PropertyId,
            GateDeviceId = request.GateDeviceId,
            GatePassId = pass?.Id,
            SubjectType = GateSubjectType.Person,
            Direction = request.Direction,
            Method = request.Method,
            IsSuccess = success,
            Reason = reason,
            SubjectName = subjectName,
            UserId = pass?.PassType == GatePassType.Resident ? pass.IssuedByUserId : null,
            TenantId = pass?.TenantId,
            OccurredAt = now,
        };
        db.GateLogs.Add(log);

        if (request.GateDeviceId is { } deviceId)
        {
            var device = await db.GateDevices.FirstOrDefaultAsync(item => item.Id == deviceId && item.PropertyId == request.PropertyId, ct);
            if (device is not null)
            {
                device.IsOnline = true;
                device.LastSeenAt = now;
            }
        }

        return log;
    }

    private async Task<bool> TryOpenAsync(int propertyId, int? gateDeviceId, string reason, string actorUserId, CancellationToken ct)
    {
        var result = await gateController.OpenAsync(new GateOpenRequest(propertyId, gateDeviceId, reason, actorUserId), ct);
        return result.Opened;
    }

    private static GateVerifyResult Fail(string code, string message) => new(false, code, message, null, null);

    internal static string MessageFor(string code) => code switch
    {
        GateErrorCodes.PassRevoked => "Phiếu đã bị thu hồi.",
        GateErrorCodes.PassNotApproved => "Phiếu chưa được duyệt.",
        GateErrorCodes.PassExpired => "Phiếu đã hết hạn.",
        GateErrorCodes.PassUsedUp => "Phiếu đã dùng hết lượt.",
        GateErrorCodes.OutsideAllowedHours => "Ngoài khung giờ khách đến chơi.",
        GateErrorCodes.MethodNotAllowed => "Phương thức này không được phép cho phiếu.",
        GateErrorCodes.ManualOpenDisabled => "Chính sách không cho mở tay.",
        _ => "Không hợp lệ.",
    };
}
