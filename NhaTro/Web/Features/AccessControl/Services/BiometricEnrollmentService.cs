using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Contracts.Enums;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Infrastructure.Time;

namespace Web.Features.AccessControl.Services;

public interface IBiometricEnrollmentService
{
    Task<IReadOnlyList<BiometricEnrollmentDto>> ListAsync(PropertyCaller caller, CancellationToken ct = default);
    Task<int> EnrollAsync(BiometricMethod method, string? templateRef, string? publicKey, PropertyCaller caller, CancellationToken ct = default);
    Task<bool> ApproveAsync(int id, PropertyCaller caller, CancellationToken ct = default);
    Task<bool> RejectAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default);
    Task<bool> RevokeAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default);
}

public sealed class BiometricEnrollmentService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    INotificationService notifications,
    AuditLogService audit) : IBiometricEnrollmentService
{
    public async Task<IReadOnlyList<BiometricEnrollmentDto>> ListAsync(PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        IQueryable<BiometricEnrollment> query;
        if (caller.IsKhachThue)
        {
            var tenantIds = await TenantIdsAsync(db, caller.UserId, ct);
            query = db.BiometricEnrollments.AsNoTracking().Where(item => item.TenantId != null && tenantIds.Contains(item.TenantId.Value));
        }
        else if (caller.IsManager)
        {
            var properties = GateScope.Apply(db, caller);
            query = db.BiometricEnrollments.AsNoTracking().Where(item => properties.Any(property => property.Id == item.PropertyId));
        }
        else
        {
            return [];
        }

        return await (from item in query
                      join property in db.Properties.AsNoTracking() on item.PropertyId equals property.Id
                      orderby item.CreatedAt descending
                      select new BiometricEnrollmentDto(item.Id, property.Name, item.TenantId, item.GatePassId,
                          item.Method, item.Status, item.ConsentAt, item.RetentionUntil))
            .ToListAsync(ct);
    }

    public async Task<int> EnrollAsync(
        BiometricMethod method, string? templateRef, string? publicKey, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
        var lease = await (from member in db.ContractMembers.AsNoTracking()
                           join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
                           join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                           join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                           where tenant.UserId == caller.UserId && contract.Status == ContractStatus.Approved
                               && contract.StartDate <= today && contract.EndDate >= today
                           select new { TenantId = tenant.Id, room.PropertyId })
            .FirstOrDefaultAsync(ct);

        if (lease is null) throw new InvalidOperationException(GateErrorCodes.TenantNoActiveContract);

        var existing = await db.BiometricEnrollments.FirstOrDefaultAsync(item =>
            item.TenantId == lease.TenantId && item.Method == method && item.Status != EnrollmentStatus.Revoked, ct);
        if (existing is not null) throw new InvalidOperationException(GateErrorCodes.InvalidStatus);

        var now = clock.GetUtcNow();
        var enrollment = new BiometricEnrollment
        {
            PropertyId = lease.PropertyId,
            TenantId = lease.TenantId,
            Method = method,
            TemplateRef = templateRef?.Trim(),
            PublicKey = publicKey?.Trim(),
            ConsentAt = now,
            EnrolledByUserId = caller.UserId,
            Status = EnrollmentStatus.Pending,
            RetentionUntil = now.AddYears(1),
            CreatedAt = now,
        };
        db.BiometricEnrollments.Add(enrollment);
        await db.SaveChangesAsync(ct);

        var ownerId = await db.Properties.AsNoTracking()
            .Where(property => property.Id == lease.PropertyId).Select(property => property.OwnerId).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            await notifications.NotifyAsync(ownerId, "Đăng ký sinh trắc mới",
                $"Có đăng ký {MethodLabel(method)} đang chờ duyệt.", "/gate/settings");
        }

        await audit.RecordAsync(AuditLogCategory.Business, "BiometricEnrollmentRequested", caller.UserId,
            lease.PropertyId, metadata: $"enrollment:{enrollment.Id};method:{method}", cancellationToken: ct);
        return enrollment.Id;
    }

    public async Task<bool> ApproveAsync(int id, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var enrollment = await db.BiometricEnrollments.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (enrollment is null) return false;
        if (!await GateScope.CanAccessAsync(db, enrollment.PropertyId, caller, ct)) return false;
        if (enrollment.Status != EnrollmentStatus.Pending) return false;

        enrollment.Status = EnrollmentStatus.Active;
        enrollment.ReviewedByUserId = caller.UserId;
        enrollment.ReviewedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(AuditLogCategory.Business, "BiometricEnrollmentApproved", caller.UserId,
            enrollment.PropertyId, metadata: $"enrollment:{enrollment.Id}", cancellationToken: ct);
        return true;
    }

    public async Task<bool> RejectAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return false;
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var enrollment = await db.BiometricEnrollments.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (enrollment is null) return false;
        if (!await GateScope.CanAccessAsync(db, enrollment.PropertyId, caller, ct)) return false;
        if (enrollment.Status != EnrollmentStatus.Pending) return false;

        enrollment.Status = EnrollmentStatus.Rejected;
        enrollment.ReviewedByUserId = caller.UserId;
        enrollment.ReviewedAt = clock.GetUtcNow();
        enrollment.RevokeReason = reason.Trim();
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(AuditLogCategory.Business, "BiometricEnrollmentRejected", caller.UserId,
            enrollment.PropertyId, metadata: $"enrollment:{enrollment.Id}", cancellationToken: ct);
        return true;
    }

    public async Task<bool> RevokeAsync(int id, string reason, PropertyCaller caller, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) return false;
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var enrollment = await db.BiometricEnrollments.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (enrollment is null) return false;

        var isOwner = await GateScope.IsOwnerAsync(db, enrollment.PropertyId, caller, ct);
        var isTenant = enrollment.EnrolledByUserId == caller.UserId;
        if (!isOwner && !isTenant) return false;
        if (enrollment.Status == EnrollmentStatus.Revoked) return false;

        var now = clock.GetUtcNow();
        enrollment.Status = EnrollmentStatus.Revoked;
        enrollment.RevokedAt = now;
        enrollment.RevokeReason = reason.Trim();
        enrollment.RetentionUntil = now;
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(AuditLogCategory.Business, "BiometricEnrollmentRevoked", caller.UserId,
            enrollment.PropertyId, metadata: $"enrollment:{enrollment.Id}", cancellationToken: ct);
        return true;
    }

    private static async Task<int[]> TenantIdsAsync(ApplicationDbContext db, string userId, CancellationToken ct) =>
        await (from member in db.ContractMembers.AsNoTracking()
               join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
               where tenant.UserId == userId
               select tenant.Id).Distinct().ToArrayAsync(ct);

    private static string MethodLabel(BiometricMethod method) => method switch
    {
        BiometricMethod.Face => "khuôn mặt",
        BiometricMethod.Fingerprint => "vân tay",
        BiometricMethod.Passkey => "passkey",
        _ => "sinh trắc",
    };
}
