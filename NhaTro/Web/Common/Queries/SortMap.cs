namespace Web.Common.Queries;

/// <summary>
/// Whitelist sort key → IOrderedQueryable. Không nhận sort string thô.
/// Truyền <c>tieBreak</c> để mọi nhánh (kể cả fallback) luôn có cột unique cuối cùng.
/// </summary>
public sealed class SortMap<T>(
    Func<IQueryable<T>, bool, IOrderedQueryable<T>> fallback,
    Func<IOrderedQueryable<T>, IOrderedQueryable<T>>? tieBreak = null)
{
    private readonly Dictionary<string, Func<IQueryable<T>, bool, IOrderedQueryable<T>>> _sorts =
        new(StringComparer.OrdinalIgnoreCase);

    public SortMap<T> Add(string key, Func<IQueryable<T>, bool, IOrderedQueryable<T>> sort)
    {
        _sorts[key] = sort;
        return this;
    }

    public IOrderedQueryable<T> Apply(IQueryable<T> query, string? key, bool descending)
    {
        var ordered = key is not null && _sorts.TryGetValue(key.Trim(), out var sort)
            ? sort(query, descending)
            : fallback(query, descending);
        return tieBreak is null ? ordered : tieBreak(ordered);
    }
}
