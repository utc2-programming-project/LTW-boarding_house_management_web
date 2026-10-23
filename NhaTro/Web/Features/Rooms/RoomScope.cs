using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;

namespace Web.Features.Rooms;

// Pham vi phong: suy ra qua Property.OwnerId (Room chi co PropertyId).
// Áp dụng điều kiện trực tiếp lên query để SQL chỉ trả các phòng trong phạm vi.
public static class RoomScope
{
    public static IQueryable<Room> Apply(
        IQueryable<Room> rooms,
        IQueryable<Property> properties,
        PropertyCaller caller)
    {
        if (caller.IsQuanLy)
        {
            return rooms;
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
