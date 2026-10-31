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

    /// <summary>
    /// Có hợp đồng thuê đang hiệu lực (đã duyệt, hôm nay nằm trong thời hạn) theo liên kết thật
    /// Tenant -> ContractMember -> Contract.
    /// </summary>
    public required bool HasActiveContract { get; init; }

    /// <summary>
    /// Tên phòng đang thuê (D9: Quản lý được xem cột "phòng"), lấy từ hợp đồng đang hiệu lực.
    /// Null khi khách chưa thuê.
    /// </summary>
    public string? RoomName { get; init; }
}
