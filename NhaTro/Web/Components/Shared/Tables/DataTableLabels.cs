namespace Web.Components.Shared.Tables;

/// <summary>
/// Chữ của bảng. Mặc định là tiếng Việt; màn hình tiếng Anh truyền bản dịch qua parameter
/// <c>Labels</c> (lấy từ resource khi localization chung của VHT-20 sẵn sàng).
/// </summary>
public sealed record DataTableLabels
{
    public string Loading { get; init; } = "Đang tải…";
    public string EmptyTitle { get; init; } = "Chưa có dữ liệu";
    public string? EmptyDescription { get; init; }
    public string NoResultsTitle { get; init; } = "Không có kết quả phù hợp";
    public string? NoResultsDescription { get; init; }
    public string ClearFilters { get; init; } = "Xóa bộ lọc";
    public string LoadFailed { get; init; } = "Không tải được dữ liệu.";
    public string NoData { get; init; } = "Chưa có dữ liệu";
    public string Retry { get; init; } = "Thử lại";
}
