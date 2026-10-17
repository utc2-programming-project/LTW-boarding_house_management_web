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
}
