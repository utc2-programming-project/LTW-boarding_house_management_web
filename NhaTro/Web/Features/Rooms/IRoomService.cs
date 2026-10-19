using Web.Common.Models;
using Web.Features.Rooms.DTOs;

namespace Web.Features.Rooms;

/// <summary>
/// Service phòng P2 (đợt 1C). Mọi hàm nhận <see cref="Properties.PropertyCaller"/> và tự kiểm quyền/phạm vi.
/// Sai vai trò ném <see cref="UnauthorizedAccessException"/>; lỗi nghiệp vụ trả <see cref="RoomResult"/>.
/// </summary>
public interface IRoomService
{
    Task<PagedResult<RoomListItem>> ListAsync(
        RoomFilter filter, Properties.PropertyCaller caller, CancellationToken ct = default);

    Task<RoomResult> GetAsync(int id, Properties.PropertyCaller caller, CancellationToken ct = default);

    /// <summary>Tạo phòng. PropertyId phải nằm trong phạm vi người gọi; mã không trùng trong nhà.</summary>
    Task<RoomResult> CreateAsync(
        RoomSave request, Properties.PropertyCaller caller, CancellationToken ct = default);

    /// <summary>
    /// Sửa phòng. Quản lý đi qua policy công tắc (SW-01/02/03 — VHT-94);
    /// VHT-7 chỉ chừa điểm gọi, chưa làm hàng đợi duyệt (VHT-96).
    /// </summary>
    Task<RoomResult> UpdateAsync(
        int id, RoomSave request, Properties.PropertyCaller caller, CancellationToken ct = default);

    /// <summary>Xóa mềm (thùng rác VHT-98). Chỉ Chủ trọ; chặn khi phòng còn hợp đồng hiệu lực.</summary>
    Task<RoomResult> DeleteAsync(
        int id, Properties.PropertyCaller caller, CancellationToken ct = default);

    /// <summary>Khôi phục phòng từ thùng rác. Chỉ Chủ trọ; chặn khi mã phòng đã bị dùng lại.</summary>
    Task<RoomResult> RestoreAsync(
        int id, Properties.PropertyCaller caller, CancellationToken ct = default);

    /// <summary>Dữ liệu chọn phòng bàn giao cho Đào (hợp đồng VHT-11/VHT-29) và query P8.</summary>
    Task<List<RoomOption>> SearchForPickerAsync(
        RoomOptionRequest request, Properties.PropertyCaller caller, CancellationToken ct = default);
}
