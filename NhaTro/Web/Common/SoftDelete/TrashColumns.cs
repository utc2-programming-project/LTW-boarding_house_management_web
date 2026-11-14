using Microsoft.AspNetCore.Components;
using Web.Common.Utils;
using Web.Components.Shared.Tables;

namespace Web.Common.SoftDelete;

/// <summary>Cột dùng chung cho các bảng thùng rác.</summary>
public static class TrashColumns
{
    public static DataTableColumn<T> DeletedAt<T>(string title, Func<T, DateTime?> selector) =>
        new()
        {
            Key = "deletedAt",
            Title = title,
            Value = item => DateFormatter.FormatDateTime(selector(item)),
            Width = "170px",
        };

    public static DataTableColumn<T> Actions<T>(string title, RenderFragment<T> actions) =>
        new()
        {
            Key = "actions",
            Title = title,
            Template = actions,
            CellClass = "cell-actions",
            Width = "120px",
        };
}
