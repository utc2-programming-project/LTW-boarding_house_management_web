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

    private static Task<bool> OwnsPropertyAsync(
        ApplicationDbContext db, int propertyId, string ownerUserId, CancellationToken ct) =>
        db.Properties.AnyAsync(p => p.Id == propertyId && p.OwnerId == ownerUserId, ct);

    private static Task<bool> IsInRoleAsync(
        ApplicationDbContext db, string userId, string roleName, CancellationToken ct) =>
        (from userRole in db.UserRoles
         join role in db.Roles on userRole.RoleId equals role.Id
         where userRole.UserId == userId && role.Name == roleName
         select userRole.UserId)
        .AnyAsync(ct);
}
