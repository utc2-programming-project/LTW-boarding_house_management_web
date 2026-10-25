using System.Security.Principal;
using Web.Features.Accounts;

namespace Web.Features.Storage;

/// <summary>
/// Ngữ cảnh người gọi storage. Tách khỏi <see cref="IPrincipal"/> để service tệp dùng được cả từ
/// HTTP endpoint lẫn service nghiệp vụ P3 (không phải dựng lại ClaimsPrincipal).
/// Role lấy từ danh sách vai trò đã chốt; vai trò lạ trả chuỗi rỗng và bị từ chối.
/// </summary>
public sealed record StorageCaller(string UserId, string Role)
{
    public bool IsAdmin => Role == Roles.Admin;

    public bool IsChuTro => Role == Roles.ChuTro;

    public bool IsQuanLy => Role == Roles.QuanLy;

    public bool IsKhachThue => Role == Roles.KhachThue;

    /// <summary>Chuyển ClaimsPrincipal của request thành ngữ cảnh storage.</summary>
    public static StorageCaller FromPrincipal(IPrincipal user)
    {
        var role = Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
        return new StorageCaller(user.UserId() ?? string.Empty, role);
    }
}
