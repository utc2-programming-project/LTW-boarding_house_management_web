namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Một kết quả trong danh sách chọn khách.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.8.
/// </summary>
public class TenantPickerItemDto
{
    public required int Id { get; init; }

    public required string FullName { get; init; }

    public required string PhoneNumber { get; init; }

    /// <summary>Chỉ 4 ký tự cuối (ví dụ ********1234).</summary>
    public string? IdNumberMasked { get; init; }

    /// <summary>2C (VHT-25): hợp đồng đã duyệt còn hiệu lực; hiện chưa bật nên luôn false.</summary>
    public required bool HasActiveContract { get; init; }
}
