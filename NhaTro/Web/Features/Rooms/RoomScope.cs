using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;

namespace Web.Features.Rooms;

// Pham vi phong: suy ra qua Property (Room chi co PropertyId).
//  - Chủ trọ: phòng thuộc nhà có OwnerId của mình.
//  - Quản lý: phòng thuộc nhà được phân công đang hiệu lực (VHT-24).
//  - Vai trò khác: không thấy gì.
// Áp dụng điều kiện trực tiếp lên query để SQL chỉ trả các phòng trong phạm vi.
public static class RoomScope
{
    public static IQueryable<Room> Apply(
        IQueryable<Room> rooms,
        IQueryable<Property> properties,
        IQueryable<PropertyManager> assignments,
        PropertyCaller caller)
    {
        if (caller.IsQuanLy)
        {
            var assignedPropertyIds = assignments
                .Where(a => a.ManagerUserId == caller.UserId && a.RevokedAt == null)
                .Select(a => a.PropertyId);

            return from room in rooms
                   join property in properties on room.PropertyId equals property.Id
                   where assignedPropertyIds.Contains(property.Id)
                   select room;
        }

        if (!caller.IsChuTro)
        {
            return rooms.Where(_ => false);
        }

        return from room in rooms
               join property in properties on room.PropertyId equals property.Id
               where property.OwnerId == caller.UserId
               select room;
    }

    public static void EnsureOwnerCanAccess(string propertyOwnerId, string userId)
    {
        if (propertyOwnerId == userId)
        {
            return;
        }

        throw new UnauthorizedAccessException("Ngoai pham vi phong cua ban.");
    }
}
