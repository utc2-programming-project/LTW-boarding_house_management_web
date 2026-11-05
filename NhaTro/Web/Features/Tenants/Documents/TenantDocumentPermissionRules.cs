namespace Web.Features.Tenants.Documents;

/// <summary>
/// Luật quyền thuần cho giấy tờ P3 (không DB) — đối chiếu ma trận quyền đợt 2B/2D (D9):
/// <list type="bullet">
/// <item>Admin: không có quyền xem hoặc thao tác.</item>
/// <item>Chủ trọ: đầy đủ quyền trong phạm vi nhà trọ (xem, tải về, tải lên, xóa, thay thế, xuất danh sách).</item>
/// <item>Quản lý: xem và tải xuống trong phạm vi tòa nhà được giao để làm thủ tục tạm trú theo D9; không tải lên, không xóa, không thay thế, không xuất danh sách.</item>
/// <item>Khách thuê: xem, tải lên hồ sơ cá nhân và tải xuống giấy tờ của chính mình; không xóa, thay thế hoặc xuất danh sách.</item>
/// </list>
/// </summary>
public static class TenantDocumentPermissionRules
{
    public static bool CanView(TenantDocumentAccessLevel access) =>
        access is TenantDocumentAccessLevel.Own
            or TenantDocumentAccessLevel.Owner
            or TenantDocumentAccessLevel.Manager;

    public static bool CanUpload(TenantDocumentAccessLevel access) =>
        access is TenantDocumentAccessLevel.Own or TenantDocumentAccessLevel.Owner;

    public static bool CanDelete(TenantDocumentAccessLevel access) => access == TenantDocumentAccessLevel.Owner;

    public static bool CanReplace(TenantDocumentAccessLevel access) => access == TenantDocumentAccessLevel.Owner;

    public static bool CanDownload(TenantDocumentAccessLevel access) =>
        access is TenantDocumentAccessLevel.Own or TenantDocumentAccessLevel.Owner or TenantDocumentAccessLevel.Manager;

    public static bool CanExport(TenantDocumentAccessLevel access) => access == TenantDocumentAccessLevel.Owner;
}
