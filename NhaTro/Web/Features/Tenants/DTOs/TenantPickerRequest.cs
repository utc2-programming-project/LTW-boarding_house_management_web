using System.ComponentModel.DataAnnotations;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Bộ lọc chọn khách cho hợp đồng (Đào dùng).
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.8.
/// </summary>
public sealed class TenantPickerRequest
{
    /// <summary>Họ tên / số điện thoại / số giấy tờ.</summary>
    [MaxLength(200)]
    public string? Keyword { get; set; }

    /// <summary>Số kết quả tối đa; mặc định 20, trần 50 (cần nhóm chốt).</summary>
    public int Limit { get; set; } = 20;

    /// <summary>Loại trừ khách đã chọn; tối đa 100 phần tử để câu IN (...) không quá dài.</summary>
    public List<int>? ExcludeTenantIds { get; set; }

    // Normalize là nguồn duy nhất kẹp giá trị; KHÔNG dùng [Range] cho Limit.
    public void Normalize()
    {
        if (Limit < 1) Limit = 20;
        if (Limit > 50) Limit = 50;
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
        if (ExcludeTenantIds is { Count: > 100 } ids)
        {
            ExcludeTenantIds = ids.Take(100).ToList();
        }
    }
}
