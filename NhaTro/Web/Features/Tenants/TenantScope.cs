namespace Web.Features.Tenants;

// Pham vi khach: KhachThue chi thay ho so co UserId cua minh.
// ChuTro/QuanLy theo lien ket thue do VHT-25 lam, o day chi mo cho KhachThue tu xem.
public static class TenantScope
{
    public static void EnsureCanAccess(string? tenantUserId, string userId, string role)
    {
        if (role == Accounts.Roles.KhachThue && tenantUserId == userId)
        {
            return;
        }

        throw new UnauthorizedAccessException("Ngoai pham vi ho so khach thue cua ban.");
    }
}
