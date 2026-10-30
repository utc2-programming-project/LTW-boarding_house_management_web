using Web.Common.Models;
using Web.Features.Accounts.DTOs;

namespace Web.Features.Accounts;

/// <summary>
/// VHT-8: service tai khoan cho P1 (ho so ca nhan + quan tri tai khoan).
/// Moi ham nhan <c>callerUserId</c> va <c>callerRole</c>; service tu kiem quyen theo D1,
/// khong tin du lieu tu client.
///
/// Khac <see cref="RoleService"/> (VHT-6): RoleService la lop gac quyen cung, con service nay
/// la nghiep vu UI can (loc, phan trang, khoa/mo khoa, cap nhat ho so).
/// </summary>
public interface IAccountService
{
    /// <summary>Danh sach tai khoan cho /admin/users, da loc theo vai tro nguoi goi duoc thay.</summary>
    Task<PagedResult<AccountListItemDto>> ListAsync(
        string? keyword, string? roleFilter, int page, int pageSize,
        string callerUserId, string callerRole, CancellationToken ct = default);

    /// <summary>Ho so cua chinh nguoi dang dang nhap.</summary>
    Task<AccountResultOf<MyProfileDto>> GetMyProfileAsync(
        string callerUserId, CancellationToken ct = default);

    /// <summary>Cap nhat ho ten / so dien thoai cua chinh minh.</summary>
    Task<AccountResultOf<MyProfileDto>> UpdateMyProfileAsync(
        UpdateMyProfileRequest request, string callerUserId, CancellationToken ct = default);

    /// <summary>Doi vai tro mot tai khoan (chi Admin - D1).</summary>
    Task<AccountResult> ChangeRoleAsync(
        string targetUserId, string newRole,
        string callerUserId, string callerRole, CancellationToken ct = default);

    /// <summary>Khoa tai khoan (khoa vinh vien cho toi khi mo).</summary>
    Task<AccountResult> LockAsync(
        string targetUserId, string callerUserId, string callerRole, CancellationToken ct = default);

    /// <summary>Mo khoa tai khoan; tai khoan chua duyet quay ve Cho duyet.</summary>
    Task<AccountResult> UnlockAsync(
        string targetUserId, string callerUserId, string callerRole, CancellationToken ct = default);

    /// <summary>VHT-95: tao tai khoan theo o #5 (Admin/ChuTro hieu luc ngay, QuanLy Cho duyet).</summary>
    Task<AccountResult> CreateAsync(
        CreateAccountRequest request, string callerUserId, string callerRole, CancellationToken ct = default);

    /// <summary>VHT-95: duyet tai khoan dang Cho duyet (o #6).</summary>
    Task<AccountResult> ApproveAsync(
        string targetUserId, string callerUserId, string callerRole, CancellationToken ct = default);

    /// <summary>VHT-95: ho so khach chua co tai khoan trong pham vi nguoi goi (cho dialog tao).</summary>
    Task<IReadOnlyList<AssignableTenantDto>> SearchAssignableTenantsAsync(
        string? keyword, string callerUserId, string callerRole, CancellationToken ct = default);
}
