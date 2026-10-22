using Web.Features.Accounts;

namespace Web.Features.Settings;

/// <summary>
/// Ngữ cảnh người gọi service công tắc. Service tự kiểm quyền bên trong,
/// không nhận vai trò/phạm vi từ client.
/// </summary>
public sealed record ManagerToggleCaller(string UserId, string Role)
{
    /// <summary>D12/ma trận dòng 8: chỉ Chủ trọ được đọc và ghi công tắc nghiệp vụ.</summary>
    public bool IsChuTro => Role == Roles.ChuTro;

    public bool IsQuanLy => Role == Roles.QuanLy;

    public bool IsAdmin => Role == Roles.Admin;

    public bool IsKhachThue => Role == Roles.KhachThue;

    /// <summary>Quản lý không đổi được công tắc của mình; Admin không có quyền nghiệp vụ (D1).</summary>
    public bool CanConfigure => IsChuTro;
}
