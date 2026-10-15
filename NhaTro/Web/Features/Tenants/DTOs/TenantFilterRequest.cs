using Web.Features.Tenants.Enums;

namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Bộ lọc danh sách khách thuê.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.1.
/// </summary>
public class TenantFilterRequest
{
    /// <summary>Tìm theo họ tên, số điện thoại, số giấy tờ (sau chuẩn hóa); khớp một phần.</summary>
    public string? Keyword { get; init; }

    /// <summary>null = tất cả; mặc định đề xuất true.</summary>
    public bool? IsActive { get; init; }

    /// <summary>Lọc theo đã/chưa có tài khoản.</summary>
    public bool? HasAccount { get; init; }

    /// <summary>Cần chốt: phụ thuộc dữ liệu hợp đồng.</summary>
    public bool? HasActiveContract { get; init; }

    /// <summary>Bắt đầu từ 1; mặc định đề xuất 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Mặc định và tối đa: cần chốt.</summary>
    public int PageSize { get; init; } = 20;

    /// <summary>Mặc định đề xuất FullName.</summary>
    public TenantSortBy SortBy { get; init; } = TenantSortBy.FullName;

    public SortDirection SortDirection { get; init; } = SortDirection.Asc;
}
