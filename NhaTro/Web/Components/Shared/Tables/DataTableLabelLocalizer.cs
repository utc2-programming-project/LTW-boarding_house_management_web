using Microsoft.Extensions.Localization;

namespace Web.Components.Shared.Tables;

/// <summary>
/// VHT-8: dung nhan ban dich .resx cho <see cref="DataTable{TItem}"/> thay vi mac dinh tieng Viet cung.
/// Mac dinh trong <see cref="DataTableLabels"/> chi dung khi component duoc dung doc lap (trang demo VHT-15).
/// </summary>
public static class DataTableLabelLocalizer
{
    public static DataTableLabels Create(IStringLocalizer localizer) => new()
    {
        Loading = localizer["TableLoading"],
        EmptyTitle = localizer["TableEmptyTitle"],
        NoResultsTitle = localizer["TableNoResults"],
        NoResultsDescription = localizer["TableNoResultsDescription"],
        ClearFilters = localizer["TableClearFilters"],
        LoadFailed = localizer["TableLoadFailed"],
        Retry = localizer["TableRetry"],
        FilterBy = localizer["TableFilterBy"],
        Pagination = localizer["TablePagination"],
        FirstPage = localizer["TableFirstPage"],
        PreviousPage = localizer["TablePreviousPage"],
        NextPage = localizer["TableNextPage"],
        LastPage = localizer["TableLastPage"],
        Page = localizer["TablePage"],
        Summary = localizer["TableSummary"],
    };
}
