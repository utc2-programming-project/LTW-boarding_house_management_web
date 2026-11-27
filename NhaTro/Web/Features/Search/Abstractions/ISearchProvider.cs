using Web.Features.Properties;
using Web.Features.Search.DTOs;

namespace Web.Features.Search.Abstractions;

public interface ISearchProvider
{
    string GroupKey { get; }
    string Icon { get; }
    int Order { get; }

    /// <summary>
    /// Khoá tài nguyên đã bản địa hoá của nhãn nhóm (B05), ví dụ <c>SearchGroupRooms</c>.
    /// UI dịch qua <c>IStringLocalizer</c>; provider không phụ thuộc tầng trình bày.
    /// </summary>
    string GroupLabelKey => $"SearchGroup{char.ToUpperInvariant(GroupKey[0])}{GroupKey[1..]}";

    /// <summary>Timeout riêng từng provider (D-TK-07); mặc định 2 giây.</summary>
    TimeSpan Timeout => TimeSpan.FromSeconds(2);

    /// <summary>
    /// Vai trò có nhóm này không (O-TK-04). Provider vẫn tự áp scope bên trong;
    /// cờ này chỉ để lõi và UI không chạy/hiện nhóm chắc chắn rỗng.
    /// </summary>
    bool CanRun(PropertyCaller caller) => true;

    Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct);
}
