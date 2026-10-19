namespace Web.Components.Shared.Tables;

/// <summary>
/// Yêu cầu bảng gửi tới nơi lấy dữ liệu. <paramref name="Page"/> bắt đầu từ 1.
/// <paramref name="Filters"/> chỉ chứa các cột có giá trị lọc (đã cắt khoảng trắng).
/// </summary>
public sealed record DataTableRequest(
    int Page,
    int PageSize,
    string? SortKey,
    bool SortDescending,
    IReadOnlyDictionary<string, string> Filters);
