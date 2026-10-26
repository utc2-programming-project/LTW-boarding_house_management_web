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
    /// 2C (VHT-25): sẽ lấy từ hợp đồng đã duyệt còn hiệu lực; hiện chưa bật nên luôn false.
    /// </summary>
    public required bool HasActiveContract { get; init; }

    /// <summary>2C (VHT-25): mã phòng đang thuê theo hợp đồng; hiện chưa bật nên luôn null.</summary>
    public string? RoomName { get; init; }
}
