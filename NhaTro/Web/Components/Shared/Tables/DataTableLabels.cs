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
    public string FilterBy { get; init; } = "Lọc theo";
    public string Pagination { get; init; } = "Phân trang";
    public string FirstPage { get; init; } = "Trang đầu";
    public string PreviousPage { get; init; } = "Trang trước";
    public string NextPage { get; init; } = "Trang sau";
    public string LastPage { get; init; } = "Trang cuối";
    public string Page { get; init; } = "Trang";

    /// <summary>Định dạng {0} = từ, {1} = đến, {2} = tổng.</summary>
    public string Summary { get; init; } = "Hiển thị {0}–{1} / {2}";

    /// <summary>Chân bảng gọn: {0} = trang, {1} = tổng trang, {2} = tổng bản ghi, {3} = đơn vị.</summary>
    public string PageInfo { get; init; } = "Trang {0}/{1} · {2} {3}";

    public string Previous { get; init; } = "Trước";

    public string Next { get; init; } = "Sau";
}
