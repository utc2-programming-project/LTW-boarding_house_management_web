namespace Web.Features.Tenants.Documents;

/// <summary>
/// Luật quyền thuần cho giấy tờ P3 (không DB) — đối chiếu D9 và ma trận quyền:
/// <list type="bullet">
/// <item>Admin: không xem/tải/ghi.</item>
/// <item>Chủ trọ: xem/tải/upload/thay/xóa/export giấy tờ trong phạm vi.</item>
/// <item>Quản lý: xem/tải/upload/thay/xóa giấy tờ trong tòa nhà được giao, <b>không xuất</b>.</item>
/// <item>Khách thuê: xem/tải/upload giấy tờ của chính mình; không xóa/thay (deny-by-default VHT-12 §4.3).</item>
/// </list>
/// </summary>
public static class TenantDocumentPermissionRules
{
    public static bool CanView(TenantDocumentAccessLevel access) =>
        access is TenantDocumentAccessLevel.Own
            or TenantDocumentAccessLevel.Owner
            or TenantDocumentAccessLevel.Manager;

    public static bool CanUpload(TenantDocumentAccessLevel access) => CanView(access);

    public static bool CanDelete(TenantDocumentAccessLevel access) =>
        access is TenantDocumentAccessLevel.Owner or TenantDocumentAccessLevel.Manager;

    public static bool CanReplace(TenantDocumentAccessLevel access) => CanDelete(access);

    /// <summary>D9: Quản lý không xuất giấy tờ; Admin không có quyền nghiệp vụ.</summary>
    public static bool CanExport(TenantDocumentAccessLevel access) =>
        access is TenantDocumentAccessLevel.Own or TenantDocumentAccessLevel.Owner;
}
