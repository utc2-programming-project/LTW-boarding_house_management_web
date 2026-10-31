using Web.Features.Properties.DTOs;

namespace Web.Features.Properties;

/// <summary>
/// VHT-24 — phân công Quản lý theo tòa nhà. Chỉ Chủ trọ sở hữu tòa nhà được gán/bỏ gán;
/// mọi hàm tự kiểm quyền bên trong, không nhận phạm vi từ client.
/// </summary>
public interface IPropertyManagerService
{
    /// <summary>
    /// Danh sách phân công của một tòa nhà (Chủ trọ sở hữu).
    /// Mặc định chỉ trả phân công đang hiệu lực; <paramref name="includeHistory"/> = true trả cả dòng đã bỏ gán.
    /// </summary>
    Task<PropertyManagerListResult> ListAssignmentsAsync(
        int propertyId,
        PropertyCaller caller,
        bool includeHistory = false,
        CancellationToken ct = default);

    /// <summary>Gán một tài khoản Quản lý vào tòa nhà. Trùng phân công đang hiệu lực trả AlreadyAssigned.</summary>
    Task<PropertyManagerResult> AssignAsync(
        int propertyId,
        string managerUserId,
        PropertyCaller caller,
        CancellationToken ct = default);

    /// <summary>Bỏ gán (giữ lịch sử: set RevokedAt). Phân công không còn hiệu lực ngay.</summary>
    Task<PropertyManagerResult> RevokeAsync(
        int propertyId,
        string managerUserId,
        PropertyCaller caller,
        CancellationToken ct = default);
}
