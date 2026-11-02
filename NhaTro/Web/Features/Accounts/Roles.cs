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

    /// <summary>
    /// VHT-119: quy tắc chọn vai trò duy nhất cho mọi tầng (Admin → ChuTro → QuanLy → KhachThue).
    /// Hệ thống vốn chỉ một vai trò/tài khoản; nếu dữ liệu lệch nhiều vai trò thì Chủ trọ thắng
    /// để không vỡ gắn/gỡ tài khoản và xem CCCD (TenantService.EnsureOwner, TenantCaller.CanRevealIdNumber).
    /// </summary>
    public static string ResolvePrimaryRole(IPrincipal user) =>
        All.FirstOrDefault(user.IsInRole) ?? string.Empty;
}
