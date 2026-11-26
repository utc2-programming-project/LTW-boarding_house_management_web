using Web.Features.Accounts;

namespace Web.Features.Properties;

/// <summary>
/// Ngữ cảnh người gọi service P2 (nhà trọ + phòng).
/// Service tự kiểm quyền/phạm vi bên trong; không nhận OwnerId/phạm vi từ client.
/// Quản lý chỉ thấy nhà/phòng được phân công đang hiệu lực (VHT-24).
/// </summary>
public sealed record PropertyCaller(string UserId, string Role)
{
    /// <summary>
    /// Chủ trọ đang bị khoá (vĩnh viễn). Quản lý/khách thuê có nhiều chủ trọ vẫn dùng được
    /// vai trò, nhưng dữ liệu thuộc chủ trọ bị khoá phải bị loại khỏi mọi phạm vi.
    /// </summary>
    public List<string> BlockedOwnerIds { get; init; } = [];

    public bool BlocksOwner(string ownerId) => BlockedOwnerIds.Contains(ownerId);

    public bool IsChuTro => Role == Roles.ChuTro;

    public bool IsQuanLy => Role == Roles.QuanLy;

    public bool IsAdmin => Role == Roles.Admin;

    public bool IsKhachThue => Role == Roles.KhachThue;

    /// <summary>Chủ trọ / Quản lý: được đọc P2; service áp dụng phạm vi riêng theo vai trò.</summary>
    public bool IsManager => IsChuTro || IsQuanLy;
}
