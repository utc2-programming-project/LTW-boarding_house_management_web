using Web.Features.Tenants.DTOs;

namespace Web.Features.Tenants;

/// <summary>
/// Service khách thuê P3 (mục 7). Mọi hàm nhận <see cref="TenantCaller"/> và tự kiểm quyền/phạm vi.
/// Authz (sai vai trò) ném <see cref="UnauthorizedAccessException"/> để controller trả 403;
/// lỗi nghiệp vụ (không thấy, trùng, ghi đè) trả <see cref="TenantResult"/>.
/// </summary>
public interface ITenantService
{
    Task<PagedResult<TenantListItemDto>> ListAsync(
        TenantFilterRequest filter, TenantCaller caller, CancellationToken ct = default);

    Task<TenantResult> GetAsync(int id, TenantCaller caller, CancellationToken ct = default);

    Task<TenantResult> GetOwnAsync(TenantCaller caller, CancellationToken ct = default);

    Task<TenantResult> CreateAsync(
        CreateTenantRequest request, TenantCaller caller, CancellationToken ct = default);

    Task<TenantResult> UpdateAsync(
        int id, UpdateTenantRequest request, TenantCaller caller, CancellationToken ct = default);

    Task<TenantResult> UpdateOwnAsync(
        UpdateOwnProfileRequest request, TenantCaller caller, CancellationToken ct = default);

    Task<TenantResult> SetActiveAsync(
        int id, bool isActive, string rowVersion, TenantCaller caller, CancellationToken ct = default);

    /// <summary>
    /// Gắn tài khoản đăng nhập (vai trò Khách thuê) vào hồ sơ. Chặn liên kết nhầm:
    /// tài khoản phải tồn tại, đúng vai trò Khách thuê, chưa gắn hồ sơ khác.
    /// Một tài khoản tối đa một hồ sơ (mục 1, đang chờ Đào/Phát chốt).
    /// </summary>
    Task<TenantResult> LinkAccountAsync(
        int id, string accountUserId, string rowVersion, TenantCaller caller, CancellationToken ct = default);

    /// <summary>Gỡ liên kết tài khoản khỏi hồ sơ (giữ nguyên hồ sơ).</summary>
    Task<TenantResult> UnlinkAccountAsync(
        int id, string rowVersion, TenantCaller caller, CancellationToken ct = default);

    Task<List<TenantPickerItemDto>> SearchForPickerAsync(
        TenantPickerRequest request, TenantCaller caller, CancellationToken ct = default);
}
