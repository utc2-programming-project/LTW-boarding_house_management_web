namespace Web.Common.Queries;

/// <summary>Option/picker phải có trần cứng; không trả toàn bảng.</summary>
public static class LookupQuery
{
    public const int MaxOptions = 200;

    public static int ClampTake(int take) => Math.Clamp(take, 1, MaxOptions);

    public static IQueryable<T> Bounded<T>(IQueryable<T> query, int take = MaxOptions) =>
        query.Take(ClampTake(take));
}
