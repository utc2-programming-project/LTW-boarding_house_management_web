namespace Web.Features.Properties;

// Pham vi nha tro: ChuTro chi thay nha co OwnerId cua minh (VHT-6 3c).
// QuanLy theo phan cong toa nha do VHT-24 lam, o day tu choi de khong vo tinh mo rong.
public static class PropertyScope
{
    public static bool IsOwner(string ownerId, string userId) => ownerId == userId;

    public static void EnsureCanAccess(string ownerId, string userId, string role)
    {
        if (role == Accounts.Roles.ChuTro && IsOwner(ownerId, userId))
        {
            return;
        }

        throw new UnauthorizedAccessException("Ngoai pham vi nha tro cua ban.");
    }

    // Loc danh sach theo pham vi: tra ve dieu kien OwnerId, service VHT-7 dung de khong phai load het vao RAM.
    public static string OwnerFilter(string userId) => userId;
}
