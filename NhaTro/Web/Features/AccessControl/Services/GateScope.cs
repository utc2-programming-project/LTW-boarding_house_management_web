using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Properties;

namespace Web.Features.AccessControl.Services;

/// <summary>Kiểm tra phạm vi nhà cho module ra vào cổng (server-side, không tin client).</summary>
public static class GateScope
{
    public static IQueryable<Web.Features.Properties.Entities.Property> Apply(
        ApplicationDbContext db, PropertyCaller caller) =>
        PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), caller);

    public static Task<bool> CanAccessAsync(ApplicationDbContext db, int propertyId, PropertyCaller caller, CancellationToken ct = default) =>
        Apply(db, caller).AnyAsync(property => property.Id == propertyId, ct);

    public static async Task<bool> IsOwnerAsync(ApplicationDbContext db, int propertyId, PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsChuTro) return false;
        return await db.Properties.AsNoTracking()
            .AnyAsync(property => property.Id == propertyId && property.OwnerId == caller.UserId, ct);
    }

    public static async Task<bool> IsAssignedManagerAsync(ApplicationDbContext db, int propertyId, PropertyCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsQuanLy) return false;
        return await db.PropertyManagers.AsNoTracking()
            .AnyAsync(manager => manager.PropertyId == propertyId && manager.ManagerUserId == caller.UserId && manager.RevokedAt == null, ct);
    }
}
