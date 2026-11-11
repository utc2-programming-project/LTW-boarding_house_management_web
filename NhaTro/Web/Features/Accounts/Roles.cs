using System.Security.Claims;
using System.Security.Principal;

namespace Web.Features.Accounts;

// Ten vai tro chot theo docs/tech-stack (VHT-4/VHT-6).
// Dung ten ASCII on dinh de lưu trong AspNetRoles, hien thi tieng Viet o UI.
public static class Roles
{
    public const string Admin = "Admin";
    public const string ChuTro = "ChuTro";
    public const string QuanLy = "QuanLy";
    public const string KhachThue = "KhachThue";

    public static readonly string[] All = [Admin, ChuTro, QuanLy, KhachThue];

    /// <summary>Claim ngữ cảnh đang hoạt động (PLAN-023).</summary>
    public const string ActiveRoleClaim = "nhatro.active_role";

    /// <summary>
    /// VHT-119 + PLAN-023: ưu tiên ngữ cảnh đang chọn trong claim; nếu chưa có thì
    /// fallback thứ tự Admin → ChuTro → QuanLy → KhachThue cho dữ liệu cũ.
    /// </summary>
    public static string ResolvePrimaryRole(IPrincipal user)
    {
        var active = (user as ClaimsPrincipal)?.FindFirst(ActiveRoleClaim)?.Value;
        if (!string.IsNullOrWhiteSpace(active) && All.Contains(active))
        {
            return active;
        }

        return All.FirstOrDefault(user.IsInRole) ?? string.Empty;
    }
}
