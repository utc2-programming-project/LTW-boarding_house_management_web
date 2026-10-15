using System.Security.Claims;
using System.Security.Principal;

namespace Web.Features.Accounts;

// Gac cong dung chung VHT-6 (3c). Dat o service, khong chi giau nut o menu.
public static class BusinessGuard
{
    // Moi service nghiep vu P2-P8 goi dau tien: Admin bi tu choi thang.
    public static void EnsureBusinessAllowed(IPrincipal? user)
    {
        if (user is not null && user.IsInRole(Roles.Admin))
        {
            throw new UnauthorizedAccessException("Admin chi quan tri tai khoan/vai tro, khong duoc goi service nghiep vu.");
        }
    }

    // Chi Admin duoc doi vai tro (ChuTro doi la tu choi).
    public static void EnsureCanChangeRole(IPrincipal? user)
    {
        if (user is null || !user.IsInRole(Roles.Admin))
        {
            throw new UnauthorizedAccessException("Chi Admin duoc doi vai tro.");
        }
    }
}

// Luat doi vai tro dang pure de test nhanh khong can DB.
public static class RoleChangeGuard
{
    // Tra ve null neu duoc phep, nguoc lai tra ve ly do tu choi.
    public static string? Check(
        bool callerIsAdmin,
        string callerUserId,
        string targetUserId,
        bool targetIsAdmin,
        bool targetIsLastAdmin,
        string newRole)
    {
        if (!callerIsAdmin)
        {
            return "Chi Admin duoc doi vai tro.";
        }

        if (callerUserId == targetUserId)
        {
            return "Khong duoc tu doi vai tro cua chinh minh.";
        }

        if (!Roles.All.Contains(newRole))
        {
            return "Vai tro moi khong hop le.";
        }

        if (targetIsAdmin && newRole != Roles.Admin && targetIsLastAdmin)
        {
            return "Khong duoc go role Admin cuoi cung.";
        }

        return null;
    }
}

public static class PrincipalExtensions
{
    public static string? UserId(this IPrincipal user) =>
        (user as ClaimsPrincipal)?.FindFirstValue(ClaimTypes.NameIdentifier);
}
