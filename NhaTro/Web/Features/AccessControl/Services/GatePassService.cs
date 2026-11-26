using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.AccessControl.Rules;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Contracts.Enums;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Infrastructure.Time;

namespace Web.Features.AccessControl.Services;

public interface IGatePassService
{
    Task<IReadOnlyList<GatePassDto>> ListAsync(PropertyCaller caller, GatePassStatus? status = null, CancellationToken ct = default);
    Task<IReadOnlyList<GateRoomOption>> RoomsAsync(PropertyCaller caller, CancellationToken ct = default);
    Task<GatePassResult> CreateAsync(GatePassCreateRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task<GatePassResult> ApproveAsync(int id, PropertyCaller caller, CancellationToken ct = default);
    Task<GatePassResult> RejectAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default);
    Task<GatePassResult> RevokeAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default);
}

public sealed class GatePassService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    IGateTokenService tokens,
    INotificationService notifications,
    AuditLogService audit) : IGatePassService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<IReadOnlyList<GatePassDto>> ListAsync(
        PropertyCaller caller, GatePassStatus? status = null, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var query = ScopePasses(db, caller);
        if (status is { } filter)
        {
            query = query.Where(pass => pass.Status == filter);
        }

        var rows = await Project(db, query.OrderByDescending(pass => pass.CreatedAt)).ToListAsync(ct);

        var propertyIds = rows.Select(row => row.PropertyId).Distinct().ToArray();
        var approvalByProperty = await db.GatePolicies.AsNoTracking()
            .Where(policy => propertyIds.Contains(policy.PropertyId))
            .ToDictionaryAsync(policy => policy.PropertyId, policy => policy.AllowManagerApprove, ct);

        return rows.Select(row => row with
        {
            CanApprove = (caller.IsChuTro || (caller.IsQuanLy && approvalByProperty.GetValueOrDefault(row.PropertyId)))
                         && row.Status == GatePassStatus.Pending,
            CanRevoke = row.Status is GatePassStatus.Pending or GatePassStatus.Approved &&
                        (caller.IsChuTro || caller.IsQuanLy || (caller.IsKhachThue && row.IssuedByUserId == caller.UserId)),
        }).ToArray();
    }

    public async Task<IReadOnlyList<GateRoomOption>> RoomsAsync(PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
        if (caller.IsKhachThue)
        {
            // Distinct/OrderBy trên projection record không dịch được sang SQL (EF Core 10),
            // nên distinct ở SQL bằng kiểu ẩn danh rồi sắp xếp + map ở client.
            var rows = await (from member in db.ContractMembers.AsNoTracking()
                              join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
                              join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                              join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                              join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                              where tenant.UserId == caller.UserId && contract.Status == ContractStatus.Approved
                                  && contract.StartDate <= today && contract.EndDate >= today
                              select new { RoomId = room.Id, PropertyId = property.Id, PropertyName = property.Name, RoomCode = room.Code })
                .Distinct()
                .ToListAsync(ct);

            return rows
                .OrderBy(row => row.PropertyName).ThenBy(row => row.RoomCode)
                .Select(row => new GateRoomOption(row.RoomId, row.PropertyId, row.PropertyName, row.RoomCode))
                .ToArray();
        }

        if (!caller.IsManager) return [];
        var properties = GateScope.Apply(db, caller);
        return await (from room in db.Rooms.AsNoTracking()
                      join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                      where room.IsActive && !room.IsDeleted && properties.Any(item => item.Id == room.PropertyId)
                      orderby property.Name, room.Code
                      select new GateRoomOption(room.Id, property.Id, property.Name, room.Code)).ToListAsync(ct);
    }

    public async Task<GatePassResult> CreateAsync(GatePassCreateRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        var isTenant = caller.IsKhachThue;
        if (!caller.IsManager && !isTenant) return GatePassResult.Fail(GateErrorCodes.Forbidden);

        var invalid = GatePassRules.ValidateCreate(request, isTenant);
        if (invalid is not null) return GatePassResult.Fail(invalid);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        int propertyId;
        int? roomId;
        int? tenantId = null;
        Guid? contractId = null;
        GateIssuerRole issuerRole;

        if (isTenant)
        {
            var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
            var lease = await (from member in db.ContractMembers.AsNoTracking()
                               join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
                               join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                               join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                               where tenant.UserId == caller.UserId && contract.Status == ContractStatus.Approved
                                   && contract.StartDate <= today && contract.EndDate >= today
                                   && (request.RoomId == null || contract.RoomId == request.RoomId)
                               orderby member.IsRepresentative descending
                               select new { contract.ContractId, contract.RoomId, TenantId = tenant.Id, room.PropertyId })
                .FirstOrDefaultAsync(ct);

            if (lease is null) return GatePassResult.Fail(GateErrorCodes.TenantNoActiveContract);

            propertyId = lease.PropertyId;
            roomId = lease.RoomId;
            tenantId = lease.TenantId;
            contractId = lease.ContractId;
            issuerRole = GateIssuerRole.KhachThue;
        }
        else
        {
            roomId = request.RoomId;
            if (roomId is { } id)
            {
                var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, ct);
                if (room is null) return GatePassResult.Fail(GateErrorCodes.RoomNotFound);
                propertyId = room.PropertyId;
            }
            else if (request.PropertyId is { } requestedProperty)
            {
                propertyId = requestedProperty;
            }
            else
            {
                return GatePassResult.Fail(GateErrorCodes.PropertyNotFound);
            }

            if (!await GateScope.CanAccessAsync(db, propertyId, caller, ct))
            {
                return GatePassResult.Fail(GateErrorCodes.Forbidden);
            }

            issuerRole = caller.IsChuTro ? GateIssuerRole.ChuTro : GateIssuerRole.QuanLy;
        }

        var policy = await LoadPolicyAsync(db, propertyId, ct);
        if (isTenant && !policy.AllowTenantIssuePass) return GatePassResult.Fail(GateErrorCodes.TenantIssueDisabled);
        if (isTenant && request.StayType == GateStayType.Overnight && !policy.TenantMayIssueOvernight)
        {
            return GatePassResult.Fail(GateErrorCodes.TenantOvernightDisabled);
        }

        if (isTenant)
        {
            var dayStart = now.ToOffset(VietnamOffset).Date;
            var dayStartOffset = new DateTimeOffset(dayStart, VietnamOffset);
            var issuedToday = await db.GatePasses.CountAsync(pass =>
                pass.IssuedByUserId == caller.UserId && pass.CreatedAt >= dayStartOffset, ct);
            if (GatePassRules.TenantQuotaExceeded(issuedToday, policy.TenantMaxPassesPerDay))
            {
                return GatePassResult.Fail(GateErrorCodes.TenantQuotaExceeded);
            }
        }

        var ttl = isTenant
            ? Math.Clamp(request.TtlMinutes ?? policy.TenantMaxTtlMinutes,
                GatePassRules.MinTtlMinutes, Math.Max(policy.TenantMaxTtlMinutes, GatePassRules.MinTtlMinutes))
            : Math.Clamp(request.TtlMinutes ?? policy.PassTtlMinutes, GatePassRules.MinTtlMinutes, GatePassRules.MaxTtlMinutes);
        var maxUses = isTenant
            ? Math.Clamp(request.MaxUses ?? policy.TenantMaxUses, 1, Math.Max(policy.TenantMaxUses, 1))
            : Math.Clamp(request.MaxUses ?? 1, 1, 10);

        // Chủ trọ/quản lý cấp phiếu thì mặc định duyệt ngay; khách thuê chỉ được tự duyệt
        // khách đến chơi trong ngày và phải nằm trong khung giờ cho phép.
        var autoApprove = !isTenant
            || (GatePassRules.CanAutoApprove(policy, request.StayType) &&
                GatePassRules.IsInsideDayVisitWindow(policy, now));

        var rawToken = tokens.NewToken();
        var code = request.IssueCode ? tokens.NewCode() : null;
        var pass = new GatePass
        {
            PropertyId = propertyId,
            RoomId = roomId,
            TenantId = tenantId,
            ContractId = contractId,
            PassType = GatePassType.Guest,
            StayType = request.StayType,
            TokenHash = tokens.Hash(rawToken),
            CodeHash = code is null ? null : tokens.Hash(code),
            VisitorName = request.VisitorName.Trim(),
            VisitorPhone = request.VisitorPhone?.Trim(),
            VisitorIdNumber = request.VisitorIdNumber?.Trim(),
            VisitorDob = request.VisitorDob,
            StayReason = request.StayReason?.Trim(),
            Note = request.Note?.Trim(),
            Status = autoApprove ? GatePassStatus.Approved : GatePassStatus.Pending,
            AllowedMethods = GatePassRules.DefaultMethods(request.StayType),
            MaxUses = maxUses,
            ExpiresAt = GatePassRules.ComputeExpiry(now, ttl),
            IssuerRole = issuerRole,
            IssuedByUserId = caller.UserId,
            ApprovedByUserId = autoApprove ? caller.UserId : null,
            ApprovedAt = autoApprove ? now : null,
            AutoApproved = autoApprove,
            CreatedAt = now,
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        db.GatePasses.Add(pass);
        await db.SaveChangesAsync(ct);

        if (pass.Status == GatePassStatus.Approved && pass.StayType == GateStayType.Overnight)
        {
            var report = await CreateStayReportAsync(db, pass, policy, now, ct);
            pass.StayReportId = report.Id;
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "GatePassCreated",
            caller.UserId,
            propertyId,
            metadata: $"pass:{pass.Id};stay:{pass.StayType};status:{pass.Status};issuer:{issuerRole}",
            cancellationToken: ct);

        if (issuerRole == GateIssuerRole.KhachThue)
        {
            var ownerId = await OwnerOfAsync(db, propertyId, ct);
            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                await notifications.NotifyAsync(ownerId,
                    "Khách thuê cấp phiếu khách",
                    $"Phiếu #{pass.Id} cho {pass.VisitorName} ({StatusLabel(pass)}) đang chờ xử lý.",
                    "/gate/passes");
            }
        }

        var dto = await LoadDtoAsync(db, pass.Id, ct);
        return new GatePassResult(true, Created: new GatePassCreatedDto(dto, rawToken, code));
    }

    public async Task<GatePassResult> ApproveAsync(int id, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var pass = await db.GatePasses.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (pass is null) return GatePassResult.Fail(GateErrorCodes.PassNotFound);
        if (!await GateScope.CanAccessAsync(db, pass.PropertyId, caller, ct)) return GatePassResult.Fail(GateErrorCodes.Forbidden);

        var policy = await LoadPolicyAsync(db, pass.PropertyId, ct);
        if (!CanApprove(caller, policy)) return GatePassResult.Fail(GateErrorCodes.Forbidden);
        if (pass.Status != GatePassStatus.Pending) return GatePassResult.Fail(GateErrorCodes.InvalidStatus);

        var now = clock.GetUtcNow();
        pass.Status = GatePassStatus.Approved;
        pass.ApprovedByUserId = caller.UserId;
        pass.ApprovedAt = now;
        pass.AutoApproved = false;

        if (pass.StayType == GateStayType.Overnight && pass.StayReportId is null)
        {
            var report = await CreateStayReportAsync(db, pass, policy, now, ct);
            pass.StayReportId = report.Id;
        }

        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(AuditLogCategory.Business, "GatePassApproved", caller.UserId, pass.PropertyId,
            metadata: $"pass:{pass.Id}", cancellationToken: ct);
        await notifications.NotifyAsync(pass.IssuedByUserId, "Phiếu khách đã được duyệt",
            $"Phiếu #{pass.Id} cho {pass.VisitorName} đã được duyệt.", "/gate/passes");

        return new GatePassResult(true, Pass: await LoadDtoAsync(db, pass.Id, ct));
    }

    public async Task<GatePassResult> RejectAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return GatePassResult.Fail(GateErrorCodes.ReasonRequired);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var pass = await db.GatePasses.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (pass is null) return GatePassResult.Fail(GateErrorCodes.PassNotFound);
        if (!await GateScope.CanAccessAsync(db, pass.PropertyId, caller, ct)) return GatePassResult.Fail(GateErrorCodes.Forbidden);

        var policy = await LoadPolicyAsync(db, pass.PropertyId, ct);
        if (!CanApprove(caller, policy)) return GatePassResult.Fail(GateErrorCodes.Forbidden);
        if (pass.Status != GatePassStatus.Pending) return GatePassResult.Fail(GateErrorCodes.InvalidStatus);

        pass.Status = GatePassStatus.Rejected;
        pass.RejectedReason = reason.Trim();
        pass.ApprovedByUserId = caller.UserId;
        pass.ApprovedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(AuditLogCategory.Business, "GatePassRejected", caller.UserId, pass.PropertyId,
            metadata: $"pass:{pass.Id}", cancellationToken: ct);
        await notifications.NotifyAsync(pass.IssuedByUserId, "Phiếu khách bị từ chối",
            $"Phiếu #{pass.Id} cho {pass.VisitorName} bị từ chối: {reason.Trim()}", "/gate/passes");

        return new GatePassResult(true, Pass: await LoadDtoAsync(db, pass.Id, ct));
    }

    public async Task<GatePassResult> RevokeAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return GatePassResult.Fail(GateErrorCodes.ReasonRequired);

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var pass = await db.GatePasses.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (pass is null) return GatePassResult.Fail(GateErrorCodes.PassNotFound);

        var isIssuer = pass.IssuedByUserId == caller.UserId;
        var isManager = caller.IsManager && await GateScope.CanAccessAsync(db, pass.PropertyId, caller, ct);
        if (!isManager && !isIssuer) return GatePassResult.Fail(GateErrorCodes.Forbidden);
        if (isIssuer && !isManager && (pass.Status is not (GatePassStatus.Pending or GatePassStatus.Approved) || pass.UsedCount > 0))
        {
            return GatePassResult.Fail(GateErrorCodes.InvalidStatus);
        }

        if (pass.Status is GatePassStatus.Rejected or GatePassStatus.Revoked)
        {
            return GatePassResult.Fail(GateErrorCodes.InvalidStatus);
        }

        var now = clock.GetUtcNow();
        pass.Status = GatePassStatus.Revoked;
        pass.RevokedAt = now;
        pass.RevokedReason = reason.Trim();
        pass.RevokedByUserId = caller.UserId;

        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(AuditLogCategory.Business, "GatePassRevoked", caller.UserId, pass.PropertyId,
            metadata: $"pass:{pass.Id}", cancellationToken: ct);
        if (!isIssuer)
        {
            await notifications.NotifyAsync(pass.IssuedByUserId, "Phiếu khách đã bị thu hồi",
                $"Phiếu #{pass.Id} cho {pass.VisitorName} đã bị thu hồi: {reason.Trim()}", "/gate/passes");
        }

        return new GatePassResult(true, Pass: await LoadDtoAsync(db, pass.Id, ct));
    }

    internal static bool CanApprove(PropertyCaller caller, GatePolicyDto policy) =>
        GatePassRules.CanApprove(caller.IsChuTro, caller.IsQuanLy, policy.AllowManagerApprove);

    internal static async Task<GatePolicyDto> LoadPolicyAsync(ApplicationDbContext db, int propertyId, CancellationToken ct)
    {
        var policy = await db.GatePolicies.AsNoTracking().FirstOrDefaultAsync(item => item.PropertyId == propertyId, ct);
        return policy is null ? GatePolicyDto.Defaults(propertyId) : GatePolicyService.ToDto(policy);
    }

    internal static async Task<StayReport> CreateStayReportAsync(
        ApplicationDbContext db, GatePass pass, GatePolicyDto policy, DateTimeOffset now, CancellationToken ct)
    {
        var property = await db.Properties.AsNoTracking().FirstOrDefaultAsync(item => item.Id == pass.PropertyId, ct);
        var address = string.IsNullOrWhiteSpace(property?.Address) ? property?.Name ?? "" : property.Address;
        var report = new StayReport
        {
            GatePassId = pass.Id,
            PropertyId = pass.PropertyId,
            RoomId = pass.RoomId,
            ContractId = pass.ContractId,
            GuestName = pass.VisitorName,
            GuestDob = pass.VisitorDob,
            GuestIdNumber = pass.VisitorIdNumber,
            StayReason = pass.StayReason,
            StayFrom = now,
            StayTo = StayReportRules.DefaultStayTo(now),
            AddressSnapshot = address,
            Status = StayReportStatus.Pending,
            Channel = policy.PreferredStayChannel,
            DueAt = StayReportRules.DueAt(now),
            CreatedAt = now,
        };
        db.StayReports.Add(report);
        await db.SaveChangesAsync(ct);
        return report;
    }

    internal static async Task<string?> OwnerOfAsync(ApplicationDbContext db, int propertyId, CancellationToken ct) =>
        await db.Properties.AsNoTracking().Where(item => item.Id == propertyId).Select(item => item.OwnerId).FirstOrDefaultAsync(ct);

    internal static IQueryable<GatePass> ScopePasses(ApplicationDbContext db, PropertyCaller caller)
    {
        var passes = db.GatePasses.AsNoTracking();
        if (caller.IsChuTro || caller.IsQuanLy)
        {
            var properties = GateScope.Apply(db, caller);
            return passes.Where(pass => properties.Any(property => property.Id == pass.PropertyId));
        }

        if (caller.IsKhachThue)
        {
            return passes.Where(pass => pass.IssuedByUserId == caller.UserId);
        }

        return passes.Where(_ => false);
    }

    internal static IQueryable<GatePassDto> Project(ApplicationDbContext db, IQueryable<GatePass> passes) =>
        from pass in passes
        join property in db.Properties.AsNoTracking() on pass.PropertyId equals property.Id
        join room in db.Rooms.AsNoTracking() on pass.RoomId equals room.Id into rooms
        from room in rooms.DefaultIfEmpty()
        select new GatePassDto(
            pass.Id, pass.PropertyId, property.Name, pass.RoomId, room == null ? null : room.Code,
            pass.VisitorName, pass.VisitorPhone, pass.PassType, pass.StayType, pass.Status, pass.ExpiresAt,
            pass.MaxUses, pass.UsedCount, pass.IssuerRole, pass.IssuedByUserId, pass.AutoApproved,
            pass.RejectedReason, pass.RevokedReason, pass.CreatedAt);

    private static async Task<GatePassDto> LoadDtoAsync(ApplicationDbContext db, int id, CancellationToken ct) =>
        await Project(db, db.GatePasses.AsNoTracking().Where(pass => pass.Id == id)).FirstAsync(ct);

    private static string StatusLabel(GatePass pass) => pass.Status switch
    {
        GatePassStatus.Approved => "đã duyệt",
        GatePassStatus.Pending => "chờ duyệt",
        GatePassStatus.Rejected => "bị từ chối",
        GatePassStatus.Used => "đã dùng",
        GatePassStatus.Expired => "hết hạn",
        GatePassStatus.Revoked => "đã thu hồi",
        _ => "không rõ",
    };
}
