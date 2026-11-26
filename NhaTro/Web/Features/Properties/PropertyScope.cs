using Web.Features.Properties.Entities;

namespace Web.Features.Properties;

// Phạm vi nhà trọ:
//  - Chủ trọ: chỉ nhà có OwnerId của mình (VHT-6 3c).
//  - Quản lý: chỉ nhà được phân công đang hiệu lực (VHT-24).
//  - Vai trò khác: không thấy gì.
public static class PropertyScope
{
    public static bool IsOwner(string ownerId, string userId) => ownerId == userId;

    public static IQueryable<Property> Apply(
        IQueryable<Property> properties,
        IQueryable<PropertyManager> assignments,
        PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return properties.Where(property => property.OwnerId == caller.UserId);
        }

        if (caller.IsQuanLy)
        {
            var assignedPropertyIds = assignments
                .Where(a => a.ManagerUserId == caller.UserId && a.RevokedAt == null)
                .Select(a => a.PropertyId);
            var scoped = properties.Where(property => assignedPropertyIds.Contains(property.Id));
            var blocked = caller.BlockedOwnerIds;
            return blocked.Count == 0
                ? scoped
                : scoped.Where(property => !blocked.Contains(property.OwnerId));
        }

        return properties.Where(_ => false);
    }

    /// <summary>
    /// PLAN-001 #7: id nhà Quản lý được phân công, đã loại nhà của chủ trọ đang bị khoá.
    /// Dùng chung cho mọi query lọc theo phân công để không lộ dữ liệu tòa bị khoá.
    /// </summary>
    public static IQueryable<int> ManagerPropertyIds(
        IQueryable<PropertyManager> assignments,
        IQueryable<Property> properties,
        PropertyCaller caller)
    {
        var blocked = caller.BlockedOwnerIds;
        if (blocked.Count == 0)
        {
            return assignments
                .Where(a => a.ManagerUserId == caller.UserId && a.RevokedAt == null)
                .Select(a => a.PropertyId);
        }

        return from assignment in assignments
               join property in properties on assignment.PropertyId equals property.Id
               where assignment.ManagerUserId == caller.UserId
                     && assignment.RevokedAt == null
                     && !blocked.Contains(property.OwnerId)
               select assignment.PropertyId;
    }

    public static void EnsureOwnerCanAccess(string ownerId, string userId)
    {
        if (IsOwner(ownerId, userId))
        {
            return;
        }

        throw new UnauthorizedAccessException("Ngoai pham vi nha tro cua ban.");
    }
}
