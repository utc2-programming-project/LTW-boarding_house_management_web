using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Entities;

namespace Web.Features.Properties.Services;

/// <summary>
/// VHT-24 — gán/bỏ gán và đọc phân công Quản lý theo tòa nhà trên SQL.
/// Chỉ Chủ trọ sở hữu tòa nhà được thao tác; tài khoản được gán phải đang giữ vai trò Quản lý.
/// </summary>
public sealed class PropertyManagerService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock) : IPropertyManagerService
{
    public async Task<PropertyManagerListResult> ListAssignmentsAsync(
        int propertyId,
        PropertyCaller caller,
        bool includeHistory = false,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return PropertyManagerListResult.Failure(
                PropertyManagerErrorCodes.Forbidden, PropertyManagerErrorMessages.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await OwnsPropertyAsync(db, propertyId, caller.UserId, ct))
        {
            return PropertyManagerListResult.Failure(
                PropertyManagerErrorCodes.PropertyNotFound, PropertyManagerErrorMessages.PropertyNotFound);
        }

        var query = db.PropertyManagers.AsNoTracking().Where(a => a.PropertyId == propertyId);
        if (!includeHistory)
        {
            query = query.Where(a => a.RevokedAt == null);
        }

        var items = await (
                from assignment in query
                join user in db.Users.AsNoTracking() on assignment.ManagerUserId equals user.Id into users
                from user in users.DefaultIfEmpty()
                orderby assignment.AssignedAt descending, assignment.Id descending
                select new PropertyManagerItem(
                    assignment.PropertyId,
                    assignment.ManagerUserId,
                    user != null ? user.Email : null,
                    assignment.AssignedAt,
                    assignment.AssignedByUserId,
                    assignment.RevokedAt,
                    assignment.RevokedByUserId))
            .ToListAsync(ct);

        return PropertyManagerListResult.Success(items);
    }

    public async Task<PropertyManagerCandidateListResult> ListCandidatesAsync(
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return PropertyManagerCandidateListResult.Failure(
                PropertyManagerErrorCodes.Forbidden, PropertyManagerErrorMessages.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await OwnsPropertyAsync(db, propertyId, caller.UserId, ct))
        {
            return PropertyManagerCandidateListResult.Failure(
                PropertyManagerErrorCodes.PropertyNotFound, PropertyManagerErrorMessages.PropertyNotFound);
        }

        var assignedUserIds = db.PropertyManagers
            .Where(a => a.PropertyId == propertyId && a.RevokedAt == null)
            .Select(a => a.ManagerUserId);

        // Mỗi Quản lý chỉ thuộc một Chủ trọ: loại khỏi danh sách ứng viên những
        // tài khoản đang được phân công cho tòa nhà của Chủ trọ khác.
        var managerIdsOfOtherOwners = from assignment in db.PropertyManagers
                                      join property in db.Properties on assignment.PropertyId equals property.Id
                                      where assignment.RevokedAt == null
                                          && property.OwnerId != caller.UserId
                                      select assignment.ManagerUserId;

        var candidates = await (
                from userRole in db.UserRoles
                join role in db.Roles on userRole.RoleId equals role.Id
                join user in db.Users on userRole.UserId equals user.Id
                where role.Name == Roles.QuanLy
                    && !assignedUserIds.Contains(user.Id)
                    && !managerIdsOfOtherOwners.Contains(user.Id)
                orderby user.Email
                select new PropertyManagerCandidate(user.Id, user.Email, user.FullName))
            .ToListAsync(ct);

        return PropertyManagerCandidateListResult.Success(candidates);
    }

    public async Task<PropertyManagerResult> AssignAsync(
        int propertyId,
        string managerUserId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.Forbidden, PropertyManagerErrorMessages.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(managerUserId))
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.ManagerNotFound, PropertyManagerErrorMessages.ManagerNotFound);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await OwnsPropertyAsync(db, propertyId, caller.UserId, ct))
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.PropertyNotFound, PropertyManagerErrorMessages.PropertyNotFound);
        }

        var manager = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == managerUserId, ct);
        if (manager is null)
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.ManagerNotFound, PropertyManagerErrorMessages.ManagerNotFound);
        }

        if (!await IsInRoleAsync(db, managerUserId, Roles.QuanLy, ct))
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.InvalidManagerRole, PropertyManagerErrorMessages.InvalidManagerRole);
        }

        if (await HasAssignmentWithAnotherOwnerAsync(db, managerUserId, caller.UserId, ct))
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.ManagerOwnedByAnotherOwner,
                PropertyManagerErrorMessages.ManagerOwnedByAnotherOwner);
        }

        var existing = await db.PropertyManagers
            .FirstOrDefaultAsync(
                a => a.PropertyId == propertyId && a.ManagerUserId == managerUserId && a.RevokedAt == null,
                ct);
        if (existing is not null)
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.AlreadyAssigned, PropertyManagerErrorMessages.AlreadyAssigned);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var entity = new PropertyManager
        {
            PropertyId = propertyId,
            ManagerUserId = managerUserId,
            AssignedAt = now,
            AssignedByUserId = caller.UserId,
        };
        db.PropertyManagers.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Đua tranh hai request gán cùng lúc: unique filtered index chặn.
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.OperationFailed, PropertyManagerErrorMessages.OperationFailed);
        }

        return PropertyManagerResult.Success(new PropertyManagerItem(
            propertyId, managerUserId, manager.Email, now, caller.UserId, null, null));
    }

    public async Task<PropertyManagerResult> RevokeAsync(
        int propertyId,
        string managerUserId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.Forbidden, PropertyManagerErrorMessages.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await OwnsPropertyAsync(db, propertyId, caller.UserId, ct))
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.PropertyNotFound, PropertyManagerErrorMessages.PropertyNotFound);
        }

        var entity = await db.PropertyManagers
            .FirstOrDefaultAsync(
                a => a.PropertyId == propertyId && a.ManagerUserId == managerUserId && a.RevokedAt == null,
                ct);
        if (entity is null)
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.NotAssigned, PropertyManagerErrorMessages.NotAssigned);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        entity.RevokedAt = now;
        entity.RevokedByUserId = caller.UserId;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return PropertyManagerResult.Failure(
                PropertyManagerErrorCodes.OperationFailed, PropertyManagerErrorMessages.OperationFailed);
        }

        return PropertyManagerResult.Success(new PropertyManagerItem(
            propertyId, managerUserId, null, entity.AssignedAt, entity.AssignedByUserId, now, caller.UserId));
    }

    public async Task<PropertyManagerListResult> ListAssignmentsForManagerAsync(
        string managerUserId,
        PropertyCaller caller,
        bool includeHistory = false,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return PropertyManagerListResult.Failure(
                PropertyManagerErrorCodes.Forbidden, PropertyManagerErrorMessages.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var query =
            from assignment in db.PropertyManagers.AsNoTracking()
            join property in db.Properties.AsNoTracking() on assignment.PropertyId equals property.Id
            where assignment.ManagerUserId == managerUserId && property.OwnerId == caller.UserId
            select new { assignment, property };

        if (!includeHistory)
        {
            query = query.Where(row => row.assignment.RevokedAt == null);
        }

        var items = await (
                from row in query
                join user in db.Users.AsNoTracking() on row.assignment.ManagerUserId equals user.Id into users
                from user in users.DefaultIfEmpty()
                orderby row.property.Name, row.assignment.AssignedAt descending
                select new PropertyManagerItem(
                    row.assignment.PropertyId,
                    row.assignment.ManagerUserId,
                    user != null ? user.Email : null,
                    row.assignment.AssignedAt,
                    row.assignment.AssignedByUserId,
                    row.assignment.RevokedAt,
                    row.assignment.RevokedByUserId,
                    row.property.Name,
                    row.property.Address))
            .ToListAsync(ct);

        return PropertyManagerListResult.Success(items);
    }

    public async Task<PropertyManagerPropertyListResult> ListAssignablePropertiesAsync(
        string managerUserId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return PropertyManagerPropertyListResult.Failure(
                PropertyManagerErrorCodes.Forbidden, PropertyManagerErrorMessages.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(managerUserId))
        {
            return PropertyManagerPropertyListResult.Failure(
                PropertyManagerErrorCodes.ManagerNotFound, PropertyManagerErrorMessages.ManagerNotFound);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == managerUserId, ct))
        {
            return PropertyManagerPropertyListResult.Failure(
                PropertyManagerErrorCodes.ManagerNotFound, PropertyManagerErrorMessages.ManagerNotFound);
        }

        if (!await IsInRoleAsync(db, managerUserId, Roles.QuanLy, ct))
        {
            return PropertyManagerPropertyListResult.Failure(
                PropertyManagerErrorCodes.InvalidManagerRole, PropertyManagerErrorMessages.InvalidManagerRole);
        }

        if (await HasAssignmentWithAnotherOwnerAsync(db, managerUserId, caller.UserId, ct))
        {
            return PropertyManagerPropertyListResult.Failure(
                PropertyManagerErrorCodes.ManagerOwnedByAnotherOwner,
                PropertyManagerErrorMessages.ManagerOwnedByAnotherOwner);
        }

        var assignedPropertyIds = db.PropertyManagers
            .Where(a => a.ManagerUserId == managerUserId && a.RevokedAt == null)
            .Select(a => a.PropertyId);

        var items = await db.Properties.AsNoTracking()
            .Where(p => p.OwnerId == caller.UserId && !p.IsDeleted && !assignedPropertyIds.Contains(p.Id))
            .OrderBy(p => p.Name)
            .Select(p => new PropertyManagerPropertyOption(p.Id, p.Name, p.Address))
            .ToListAsync(ct);

        return PropertyManagerPropertyListResult.Success(items);
    }

    public async Task<ManagerOwnerResult> GetOwnerForManagerAsync(
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsQuanLy)
        {
            return ManagerOwnerResult.Failure(
                PropertyManagerErrorCodes.Forbidden, PropertyManagerErrorMessages.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(ct);

        var rows = await (
                from assignment in db.PropertyManagers.AsNoTracking()
                join property in db.Properties.AsNoTracking() on assignment.PropertyId equals property.Id
                join owner in db.Users.AsNoTracking() on property.OwnerId equals owner.Id
                where assignment.ManagerUserId == caller.UserId && assignment.RevokedAt == null
                select new { owner.Id, owner.FullName, owner.Email })
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return ManagerOwnerResult.Success(null);
        }

        var first = rows[0];
        return ManagerOwnerResult.Success(
            new ManagerOwnerInfo(first.Id, first.FullName, first.Email, rows.Count));
    }

    private static Task<bool> OwnsPropertyAsync(
        ApplicationDbContext db, int propertyId, string ownerUserId, CancellationToken ct) =>
        db.Properties.AnyAsync(p => p.Id == propertyId && p.OwnerId == ownerUserId, ct);

    private static Task<bool> HasAssignmentWithAnotherOwnerAsync(
        ApplicationDbContext db, string managerUserId, string ownerUserId, CancellationToken ct) =>
        (from assignment in db.PropertyManagers
         join property in db.Properties on assignment.PropertyId equals property.Id
         where assignment.ManagerUserId == managerUserId
             && assignment.RevokedAt == null
             && property.OwnerId != ownerUserId
         select assignment.Id)
        .AnyAsync(ct);

    private static Task<bool> IsInRoleAsync(
        ApplicationDbContext db, string userId, string roleName, CancellationToken ct) =>
        (from userRole in db.UserRoles
         join role in db.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == roleName
         select userRole.UserId)
        .AnyAsync(ct);
}
