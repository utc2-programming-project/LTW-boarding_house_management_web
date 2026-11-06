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

    /// <summary>
    /// Danh sách tài khoản đang có role Quản lý và chưa được gán vào tòa nhà.
    /// Chỉ Chủ trọ sở hữu tòa nhà được đọc.
    /// </summary>
    Task<PropertyManagerCandidateListResult> ListCandidatesAsync(
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default);

    /// <summary>Gán một tài khoản Quản lý vào tòa nhà. Trùng phân công đang hiệu lực trả AlreadyAssigned.</summary>
    Task<PropertyManagerResult> AssignAsync(
        int propertyId,
        string managerUserId,
        PropertyCaller caller,
        CancellationToken ct = default);

    /// <summary>
    /// Danh sách tài khoản đang giữ vai trò Quản lý, đã duyệt và chưa bị khóa,
    /// kèm cờ đã phân công cho tòa nhà này (Chủ trọ sở hữu tòa nhà mới đọc được).
    /// </summary>
    Task<PropertyManagerCandidateResult> ListAssignableManagersAsync(
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default);

    /// <summary>Bỏ gán (giữ lịch sử: set RevokedAt). Phân công không còn hiệu lực ngay.</summary>
    Task<PropertyManagerResult> RevokeAsync(
        int propertyId,
        string managerUserId,
        PropertyCaller caller,
        CancellationToken ct = default);

    /// <summary>
    /// Các tòa nhà của Chủ trọ đang gán cho một Quản lý (dùng cho trang Quản trị tài khoản).
    /// Chỉ trả tòa thuộc sở hữu của người gọi.
    /// </summary>
    Task<PropertyManagerListResult> ListAssignmentsForManagerAsync(
        string managerUserId,
        PropertyCaller caller,
        bool includeHistory = false,
        CancellationToken ct = default);

    /// <summary>
    /// Các tòa nhà của Chủ trọ còn trống để gán thêm cho một Quản lý.
    /// Quản lý đã thuộc Chủ trọ khác trả ManagerOwnedByAnotherOwner.
    /// </summary>
    Task<PropertyManagerPropertyListResult> ListAssignablePropertiesAsync(
        string managerUserId,
        PropertyCaller caller,
        CancellationToken ct = default);

    /// <summary>Chủ trọ mà Quản lý đang đăng nhập trực thuộc; null nếu chưa được phân công.</summary>
    Task<ManagerOwnerResult> GetOwnerForManagerAsync(
        PropertyCaller caller,
        CancellationToken ct = default);
}
