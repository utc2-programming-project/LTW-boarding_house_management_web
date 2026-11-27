using Web.Common.Utils;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Search.Abstractions;
using Web.Features.Search.DTOs;

namespace Web.Features.Search.Services;

public sealed class GlobalSearchService(
    IEnumerable<ISearchProvider> providers,
    ISearchCallerProvider callers,
    ILogger<GlobalSearchService> log)
{
    /// <summary>
    /// Tương thích API cũ (query/group/limit/offset). Chuyển offset sang số trang để
    /// mọi nơi đi qua <see cref="SearchRequest"/> (B05).
    /// </summary>
    public Task<SearchResponse> SearchAsync(
        string? query, string? group, int limit, int offset, CancellationToken ct)
    {
        var request = new SearchRequest(query, group, limit);
        var page = offset <= 0 ? 0 : offset / request.EffectiveLimit;
        return SearchAsync(request with { Page = page }, ct);
    }

    public async Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken ct)
    {
        var raw = request.Query?.Trim() ?? string.Empty;
        var normalized = TextSearchNormalizer.Normalize(raw);
        if (normalized.Length < 2) return new([]);

        PropertyCaller caller;
        try
        {
            caller = await callers.GetCallerAsync();
        }
        catch (UnauthorizedAccessException)
        {
            return new([]);
        }

        var selected = providers
            .Where(p => p.CanRun(caller))
            .Where(p => string.IsNullOrEmpty(request.Group) || p.GroupKey == request.Group)
            .ToArray();
        var context = new SearchContext(
            caller, raw, normalized, request.EffectiveLimit, Math.Max(0, request.Offset));

        var results = await Task.WhenAll(selected.Select(p => RunAsync(p, context, ct)));
        var labeled = results
            .Select(r => r with { GroupLabelKey = selected.First(p => p.GroupKey == r.GroupKey).GroupLabelKey })
            .ToArray();
        var failedGroups = labeled.Where(r => r.Failed).Select(r => r.GroupKey).ToArray();
        var groups = labeled
            .Where(r => !r.Failed && r.Hits.Count > 0)
            .OrderBy(r => selected.First(p => p.GroupKey == r.GroupKey).Order)
            .ToArray();

        // B06 / O-TK-05: KHÔNG log nội dung từ khoá (bản thân đã là PII: tên/SĐT).
        // Chỉ log ẩn danh độ dài + số nhóm/kết quả để theo dõi chất lượng tìm.
        log.LogDebug(
            "Global search: queryLength={QueryLength}, groups={GroupCount}, hits={HitCount}",
            raw.Length, groups.Length, groups.Sum(g => g.Hits.Count));

        return new SearchResponse(groups, failedGroups, selected.Length);
    }

    /// <summary>Nhóm mà người dùng hiện tại được phép tìm — dựng bộ lọc nhóm ở /search (B03).</summary>
    public async Task<IReadOnlyList<SearchGroupInfo>> GetGroupsAsync(CancellationToken ct = default)
    {
        PropertyCaller caller;
        try
        {
            caller = await callers.GetCallerAsync();
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        return providers
            .Where(p => p.CanRun(caller))
            .OrderBy(p => p.Order)
            .Select(p => new SearchGroupInfo(p.GroupKey, p.Icon, p.Order, p.GroupLabelKey))
            .ToArray();
    }

    private async Task<SearchGroupResult> RunAsync(ISearchProvider provider, SearchContext context, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(provider.Timeout);

        try
        {
            return await provider.SearchAsync(context, timeout.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Một provider lỗi/timeout chỉ mất nhóm đó (mục 3 "Lỗi một phần").
            log.LogWarning("Global search provider {Provider} failed: {Type}", provider.GroupKey, ex.GetType().Name);
            return new SearchGroupResult(provider.GroupKey, provider.Icon, [], false, Failed: true);
        }
    }
}
