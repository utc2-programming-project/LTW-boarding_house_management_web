namespace Web.Common.Utils;

/// <summary>Tính toán phân trang thuần (số trang bắt đầu từ 1). Không phụ thuộc UI hay database.</summary>
public static class PaginationHelper
{
    /// <summary>Tổng số trang; luôn tối thiểu là 1 kể cả khi không có dữ liệu.</summary>
    public static int TotalPages(int totalCount, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        if (totalCount <= 0) return 1;
        return (int)(((long)totalCount + pageSize - 1) / pageSize);
    }

    public static int ClampPage(int page, int totalPages) => Math.Clamp(page, 1, totalPages);

    /// <summary>Dãy số trang hiển thị quanh trang hiện tại, tối đa <paramref name="size"/> số.</summary>
    public static IReadOnlyList<int> PageWindow(int currentPage, int totalPages, int size = 5)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        var count = Math.Min(size, totalPages);
        var start = Math.Clamp(currentPage - size / 2, 1, totalPages - count + 1);
        return Enumerable.Range(start, count).ToList();
    }

    /// <summary>Thứ tự bản ghi đầu và cuối của trang (cho dòng "Hiển thị 11–20 / 45"). Không có dữ liệu: (0, 0).</summary>
    public static (int From, int To) ItemRange(int page, int pageSize, int totalCount)
    {
        if (totalCount <= 0) return (0, 0);
        var from = (page - 1) * pageSize + 1;
        var to = Math.Min(page * pageSize, totalCount);
        return (from, to);
    }
}
