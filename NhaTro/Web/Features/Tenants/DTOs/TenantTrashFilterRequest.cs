using System.ComponentModel.DataAnnotations;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Bộ lọc thùng rác hồ sơ khách thuê (VHT-99). Chỉ Chủ trọ được đọc/khôi phục.
/// </summary>
public sealed class TenantTrashFilterRequest
{
    /// <summary>Tìm theo họ tên, số điện thoại hoặc số giấy tờ; khớp một phần.</summary>
    [MaxLength(200)]
    public string? Keyword { get; set; }

    /// <summary>Bắt đầu từ 1.</summary>
    public int Page { get; set; } = 1;

    /// <summary>Mặc định 20, tối đa 100.</summary>
    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        if (Page < 1) Page = 1;
        if (PageSize < 1) PageSize = 20;
        if (PageSize > 100) PageSize = 100;
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();
    }
}
