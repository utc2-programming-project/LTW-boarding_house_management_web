namespace Web.Components.Shared.Tables;

/// <summary>
/// Yêu cầu bảng gửi tới nơi lấy dữ liệu. <paramref name="Page"/> bắt đầu từ 1.
/// Bộ lọc do trang tự quản (thanh lọc chung ngoài bảng), không đi qua yêu cầu này.
/// </summary>
public sealed record DataTableRequest(
    int Page,
    int PageSize,
    string? SortKey,
    bool SortDescending);
