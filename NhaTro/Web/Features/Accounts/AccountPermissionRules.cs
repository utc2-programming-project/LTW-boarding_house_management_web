namespace Web.Features.Accounts;

/// <summary>
/// VHT-8 / D1: luat phan quyen quan tri tai khoan.
/// Tach thanh ham thuan (khong DB, khong HttpContext) de unit test nhanh va de doc.
///
/// D1 (chot 12/10/2026):
///  - Admin   : quan ly toan bo tai khoan, cap/doi vai tro, khoa/mo khoa.
///  - ChuTro  : duyet/kich hoat/khoa QuanLy va KhachThue, tao tai khoan QuanLy. KHONG doi vai tro.
///  - QuanLy  : sua ho so minh, tao tai khoan KhachThue (Cho duyet), xem khach toa nha.
///  - KhachThue: chi ho so ca nhan.
/// </summary>
public static class AccountPermissionRules
{
    /// <summary>Vai tro ma nguoi goi duoc phep gan khi tao/doi vai tro.</summary>
    public static IReadOnlyList<string> AssignableRoles(string callerRole) => callerRole switch
    {
        Roles.Admin => Roles.All,
        // ChuTro tao duoc tai khoan QuanLy nhung khong doi vai tro (xem CanChangeRole).
        Roles.ChuTro => [Roles.QuanLy],
        // QuanLy tao tai khoan KhachThue o trang thai Cho duyet.
        Roles.QuanLy => [Roles.KhachThue],
        _ => [],
    };

    /// <summary>Chi Admin duoc doi vai tro (D1: ChuTro "KHONG doi vai tro").</summary>
    public static bool CanChangeRole(string callerRole) => callerRole == Roles.Admin;

    /// <summary>Vai tro ma nguoi goi duoc phep khoa/mo khoa.</summary>
    public static IReadOnlyList<string> LockableRoles(string callerRole) => callerRole switch
    {
        Roles.Admin => Roles.All,
        // D1: ChuTro chi duyet/kich hoat/khoa QuanLy va KhachThue.
        Roles.ChuTro => [Roles.QuanLy, Roles.KhachThue],
        _ => [],
    };

    /// <summary>Nguoi goi co duoc vao man hinh quan tri tai khoan khong.</summary>
    public static bool CanManageAccounts(string callerRole) =>
        callerRole is Roles.Admin or Roles.ChuTro;

    /// <summary>Vai tro ma nguoi goi duoc phep nhin thay trong danh sach.</summary>
    public static IReadOnlyList<string> VisibleRoles(string callerRole) => callerRole switch
    {
        Roles.Admin => Roles.All,
        Roles.ChuTro => [Roles.QuanLy, Roles.KhachThue],
        // QuanLy chi thay khach trong toa nha minh (D9); pham vi toa nha thuoc VHT-95.
        Roles.QuanLy => [Roles.KhachThue],
        _ => [],
    };

    /// <summary>
    /// Kiem tra mot lan doi vai tro. Tra ve null neu duoc phep, nguoc lai la ma loi.
    /// </summary>
    public static string? CheckChangeRole(
        string callerRole,
        string callerUserId,
        string targetUserId,
        string targetRole,
        bool targetIsLastAdmin,
        string newRole)
    {
        if (!CanChangeRole(callerRole))
        {
            return AccountErrorCodes.Forbidden;
        }

        if (!Roles.All.Contains(newRole))
        {
            return AccountErrorCodes.InvalidRole;
        }

        // Tu doi vai tro minh la con duong tu nang quyen hoac tu khoa minh.
        if (callerUserId == targetUserId)
        {
            return AccountErrorCodes.CannotChangeOwnRole;
        }

        // Go Admin cuoi cung se lam he thong khong con ai quan tri.
        if (targetRole == Roles.Admin && newRole != Roles.Admin && targetIsLastAdmin)
        {
            return AccountErrorCodes.CannotRemoveLastAdmin;
        }

        return null;
    }

    /// <summary>
    /// Kiem tra mot lan khoa/mo khoa. Tra ve null neu duoc phep, nguoc lai la ma loi.
    /// </summary>
    public static string? CheckLock(
        string callerRole,
        string callerUserId,
        string targetUserId,
        string targetRole,
        bool unlock)
    {
        if (unlock)
        {
            // Mo khoa cung phai co quyen quan ly, nhung khong co rui ro tu khoa minh.
            return LockableRoles(callerRole).Contains(targetRole)
                ? null
                : AccountErrorCodes.Forbidden;
        }

        if (!LockableRoles(callerRole).Contains(targetRole))
        {
            // Phan biet ro: ChuTro khoa Admin -> khong duoc phep vi vai tro, khong phai vi thieu quyen.
            return callerRole != Roles.Admin && targetRole == Roles.Admin
                ? AccountErrorCodes.CannotLockAdmin
                : AccountErrorCodes.Forbidden;
        }

        if (callerUserId == targetUserId)
        {
            return AccountErrorCodes.CannotLockSelf;
        }

        return null;
    }

    /// <summary>
    /// VHT-27: chặn tự xoá tài khoản khi đó là Admin cuối cùng (màn hình xoá dữ liệu cá nhân).
    /// Trả về null nếu được phép, ngược lại là mã lỗi.
    /// </summary>
    public static string? CheckSelfDelete(bool targetIsAdmin, bool targetIsLastAdmin)
    {
        return targetIsAdmin && targetIsLastAdmin
            ? AccountErrorCodes.CannotRemoveLastAdmin
            : null;
    }

    /// <summary>Nhan hien thi cua vai tro (dung key .resx, khong tra chuoi da dich o day).</summary>
    public static string RoleResourceKey(string role) => role switch
    {
        Roles.Admin => "RoleAdmin",
        Roles.ChuTro => "RoleChuTro",
        Roles.QuanLy => "RoleQuanLy",
        Roles.KhachThue => "RoleKhachThue",
        _ => "RoleUnknown",
    };
}
