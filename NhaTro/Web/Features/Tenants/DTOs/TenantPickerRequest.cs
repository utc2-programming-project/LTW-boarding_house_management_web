namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Bộ lọc chọn khách cho hợp đồng (Đào dùng).
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.8.
/// </summary>
public class TenantPickerRequest
{
    /// <summary>Họ tên / số điện thoại / số giấy tờ.</summary>
    public string? Keyword { get; init; }

    /// <summary>Số kết quả tối đa; mặc định và trần cần chốt.</summary>
    public int Limit { get; init; } = 20;

    /// <summary>Loại trừ khách đã chọn.</summary>
    public List<Guid>? ExcludeTenantIds { get; init; }
}
