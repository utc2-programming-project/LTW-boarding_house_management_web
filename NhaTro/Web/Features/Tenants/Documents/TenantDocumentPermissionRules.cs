namespace Web.Features.Tenants.Documents;

/// <summary>
/// Luật quyền thuần cho giấy tờ P3 (không DB) — đối chiếu ma trận quyền đợt 2B:
/// <list type="bullet">
/// <item>Admin: không có quyền xem hoặc thao tác.</item>
/// <item>Chủ trọ: đầy đủ quyền trong phạm vi nhà trọ.</item>
/// <item>Quản lý: xem/tải trong phạm vi được giao; không tải lên, xóa, thay thế hoặc xuất.</item>
/// <item>Khách thuê: xem/tải lên/tải xuống giấy tờ của mình; không xóa, thay thế hoặc xuất.</item>
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

    public static bool CanDelete(TenantDocumentAccessLevel access, bool isUploader) => CanDelete(access);

    public static bool CanReplace(TenantDocumentAccessLevel access) => access == TenantDocumentAccessLevel.Owner;

    public static bool CanDownload(TenantDocumentAccessLevel access) =>
        access is TenantDocumentAccessLevel.Own or TenantDocumentAccessLevel.Owner or TenantDocumentAccessLevel.Manager;

    public static bool CanExport(TenantDocumentAccessLevel access) => access == TenantDocumentAccessLevel.Owner;
}
