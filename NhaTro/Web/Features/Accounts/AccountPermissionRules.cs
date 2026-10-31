namespace Web.Features.Accounts;

/// <summary>
/// VHT-8 / VHT-95 / D1: luat phan quyen quan tri tai khoan.
/// Tach thanh ham thuan (khong DB, khong HttpContext) de unit test nhanh va de doc.
///
/// D1 (chot 12/10/2026) + o #5/#6/#7 cua thao_tac_va_quyen.xlsx:
///  - Admin   : quan ly toan bo tai khoan, cap/doi vai tro, khoa/mo khoa.
///  - ChuTro  : tao QuanLy + KhachThue (hieu luc ngay), duyet/khoa trong pham vi minh. KHONG doi vai tro.
///  - QuanLy  : sua ho so minh, tao tai khoan KhachThue (Cho duyet) trong toa duoc giao.
///  - KhachThue: chi ho so ca nhan.
/// </summary>
public static class AccountPermissionRules
{
    /// <summary>Vai tro ma nguoi goi duoc phep gan khi tao/doi vai tro.</summary>
    public static IReadOnlyList<string> AssignableRoles(string callerRole) => callerRole switch
    {
        Roles.Admin => Roles.All,
        // ChuTro tao QuanLy + KhachThue nhung khong doi vai tro (xem CanChangeRole).
        Roles.ChuTro => [Roles.QuanLy, Roles.KhachThue],
        // QuanLy chi tao tai khoan KhachThue, o trang thai Cho duyet.
        Roles.QuanLy => [Roles.KhachThue],
        _ => [],
    };

    /// <summary>Nguoi goi co duoc tao tai khoan vai tro nay khong (o #5).</summary>
    public static bool CanCreateRole(string callerRole, string targetRole) =>
        AssignableRoles(callerRole).Contains(targetRole);

    /// <summary>Nguoi goi co duoc duyet tai khoan vai tro nay khong (o #6, cung pham vi voi khoa).</summary>
    public static bool CanApprove(string callerRole, string targetRole) =>
        LockableRoles(callerRole).Contains(targetRole);

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
        callerRole is Roles.Admin or Roles.ChuTro or Roles.QuanLy;

    /// <summary>Vai tro ma nguoi goi duoc phep nhin thay trong danh sach.</summary>
    public static IReadOnlyList<string> VisibleRoles(string callerRole) => callerRole switch
    {
        Roles.Admin => Roles.All,
        Roles.ChuTro => [Roles.QuanLy, Roles.KhachThue],
        // QuanLy chi thay chinh minh + khach trong toa duoc giao; pham vi du lieu loc o service.
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
        string newRole,
        bool targetIsLinkedTenant = false)
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

        // Tai khoan dang gan ho so khach: doi vai tro se lam ho so tro vao tai khoan sai vai tro.
        if (targetIsLinkedTenant && newRole != targetRole)
        {
            return AccountErrorCodes.RoleLinkedToTenant;
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
        bool unlock,
        bool targetIsLastAdmin = false)
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

        // Khoa Admin cuoi cung (dang hoat dong) se lam he thong khong con ai quan tri.
        if (targetRole == Roles.Admin && targetIsLastAdmin)
        {
            return AccountErrorCodes.CannotLockLastAdmin;
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
