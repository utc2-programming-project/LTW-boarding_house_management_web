using Web.Features.Accounts;
using Web.Features.Storage.Enums;

namespace Web.Features.Storage.Rules;

/// <summary>
/// Policy quyền storage thuần (VHT-12 mục 4): ai được upload / tải / xóa theo vai trò và loại tệp.
/// <b>Admin luôn bị chặn</b> — Admin chỉ quản trị tài khoản/vai trò/nhật ký, không có quyền nghiệp vụ
/// (D1–D17 12/10/2026, <c>phong_cach_giao_dien.md</c> §9).
/// </summary>
/// <remarks>
/// Không truy cập DB: phần "đối tượng liên quan có thuộc phạm vi người gọi không" do service xác định
/// rồi truyền vào qua <c>relatedEntityInScope</c>. Nhờ vậy policy test được không cần SQL.
/// </remarks>
public static class StoragePermissionRules
{
    private static readonly FileCategory[] TenantUploadableCategories =
    [
        FileCategory.IdentityDocument,
        FileCategory.IncidentPhoto,
        FileCategory.PaymentProof,
    ];

    /// <summary>
    /// Khách thuê chỉ được upload giấy tờ, ảnh sự cố và minh chứng của chính mình.
    /// <para>
    /// <c>PaymentProof</c> cho Chủ trọ/Quản lý còn "Cần chốt" ở VHT-12 mục 4.1 (nhập hộ khách?), nên
    /// mặc định <b>chặn</b> theo nguyên tắc deny-by-default (cấp quyền sau dễ hơn thu hồi).
    /// </para>
    /// </summary>
    public static bool CanUpload(string role, FileCategory category) => role switch
    {
        Roles.Admin => false,
        Roles.ChuTro or Roles.QuanLy => category != FileCategory.PaymentProof,
        Roles.KhachThue => TenantUploadableCategories.Contains(category),
        _ => false,
    };

    /// <summary>
    /// Tải tệp: Chủ trọ/Quản lý xem mọi tệp nghiệp vụ; khách thuê chỉ xem tệp do mình upload
    /// hoặc tệp gắn với đối tượng thuộc phạm vi của mình; Admin không xem.
    /// </summary>
    public static bool CanDownload(string role, bool isUploader, bool relatedEntityInScope) => role switch
    {
        Roles.Admin => false,
        Roles.ChuTro => true,
        Roles.QuanLy => true,
        Roles.KhachThue => isUploader || relatedEntityInScope,
        _ => false,
    };

    /// <summary>
    /// Xóa mềm: Chủ trọ/Quản lý xóa mọi tệp; Admin không xóa. Khách thuê xóa tệp của mình còn
    /// "Cần chốt" ở VHT-12 mục 4.3 (kèm điều kiện "nghiệp vụ chưa xử lý" chờ P6/P7) nên mặc định
    /// <b>chặn</b> theo nguyên tắc deny-by-default; mở lại khi nhóm chốt.
    /// </summary>
    public static bool CanDelete(string role) => role switch
    {
        Roles.Admin => false,
        Roles.ChuTro => true,
        Roles.QuanLy => true,
        Roles.KhachThue => false,
        _ => false,
    };
}
