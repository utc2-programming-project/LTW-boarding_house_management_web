using Microsoft.AspNetCore.Components;

namespace Web.Components.Shared.Tables;

/// <summary>Mô tả một cột của <see cref="DataTable{TItem}"/>.</summary>
public sealed class DataTableColumn<TItem>
{
    /// <summary>Khóa cột, gửi về nơi lấy dữ liệu qua <see cref="DataTableRequest"/> để sắp xếp/lọc.</summary>
    public required string Key { get; init; }

    /// <summary>Tiêu đề đã dịch.</summary>
    public required string Title { get; init; }

    /// <summary>Giá trị chữ của ô. Dùng <c>MoneyFormatter</c>/<c>DateFormatter</c> cho tiền và ngày.</summary>
    public Func<TItem, string?>? Value { get; init; }

    /// <summary>Nội dung tùy biến (badge, nút...). Nếu có thì dùng thay cho <see cref="Value"/>.</summary>
    public RenderFragment<TItem>? Template { get; init; }

    /// <summary>Cột số/tiền: căn phải, dùng tabular-nums.</summary>
    public bool IsNumeric { get; init; }

    public bool Sortable { get; init; }

    public bool Filterable { get; init; }
}
