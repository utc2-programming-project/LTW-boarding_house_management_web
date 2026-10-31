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
            return properties.Where(property => assignedPropertyIds.Contains(property.Id));
        }

        return properties.Where(_ => false);
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
