using System.ComponentModel.DataAnnotations;
using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Bộ lọc danh sách khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.1.
/// </summary>
public sealed class TenantFilterRequest
{
    /// <summary>Tìm theo họ tên, số điện thoại, số giấy tờ (sau chuẩn hóa); khớp một phần.</summary>
    [MaxLength(200)]
    public string? Keyword { get; set; }

    /// <summary>null = tất cả (mặc định).</summary>
    public bool? IsActive { get; set; }

    /// <summary>Lọc theo đã/chưa có tài khoản.</summary>
    public bool? HasAccount { get; set; }

    /// <summary>Chờ hợp đồng (Đào xác nhận); hiện chưa có bảng hợp đồng nên bỏ qua.</summary>
    public bool? HasActiveContract { get; set; }

    /// <summary>Bắt đầu từ 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Mặc định 20, tối đa 100.</summary>
    public int PageSize { get; set; } = 20;

    public TenantSortBy SortBy { get; set; } = TenantSortBy.FullName;

    public SortDirection SortDirection { get; set; } = SortDirection.Asc;

    // Normalize là nguồn duy nhất kẹp giá trị; KHÔNG dùng [Range] cho Page/PageSize
    // để tránh việc model binding trả 400 trước khi Normalize kịp xử lý.
    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 20;
        if (PageSize > 100) PageSize = 100;
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
    }
}
