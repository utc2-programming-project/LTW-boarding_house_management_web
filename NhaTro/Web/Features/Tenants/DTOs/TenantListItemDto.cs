namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Một dòng trong danh sách khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.3.
/// </summary>
public class TenantListItemDto
{
    public required int Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    /// <summary>Chỉ 4 ký tự cuối (ví dụ ********1234).</summary>
    public string? IdNumberMasked { get; init; }

    public required bool IsActive { get; init; }

    /// <summary>Null khi vai trò không được xem trạng thái tài khoản app (D9).</summary>
    public required bool? HasAccount { get; init; }

    /// <summary>Chờ hợp đồng (mục 6.1); hiện luôn false khi chưa có bảng hợp đồng.</summary>
    public required bool HasActiveContract { get; init; }

    /// <summary>
    /// Tên phòng đang thuê (D9: Quản lý được xem cột "phòng"). Hiện luôn null vì <c>Tenant</c> chưa liên kết tới
    /// phòng/hợp đồng (VHT-24/VHT-25); khi có liên kết chỉ cần gán giá trị này trong <c>TenantMapping.ToListItem</c>.
    /// </summary>
    public string? RoomName { get; init; }
}
