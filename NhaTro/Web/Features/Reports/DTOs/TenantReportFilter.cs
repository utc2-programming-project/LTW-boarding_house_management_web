using System.ComponentModel.DataAnnotations;

namespace Web.Features.Reports.DTOs;

/// <summary>
/// Bộ lọc nguồn khách thuê P8 (VHT-46), dùng lại đúng nghĩa lọc của P3.
/// <para>
/// <c>IsActive</c> mặc định <c>null</c> = tổng hồ sơ (gồm cả hồ sơ tạm ngưng), khớp
/// <c>TenantService.GetSummaryAsync</c>. KPI và danh sách chạy trên cùng một predicate.
/// </para>
/// </summary>
public sealed class TenantReportFilter
{
    /// <summary>Chỉ lấy khách có hợp đồng tại nhà trọ này (mọi trạng thái hợp đồng).</summary>
    public int? PropertyId { get; set; }

    /// <summary>null = tổng hồ sơ; true = đang hoạt động; false = tạm ngưng.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Lọc theo đã/chưa có tài khoản đăng nhập (D9: Quản lý không được lọc).</summary>
    public bool? HasAccount { get; set; }

    /// <summary>Lọc theo đã/chưa có hợp đồng thuê đang hiệu lực.</summary>
    public bool? HasActiveContract { get; set; }

    /// <summary>Tìm theo họ tên, số điện thoại, số giấy tờ (sau chuẩn hóa); khớp một phần.</summary>
    [MaxLength(200)]
    public string? Keyword { get; set; }

    /// <summary>Bắt đầu từ 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Mặc định 20, tối đa 100.</summary>
    public int PageSize { get; set; } = 20;

    // Normalize là nguồn duy nhất kẹp giá trị (giống TenantFilterRequest P3).
    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 20;
        if (PageSize > 100) PageSize = 100;
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
    }
}
