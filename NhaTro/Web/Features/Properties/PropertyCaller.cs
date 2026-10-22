using Web.Features.Accounts;

namespace Web.Features.Properties;

/// <summary>
/// Ngữ cảnh người gọi service P2 (nhà trọ + phòng).
/// Service tự kiểm quyền/phạm vi bên trong; không nhận OwnerId/phạm vi từ client.
/// Quản lý hiện xem toàn hệ thống tới khi VHT-24 bổ sung phân công nhà trọ.
/// </summary>
public sealed record PropertyCaller(string UserId, string Role)
{
    public bool IsChuTro => Role == Roles.ChuTro;

    public bool IsQuanLy => Role == Roles.QuanLy;

    public bool IsAdmin => Role == Roles.Admin;

    public bool IsKhachThue => Role == Roles.KhachThue;

    /// <summary>Chủ trọ / Quản lý: được đọc P2; service áp dụng phạm vi riêng theo vai trò.</summary>
    public bool IsManager => IsChuTro || IsQuanLy;
}
