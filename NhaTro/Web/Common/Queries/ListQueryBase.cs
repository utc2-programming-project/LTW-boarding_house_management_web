namespace Web.Common.Queries;

/// <summary>Tham số đọc danh sách dùng chung. Không biết EF, không biết quyền.</summary>
public class ListQueryBase
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = PagingExtensions.DefaultPageSize;

    public string? SortKey { get; set; }

    public bool SortDescending { get; set; }

    public string? Keyword { get; set; }

    public virtual void Normalize()
    {
        Page = PagingExtensions.ClampPage(Page);
        PageSize = PagingExtensions.ClampPageSize(PageSize);
        Keyword = SearchTerm.Normalize(Keyword);
        SortKey = SearchTerm.Normalize(SortKey);
    }
}
