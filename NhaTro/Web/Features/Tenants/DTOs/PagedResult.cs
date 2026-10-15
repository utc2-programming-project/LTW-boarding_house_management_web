namespace Web.Features.Tenants.DTOs;

/// <summary>
/// Kết quả phân trang dùng chung.
/// Nguồn: docs/task/de_xuat_hop_dong_khach_thue.md mục 6.2.
/// </summary>
public class PagedResult<T>
{
    public required List<T> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}
