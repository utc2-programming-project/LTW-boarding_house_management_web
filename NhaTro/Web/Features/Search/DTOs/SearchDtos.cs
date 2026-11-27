namespace Web.Features.Search.DTOs;

/// <summary>D03: trần số kết quả mỗi nhóm — chặn caller nội bộ truyền limit quá lớn.</summary>
public static class SearchLimits
{
    public const int MaxPerGroup = 50;
}

/// <summary>
/// Yêu cầu tìm kiếm (spec mục 7). Người dùng/vai trò **không** nằm trong request:
/// server tự suy từ phiên đăng nhập để client không thể nâng quyền.
/// </summary>
public sealed record SearchRequest(string? Query, string? Group = null, int Limit = 5, int Page = 0)
{
    /// <summary>Limit sau khi kẹp vào [1, <see cref="SearchLimits.MaxPerGroup"/>].</summary>
    public int EffectiveLimit => Math.Clamp(Limit, 1, SearchLimits.MaxPerGroup);

    public int Offset => Page <= 0 ? 0 : Page * EffectiveLimit;
}

public sealed record SearchHitDto(string GroupKey, string Icon, string Title, string? Subtitle, string Href);

/// <summary>
/// <see cref="GroupLabelKey"/> là khoá tài nguyên đã bản địa hoá của nhãn nhóm (B05),
/// UI dịch qua <c>IStringLocalizer</c> thay vì tự ghép từ <see cref="GroupKey"/>.
/// </summary>
public sealed record SearchGroupResult(
    string GroupKey,
    string Icon,
    IReadOnlyList<SearchHitDto> Hits,
    bool HasMore,
    bool Failed = false,
    string GroupLabelKey = "");

/// <summary>Mô tả nhóm mà người dùng hiện tại được phép tìm — dùng dựng bộ lọc nhóm ở /search (B03).</summary>
public sealed record SearchGroupInfo(string GroupKey, string Icon, int Order, string GroupLabelKey);

/// <summary>
/// <see cref="FailedGroups"/> chứa nhóm bị lỗi/timeout để UI phân biệt "lỗi một phần" với "rỗng".
/// <see cref="ProviderCount"/> là số provider đã chạy, dùng để nhận biết lỗi toàn bộ.
/// </summary>
public sealed record SearchResponse(
    IReadOnlyList<SearchGroupResult> Groups,
    IReadOnlyList<string>? FailedGroups = null,
    int ProviderCount = 0);
