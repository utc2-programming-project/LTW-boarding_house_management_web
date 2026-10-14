namespace Web.Common.Models;

/// <summary>Một trang dữ liệu kèm tổng số bản ghi (trước khi phân trang). Service trả về, bảng dùng chung hiển thị.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
