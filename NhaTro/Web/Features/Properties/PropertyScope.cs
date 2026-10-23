using Web.Features.Properties.Entities;

namespace Web.Features.Properties;

// Phạm vi nhà trọ: Chủ trọ chỉ thấy nhà có OwnerId của mình (VHT-6 3c).
// Quản lý tạm xem toàn hệ thống tới khi VHT-24 bổ sung bảng phân công.
public static class PropertyScope
{
    public static bool IsOwner(string ownerId, string userId) => ownerId == userId;

    public static IQueryable<Property> Apply(IQueryable<Property> properties, PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return properties.Where(property => property.OwnerId == caller.UserId);
        }

        return caller.IsQuanLy ? properties : properties.Where(_ => false);
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
