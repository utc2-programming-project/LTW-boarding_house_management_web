using Microsoft.EntityFrameworkCore;
using Web.Common.Models;

namespace Web.Common.Queries;

/// <summary>Phân trang SQL dùng chung: count + skip/take trên cùng predicate, page size 1..100.</summary>
public static class PagingExtensions
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static int ClampPage(int page) => Math.Max(1, page);

    public static int ClampPageSize(int pageSize) =>
        pageSize <= 0 ? DefaultPageSize : Math.Clamp(pageSize, 1, MaxPageSize);

    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = ClampPage(page);
        pageSize = ClampPageSize(pageSize);
        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (long)(page - 1) * pageSize;
        var items = await query
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}
