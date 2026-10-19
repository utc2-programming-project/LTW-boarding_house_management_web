using Web.Common.Models;
using Web.Features.Properties.DTOs;

namespace Web.Features.Properties;

/// <summary>
/// Service nhà trọ P2 (đợt 1C). Mọi hàm nhận <see cref="PropertyCaller"/> và tự kiểm quyền/phạm vi.
/// Sai vai trò ném <see cref="UnauthorizedAccessException"/> (controller trả 403);
/// lỗi nghiệp vụ trả <see cref="PropertyResult"/>.
/// </summary>
public interface IPropertyService
{
    /// <summary>Danh sách nhà trọ trong phạm vi người gọi, có RoomCount (phòng chưa xóa mềm).</summary>
    Task<PagedResult<PropertyListItem>> ListAsync(
        PropertyFilter filter, PropertyCaller caller, CancellationToken ct = default);

    Task<PropertyResult> GetAsync(int id, PropertyCaller caller, CancellationToken ct = default);

    /// <summary>Tạo nhà trọ. OwnerId lấy từ người gọi, không nhận từ client.</summary>
    Task<PropertyResult> CreateAsync(
        PropertySave request, PropertyCaller caller, CancellationToken ct = default);

    /// <summary>Sửa nhà trọ trong phạm vi. Không cho đổi chủ sở hữu qua DTO.</summary>
    Task<PropertyResult> UpdateAsync(
        int id, PropertySave request, PropertyCaller caller, CancellationToken ct = default);
}
