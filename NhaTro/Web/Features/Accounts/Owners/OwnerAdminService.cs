using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;

namespace Web.Features.Accounts.Owners;

/// <summary>PLAN-025: Admin xem, khoá/mở chủ trọ và khoá lan theo ngữ cảnh.</summary>
public sealed class OwnerAdminService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    UserManager<ApplicationUser> users,
    OwnerAccessGuard accessGuard,
    AuditLogService audit,
    ILogger<OwnerAdminService> logger)
{
    public async Task<(IReadOnlyList<OwnerListItemDto> Items, int TotalCount)> ListAsync(
        string? keyword, int page, int pageSize, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var query =
            from user in db.Users.AsNoTracking()
            join userRole in db.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
            join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where role.Name == Roles.ChuTro && !user.IsDeleted
            select user;

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = keyword.Trim();
            query = query.Where(user =>
                (user.FullName != null && user.FullName.Contains(term))
                || (user.Email != null && user.Email.Contains(term))
                || (user.PhoneNumber != null && user.PhoneNumber.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var owners = await query
            .OrderBy(user => user.FullName ?? user.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var items = new List<OwnerListItemDto>(owners.Count);
        foreach (var owner in owners)
        {
            var impact = await GetImpactAsync(db, owner.Id, ct);
            items.Add(new OwnerListItemDto(
                owner.Id,
                owner.FullName,
                owner.Email,
                owner.PhoneNumber,
                AccountStatusRules.IsPermanentlyLocked(owner.LockoutEnd),
                impact.PropertyCount,
                impact.ManagerCount,
                impact.TenantCount,
                owner.ApprovedAt));
        }

        return (items, total);
    }

    public async Task<OwnerImpactDto> GetImpactAsync(
        string ownerId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await GetImpactAsync(db, ownerId, ct);
    }

    public async Task<OwnerAdminResult> LockAsync(
        string ownerId, string actorUserId, CancellationToken ct = default)
    {
        var owner = await users.FindByIdAsync(ownerId);
        if (owner is null)
        {
            return OwnerAdminResult.Failure("OwnerNotFound", "Không tìm thấy chủ trọ.");
        }

        var roles = await users.GetRolesAsync(owner);
        if (!roles.Contains(Roles.ChuTro))
        {
            return OwnerAdminResult.Failure("NotOwner", "Tài khoản không phải chủ trọ.");
        }

        owner.LockoutEnabled = true;
        owner.LockoutEnd = AccountStatusRules.PermanentLockout;
        var updated = await users.UpdateAsync(owner);
        if (!updated.Succeeded)
        {
            return OwnerAdminResult.Failure("UpdateFailed", "Không khoá được tài khoản.");
        }

        await users.UpdateSecurityStampAsync(owner);
        await accessGuard.CascadeLockAsync(ownerId, ct);
        await accessGuard.RevokeOpenInvitationsAsync(ownerId, ct);
        await audit.RecordAsync(
            AuditLogCategory.Account,
            "OwnerLocked",
            actorUserId,
            metadata: $"owner:{ownerId}",
            cancellationToken: ct);

        logger.LogInformation("Admin {Actor} khoá chủ trọ {OwnerId}.", actorUserId, ownerId);
        return OwnerAdminResult.Success();
    }

    public async Task<OwnerAdminResult> UnlockAsync(
        string ownerId, string actorUserId, CancellationToken ct = default)
    {
        var owner = await users.FindByIdAsync(ownerId);
        if (owner is null)
        {
            return OwnerAdminResult.Failure("OwnerNotFound", "Không tìm thấy chủ trọ.");
        }

        var roles = await users.GetRolesAsync(owner);
        if (!roles.Contains(Roles.ChuTro))
        {
            return OwnerAdminResult.Failure("NotOwner", "Tài khoản không phải chủ trọ.");
        }

        owner.LockoutEnd = null;
        owner.AccessFailedCount = 0;
        var updated = await users.UpdateAsync(owner);
        if (!updated.Succeeded)
        {
            return OwnerAdminResult.Failure("UpdateFailed", "Không mở khoá được tài khoản.");
        }

        await audit.RecordAsync(
            AuditLogCategory.Account,
            "OwnerUnlocked",
            actorUserId,
            metadata: $"owner:{ownerId}",
            cancellationToken: ct);

        logger.LogInformation("Admin {Actor} mở khoá chủ trọ {OwnerId}.", actorUserId, ownerId);
        return OwnerAdminResult.Success();
    }

    private static async Task<OwnerImpactDto> GetImpactAsync(
        ApplicationDbContext db, string ownerId, CancellationToken ct)
    {
        var propertyCount = await db.Properties.AsNoTracking()
            .CountAsync(property => property.OwnerId == ownerId && !property.IsDeleted, ct);

        var managerCount = await (
            from assignment in db.PropertyManagers.AsNoTracking()
            join property in db.Properties.AsNoTracking() on assignment.PropertyId equals property.Id
            where assignment.RevokedAt == null && property.OwnerId == ownerId
            select assignment.ManagerUserId)
            .Distinct()
            .CountAsync(ct);

        var linkedTenants =
            from link in db.TenantOwnerLinks.AsNoTracking()
            where link.OwnerId == ownerId
            select link.TenantId;

        var contractedTenants =
            from member in db.ContractMembers.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            where property.OwnerId == ownerId
            select member.TenantId;

        var tenantCount = await linkedTenants
            .Concat(contractedTenants)
            .Distinct()
            .CountAsync(ct);

        return new OwnerImpactDto(ownerId, propertyCount, managerCount, tenantCount);
    }
}
