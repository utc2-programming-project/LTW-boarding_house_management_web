using Web.Features.Accounts;

namespace Web.Features.Approvals;

/// <summary>
/// Ngữ cảnh người gọi service hàng đợi duyệt (VHT-96). Service tự kiểm quyền bên trong,
/// không nhận vai trò/phạm vi từ client.
/// </summary>
public sealed record ApprovalCaller(string UserId, string Role)
{
    public bool IsChuTro => Role == Roles.ChuTro;

    public bool IsQuanLy => Role == Roles.QuanLy;

    public bool IsAdmin => Role == Roles.Admin;

    /// <summary>Chỉ Chủ trọ duyệt/từ chối; Quản lý không duyệt được kể cả yêu cầu của mình (D4).</summary>
    public bool CanApprove => IsChuTro;

    /// <summary>Chỉ Quản lý mới sinh yêu cầu chờ duyệt (thao tác ⚙️).</summary>
    public bool CanRequestApproval => IsQuanLy;
}
