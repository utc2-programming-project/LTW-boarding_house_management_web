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

public interface IGatePassInviteService
{
    Task<GateInviteResult> CreateAsync(GateInviteCreateRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task<IReadOnlyList<GateInviteDto>> ListActiveAsync(PropertyCaller caller, CancellationToken ct = default);
    Task<GateInviteResult> CancelAsync(int id, PropertyCaller caller, CancellationToken ct = default);
    Task<GuestPassLocation?> PeekAsync(string rawToken, CancellationToken ct = default);
    Task<GuestPassSubmitResult> SubmitAsync(string rawToken, GuestPassSubmission submission, CancellationToken ct = default);
    Task<GuestPassPublicDto?> GetPublicPassAsync(string rawPassToken, CancellationToken ct = default);
}

/// <summary>
/// Link/QR mời khách vãng lai tự điền phiếu. Token 32 byte, chỉ lưu SHA-256, dùng một lần,
/// gắn đúng một phòng; khách không cần tài khoản nhưng phải có link do người trong nhà tạo.
/// </summary>
public sealed class GatePassInviteService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    IGateTokenService tokens,
    INotificationService notifications,
    AuditLogService audit) : IGatePassInviteService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly int[] AllowedExpiryMinutes = [15, 60, 360, 1440];

    public async Task<GateInviteResult> CreateAsync(
        GateInviteCreateRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        var isTenant = caller.IsKhachThue;
        if (!caller.IsManager && !isTenant)
        {
            return GateInviteResult.Fail(GateErrorCodes.Forbidden);
        }

        if (!AllowedExpiryMinutes.Contains(request.ExpiryMinutes))
        {
            return GateInviteResult.Fail(GateErrorCodes.InvalidRequest);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var room = await db.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.RoomId && item.IsActive && !item.IsDeleted, ct);
        if (room is null)
        {
            return GateInviteResult.Fail(GateErrorCodes.RoomNotFound);
        }

        var propertyId = room.PropertyId;

        // Kiểm tra phạm vi nhà trước khi lộ bất kỳ cấu hình nào của nhà đó.
        if (!isTenant && !await GateScope.CanAccessAsync(db, propertyId, caller, ct))
        {
            return GateInviteResult.Fail(GateErrorCodes.Forbidden);
        }

        var policy = await GatePassService.LoadPolicyAsync(db, propertyId, ct);
        if (!policy.AllowGuestSelfFill)
        {
            return GateInviteResult.Fail(GateErrorCodes.GuestFillDisabled);
        }

        int? tenantId = null;
        Guid? contractId = null;
        GateIssuerRole issuerRole;

        if (isTenant)
        {
            if (!policy.AllowTenantIssuePass)
            {
                return GateInviteResult.Fail(GateErrorCodes.TenantIssueDisabled);
            }

            var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
            var lease = await (from member in db.ContractMembers.AsNoTracking()
                               join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
                               join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                               where tenant.UserId == caller.UserId
                                   && contract.Status == ContractStatus.Approved
                                   && contract.StartDate <= today && contract.EndDate >= today
                                   && contract.RoomId == room.Id
                               orderby member.IsRepresentative descending
                               select new { contract.ContractId, TenantId = tenant.Id })
                .FirstOrDefaultAsync(ct);

            if (lease is null)
            {
                return GateInviteResult.Fail(GateErrorCodes.TenantNoActiveContract);
            }

            tenantId = lease.TenantId;
            contractId = lease.ContractId;
            issuerRole = GateIssuerRole.KhachThue;
        }
        else
        {
            issuerRole = caller.IsChuTro ? GateIssuerRole.ChuTro : GateIssuerRole.QuanLy;
        }

        var now = clock.GetUtcNow();
        var rawToken = tokens.NewToken();
        var invite = new GatePassInvite
        {
            PropertyId = propertyId,
            RoomId = room.Id,
            TenantId = tenantId,
            ContractId = contractId,
            CreatedByUserId = caller.UserId,
            IssuerRole = issuerRole,
            TokenHash = tokens.Hash(rawToken),
            ExpiresAt = now.AddMinutes(request.ExpiryMinutes),
            CreatedAt = now,
        };

        db.GatePassInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "GateInviteCreated",
            caller.UserId,
            propertyId,
            metadata: $"invite:{invite.Id};room:{room.Id};issuer:{issuerRole}",
            cancellationToken: ct);

        var created = await LoadInviteDtoAsync(db, invite.Id, ct);
        return new GateInviteResult(true, Created: created is null ? null : new GateInviteCreatedDto(created, rawToken));
    }

    public async Task<IReadOnlyList<GateInviteDto>> ListActiveAsync(PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        var query = db.GatePassInvites.AsNoTracking()
            .Where(invite => invite.UsedAt == null && invite.RevokedAt == null && invite.ExpiresAt > now);

        if (caller.IsKhachThue)
        {
            query = query.Where(invite => invite.CreatedByUserId == caller.UserId);
        }
        else if (caller.IsManager)
        {
            var properties = GateScope.Apply(db, caller);
            query = query.Where(invite => properties.Any(property => property.Id == invite.PropertyId));
        }
        else
        {
            return [];
        }

        return await ProjectInvites(db, query.OrderByDescending(invite => invite.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<GateInviteResult> CancelAsync(int id, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var invite = await db.GatePassInvites.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (invite is null)
        {
            return GateInviteResult.Fail(GateErrorCodes.InviteInvalid);
        }

        var isCreator = invite.CreatedByUserId == caller.UserId;
        var isManager = caller.IsManager && await GateScope.CanAccessAsync(db, invite.PropertyId, caller, ct);
        if (!isCreator && !isManager)
        {
            return GateInviteResult.Fail(GateErrorCodes.Forbidden);
        }

        if (invite.UsedAt is not null || invite.RevokedAt is not null)
        {
            return GateInviteResult.Fail(GateErrorCodes.InvalidStatus);
        }

        invite.RevokedAt = clock.GetUtcNow();
        invite.RevokedByUserId = caller.UserId;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "GateInviteRevoked",
            caller.UserId,
            invite.PropertyId,
            metadata: $"invite:{invite.Id}",
            cancellationToken: ct);

        return new GateInviteResult(true);
    }

    public async Task<GuestPassLocation?> PeekAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        var hash = tokens.Hash(rawToken.Trim());
        var row = await (from invite in db.GatePassInvites.AsNoTracking()
                         join property in db.Properties.AsNoTracking() on invite.PropertyId equals property.Id
                         join room in db.Rooms.AsNoTracking() on invite.RoomId equals room.Id
                         where invite.TokenHash == hash
                             && invite.UsedAt == null && invite.RevokedAt == null && invite.ExpiresAt > now
                         select new { invite.Id, invite.PropertyId, invite.RoomId, PropertyName = property.Name, RoomCode = room.Code, invite.ExpiresAt })
            .FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var policy = await GatePassService.LoadPolicyAsync(db, row.PropertyId, ct);
        if (!policy.AllowGuestSelfFill)
        {
            return null;
        }

        var requiredFields = GatePassRules.ParseRequiredGuestFields(policy.RequiredGuestFields).Order().ToArray();
        return new GuestPassLocation(row.Id, row.PropertyId, row.PropertyName, row.RoomId, row.RoomCode, row.ExpiresAt, requiredFields);
    }

    public async Task<GuestPassSubmitResult> SubmitAsync(
        string rawToken, GuestPassSubmission submission, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.InviteInvalid);
        }

        // Khách tự điền: chuẩn hoá và bắt buộc SĐT di động Việt Nam để quota/so trùng ổn định.
        var phone = GatePassRules.NormalizePhone(submission.VisitorPhone);
        if (!GatePassRules.IsVietnamMobile(phone))
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.MissingGuestPhone);
        }

        var today = clock.TodayVietnam();
        var invalid = GatePassRules.ValidateCreate(new GatePassCreateRequest
        {
            RoomId = 1,
            VisitorName = submission.VisitorName,
            VisitorPhone = phone,
            VisitorIdNumber = submission.VisitorIdNumber,
            VisitorDob = submission.VisitorDob,
            StayReason = submission.StayReason,
            Note = submission.Note,
            StayType = submission.StayType,
        }, isTenant: false, todayVietnam: today);
        if (invalid is not null)
        {
            return GuestPassSubmitResult.Fail(invalid);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var now = clock.GetUtcNow();
        var hash = tokens.Hash(rawToken.Trim());
        var invite = await db.GatePassInvites.FirstOrDefaultAsync(item => item.TokenHash == hash, ct);
        if (invite is null || invite.UsedAt is not null || invite.RevokedAt is not null || invite.ExpiresAt <= now)
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.InviteInvalid);
        }

        var policy = await GatePassService.LoadPolicyAsync(db, invite.PropertyId, ct);
        if (!policy.AllowGuestSelfFill)
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.GuestFillDisabled);
        }

        var requiredFields = GatePassRules.ParseRequiredGuestFields(policy.RequiredGuestFields);
        if ((requiredFields.Contains("Dob") && submission.VisitorDob is null)
            || (requiredFields.Contains("IdNumber") && string.IsNullOrWhiteSpace(submission.VisitorIdNumber))
            || (requiredFields.Contains("StayReason") && string.IsNullOrWhiteSpace(submission.StayReason)))
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.MissingLawfulFields);
        }

        var isTenantIssue = invite.IssuerRole == GateIssuerRole.KhachThue;
        if (isTenantIssue && !policy.AllowTenantIssuePass)
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.TenantIssueDisabled);
        }

        if (isTenantIssue && submission.StayType == GateStayType.Overnight && !policy.TenantMayIssueOvernight)
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.TenantOvernightDisabled);
        }

        var dayStart = now.ToOffset(VietnamOffset).Date;
        var dayStartOffset = new DateTimeOffset(dayStart, VietnamOffset);

        if (isTenantIssue)
        {
            var issuedToday = await db.GatePasses.CountAsync(pass =>
                pass.IssuedByUserId == invite.CreatedByUserId && pass.CreatedAt >= dayStartOffset, ct);
            if (GatePassRules.TenantQuotaExceeded(issuedToday, policy.TenantMaxPassesPerDay))
            {
                return GuestPassSubmitResult.Fail(GateErrorCodes.TenantQuotaExceeded);
            }
        }

        var selfFilledToday = await db.GatePasses.CountAsync(pass =>
            pass.PropertyId == invite.PropertyId
            && pass.GatePassInviteId != null
            && pass.VisitorPhone == phone
            && pass.CreatedAt >= dayStartOffset, ct);
        if (GatePassRules.SelfFillQuotaExceeded(selfFilledToday, policy.GuestSelfMaxPassesPerDay))
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.GuestQuotaExceeded);
        }

        var duplicate = await db.GatePasses.AnyAsync(pass =>
            pass.PropertyId == invite.PropertyId
            && pass.GatePassInviteId != null
            && pass.VisitorPhone == phone
            && (pass.Status == GatePassStatus.Pending || pass.Status == GatePassStatus.Approved)
            && pass.ExpiresAt > now, ct);
        if (duplicate)
        {
            return GuestPassSubmitResult.Fail(GateErrorCodes.DuplicateGuestRequest);
        }

        var ttl = isTenantIssue
            ? Math.Clamp(policy.TenantMaxTtlMinutes, GatePassRules.MinTtlMinutes, GatePassRules.MaxTtlMinutes)
            : Math.Clamp(policy.PassTtlMinutes, GatePassRules.MinTtlMinutes, GatePassRules.MaxTtlMinutes);
        var maxUses = isTenantIssue ? Math.Clamp(policy.TenantMaxUses, 1, 10) : 1;

        var autoApprove = !isTenantIssue
            || (GatePassRules.CanAutoApprove(policy, submission.StayType)
                && GatePassRules.IsInsideDayVisitWindow(policy, now));

        var rawPassToken = tokens.NewToken();
        var code = tokens.NewCode();
        var pass = new GatePass
        {
            PropertyId = invite.PropertyId,
            RoomId = invite.RoomId,
            TenantId = invite.TenantId,
            ContractId = invite.ContractId,
            PassType = GatePassType.Guest,
            StayType = submission.StayType,
            TokenHash = tokens.Hash(rawPassToken),
            CodeHash = tokens.Hash(code),
            VisitorName = submission.VisitorName.Trim(),
            VisitorPhone = string.IsNullOrWhiteSpace(phone) ? null : phone,
            VisitorIdNumber = string.IsNullOrWhiteSpace(submission.VisitorIdNumber) ? null : submission.VisitorIdNumber.Trim(),
            VisitorDob = submission.VisitorDob,
            StayReason = string.IsNullOrWhiteSpace(submission.StayReason) ? null : submission.StayReason.Trim(),
            Note = string.IsNullOrWhiteSpace(submission.Note) ? null : submission.Note.Trim(),
            Status = autoApprove ? GatePassStatus.Approved : GatePassStatus.Pending,
            AllowedMethods = GatePassRules.DefaultMethods(submission.StayType),
            MaxUses = maxUses,
            ExpiresAt = GatePassRules.ComputeExpiry(now, ttl),
            IssuerRole = invite.IssuerRole,
            IssuedByUserId = invite.CreatedByUserId,
            ApprovedByUserId = autoApprove ? invite.CreatedByUserId : null,
            ApprovedAt = autoApprove ? now : null,
            AutoApproved = autoApprove,
            GatePassInviteId = invite.Id,
            CreatedAt = now,
        };

        // InMemory (dùng cho unit test) không hỗ trợ transaction; SQL Server thì có.
        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        db.GatePasses.Add(pass);
        await db.SaveChangesAsync(ct);

        if (pass.Status == GatePassStatus.Approved && pass.StayType == GateStayType.Overnight)
        {
            var report = await GatePassService.CreateStayReportAsync(db, pass, policy, now, ct);
            pass.StayReportId = report.Id;
        }

        invite.UsedAt = now;
        invite.UsedGatePassId = pass.Id;
        await db.SaveChangesAsync(ct);
        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "GatePassCreated",
            invite.CreatedByUserId,
            invite.PropertyId,
            metadata: $"pass:{pass.Id};stay:{pass.StayType};status:{pass.Status};issuer:{invite.IssuerRole};via:invite{invite.Id}",
            cancellationToken: ct);

        if (isTenantIssue)
        {
            var ownerId = await GatePassService.OwnerOfAsync(db, invite.PropertyId, ct);
            if (!string.IsNullOrWhiteSpace(ownerId))
            {
                await notifications.NotifyAsync(ownerId,
                    "Khách đã điền phiếu từ link mời",
                    $"Phiếu #{pass.Id} cho {pass.VisitorName} ({StatusLabel(pass.Status)}) — do khách thuê mời.",
                    "/gate/passes");
            }
        }

        return new GuestPassSubmitResult(true, Created: new GuestPassSubmittedDto(
            pass.Id, rawPassToken, code, pass.Status, pass.AutoApproved, pass.ExpiresAt));
    }

    public async Task<GuestPassPublicDto?> GetPublicPassAsync(string rawPassToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawPassToken))
        {
            return null;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var hash = tokens.Hash(rawPassToken.Trim());
        return await (from pass in db.GatePasses.AsNoTracking()
                      join property in db.Properties.AsNoTracking() on pass.PropertyId equals property.Id
                      join room in db.Rooms.AsNoTracking() on pass.RoomId equals room.Id into rooms
                      from room in rooms.DefaultIfEmpty()
                      where pass.TokenHash == hash
                      select new GuestPassPublicDto(
                          pass.Id, property.Name, room == null ? "—" : room.Code, pass.VisitorName,
                          pass.StayType, pass.Status, pass.ExpiresAt, pass.RejectedReason, pass.RevokedReason))
            .FirstOrDefaultAsync(ct);
    }

    private static IQueryable<GateInviteDto> ProjectInvites(ApplicationDbContext db, IQueryable<GatePassInvite> invites) =>
        from invite in invites
        join property in db.Properties.AsNoTracking() on invite.PropertyId equals property.Id
        join room in db.Rooms.AsNoTracking() on invite.RoomId equals room.Id
        join user in db.Users.AsNoTracking() on invite.CreatedByUserId equals user.Id into users
        from user in users.DefaultIfEmpty()
        select new GateInviteDto(
            invite.Id, invite.PropertyId, property.Name, invite.RoomId, room.Code,
            invite.CreatedByUserId,
            user == null ? invite.CreatedByUserId : (user.FullName ?? user.Email ?? invite.CreatedByUserId),
            invite.ExpiresAt, invite.CreatedAt);

    private static async Task<GateInviteDto?> LoadInviteDtoAsync(ApplicationDbContext db, int id, CancellationToken ct) =>
        await ProjectInvites(db, db.GatePassInvites.AsNoTracking().Where(invite => invite.Id == id)).FirstOrDefaultAsync(ct);

    private static string StatusLabel(GatePassStatus status) => status switch
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
