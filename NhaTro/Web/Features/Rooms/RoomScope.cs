namespace Web.Features.Rooms;

// Pham vi phong: suy ra qua Property.OwnerId (Room chi co PropertyId).
// Service nhan roomId/propertyOwnerId phai goi ham nay truoc khi doc/ghi DB.
public static class RoomScope
{
    public static void EnsureCanAccess(string propertyOwnerId, string userId, string role)
    {
        if (role == Accounts.Roles.ChuTro && propertyOwnerId == userId)
        {
            return;
        }

        throw new UnauthorizedAccessException("Ngoai pham vi phong cua ban.");
    }
}
