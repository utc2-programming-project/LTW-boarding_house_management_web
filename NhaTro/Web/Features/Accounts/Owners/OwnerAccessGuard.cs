using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Data;

namespace Web.Features.Accounts.Owners;

/// <summary>
/// PLAN-025: chủ trọ bị khoá thì các ngữ cảnh Quản lý/Khách thuê thuộc chủ trọ đó
/// cũng bị chặn. Ngữ cảnh khác (chủ trọ khác) vẫn dùng được.
/// </summary>
public sealed class OwnerAccessGuard(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> users,
    ILogger<OwnerAccessGuard> logger)
{
    public async Task<bool> HasAnyAccessibleContextAsync(
        string userId, IEnumerable<string> roles, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var blocked = await BlockedOwnerIdsAsync(db, ct);
        if (blocked.Count == 0)
        {
            return true;
        }

        foreach (var role in roles)
        {
            if (role is Roles.Admin or Roles.ChuTro)
            {
                return true;
            }

            if (role is Roles.QuanLy or Roles.KhachThue
                && !await IsRoleBlockedAsync(db, userId, role, blocked, ct))
            {
                return true;
            }
        }

        return false;
    }

    public async Task<bool> IsRoleBlockedAsync(
        string userId, string role, CancellationToken ct = default)
    {
        if (role is not (Roles.QuanLy or Roles.KhachThue))
        {
            return false;
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var blocked = await BlockedOwnerIdsAsync(db, ct);
        if (blocked.Count == 0)
        {
            return false;
        }

        return await IsRoleBlockedAsync(db, userId, role, blocked, ct);
    }

    private static async Task<bool> IsRoleBlockedAsync(
        ApplicationDbContext db,
        string userId,
        string role,
        IReadOnlySet<string> blocked,
        CancellationToken ct)
    {
        var ownerIds = role == Roles.QuanLy
            ? await ManagerOwnerIdsAsync(db, userId, ct)
            : await TenantOwnerIdsAsync(db, userId, ct);

        // Chỉ cần một chủ trọ bị khoá là ngữ cảnh đó bị chặn; tránh lộ dữ liệu tòa bị khoá.
        return ownerIds.Count > 0 && ownerIds.Any(blocked.Contains);
    }

    public async Task<IReadOnlyList<string>> GetAffectedUserIdsAsync(
        string ownerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var managerIds =
            from assignment in db.PropertyManagers.AsNoTracking()
            join property in db.Properties.AsNoTracking() on assignment.PropertyId equals property.Id
            where assignment.RevokedAt == null && property.OwnerId == ownerId
            select assignment.ManagerUserId;

        var tenantUserIds =
            from tenant in db.Tenants.AsNoTracking()
            join link in db.TenantOwnerLinks.AsNoTracking() on tenant.Id equals link.TenantId
            where link.OwnerId == ownerId && tenant.UserId != null
            select tenant.UserId!;

        var contractUserIds =
            from tenant in db.Tenants.AsNoTracking()
            join member in db.ContractMembers.AsNoTracking() on tenant.Id equals member.TenantId
            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            where property.OwnerId == ownerId && tenant.UserId != null
            select tenant.UserId!;

        return await managerIds
            .Concat(tenantUserIds)
            .Concat(contractUserIds)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>Huỷ phiên đang mở của quản lý/khách thuộc chủ trọ bị khoá.</summary>
    public async Task CascadeLockAsync(string ownerId, CancellationToken ct = default)
    {
        var affected = await GetAffectedUserIdsAsync(ownerId, ct);
        foreach (var userId in affected)
        {
            var user = await users.FindByIdAsync(userId);
            if (user is null)
            {
                continue;
            }

            var result = await users.UpdateSecurityStampAsync(user);
            if (!result.Succeeded)
            {
                logger.LogWarning(
                    "Không cập nhật được security stamp cho {UserId} khi khoá chủ trọ {OwnerId}.",
                    userId, ownerId);
            }
        }
    }

    public async Task RevokeOpenInvitationsAsync(string ownerId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.TenantInvitations
            .Where(invitation => invitation.OwnerId == ownerId
                && invitation.AcceptedAt == null
                && invitation.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(invitation => invitation.RevokedAt, (DateTime?)now), ct);
    }

    private static async Task<HashSet<string>> BlockedOwnerIdsAsync(
        ApplicationDbContext db, CancellationToken ct)
    {
        var permanent = AccountStatusRules.PermanentLockout - TimeSpan.FromMinutes(1);
        var ids = await (
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where role.Name == Roles.ChuTro
                && !user.IsDeleted
                && user.LockoutEnd != null
                && user.LockoutEnd >= permanent
            select user.Id)
            .ToListAsync(ct);

        return new HashSet<string>(ids, StringComparer.Ordinal);
    }

    private static async Task<List<string>> OwnerIdsForUserAsync(
        ApplicationDbContext db, string userId, CancellationToken ct)
    {
        var managerOwners = await ManagerOwnerIdsAsync(db, userId, ct);
        var tenantOwners = await TenantOwnerIdsAsync(db, userId, ct);
        return managerOwners.Concat(tenantOwners).Distinct().ToList();
    }

    private static async Task<List<string>> ManagerOwnerIdsAsync(
        ApplicationDbContext db, string userId, CancellationToken ct) =>
        await (
            from assignment in db.PropertyManagers.AsNoTracking()
            join property in db.Properties.AsNoTracking() on assignment.PropertyId equals property.Id
            where assignment.ManagerUserId == userId && assignment.RevokedAt == null
            select property.OwnerId)
            .Distinct()
            .ToListAsync(ct);

    private static async Task<List<string>> TenantOwnerIdsAsync(
        ApplicationDbContext db, string userId, CancellationToken ct)
    {
        var linked =
            from tenant in db.Tenants.AsNoTracking()
            join link in db.TenantOwnerLinks.AsNoTracking() on tenant.Id equals link.TenantId
            where tenant.UserId == userId
            select link.OwnerId;

        var contracted =
            from tenant in db.Tenants.AsNoTracking()
            join member in db.ContractMembers.AsNoTracking() on tenant.Id equals member.TenantId
            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            where tenant.UserId == userId
            select property.OwnerId;

        return await linked.Concat(contracted).Distinct().ToListAsync(ct);
    }
}
