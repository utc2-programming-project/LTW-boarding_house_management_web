namespace Web.Common.Models;

// Ket qua phan trang dung chung cho list P2 (Phuc dung cho filter/paging).
// Service chi tra 1 trang, khong load het vao RAM.
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
