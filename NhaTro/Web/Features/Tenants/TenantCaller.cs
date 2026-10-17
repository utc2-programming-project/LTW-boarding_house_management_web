namespace Web.Features.Tenants;

/// <summary>
/// Ngữ cảnh người gọi service khách thuê (mục 4, mục 7).
/// Service kiểm quyền/phạm vi bên trong, không nhận phạm vi từ client.
/// </summary>
public sealed record TenantCaller(string UserId, string Role)
{
    public bool IsManager => Role is Accounts.Roles.ChuTro or Accounts.Roles.QuanLy;

    public bool IsAdmin => Role == Accounts.Roles.Admin;

    public bool IsTenant => Role == Accounts.Roles.KhachThue;

    /// <summary>
    /// D9 (thao_tac_va_quyen.xlsx): chỉ Chủ trọ được xem số CCCD đầy đủ; Quản lý luôn bị che.
    /// Dùng để quyết định trả <c>IdNumber</c> đầy đủ hay chuỗi che ở chi tiết.
    /// </summary>
    public bool CanRevealIdNumber => Role == Accounts.Roles.ChuTro;
}
