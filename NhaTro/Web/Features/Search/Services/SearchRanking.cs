using Web.Common.Utils;

namespace Web.Features.Search.Services;

/// <summary>
/// Xếp hạng khớp theo spec mục 1.4/5: khớp chính xác → đầu chuỗi → chứa → khớp đủ các từ.
/// Tách từ cho phép "phong 101" khớp "Phòng 101" (sau khi bỏ dấu).
/// </summary>
public static class SearchRanking
{
    public static int Score(string? text, string query)
    {
        var normalizedQuery = TextSearchNormalizer.Normalize(query);
        if (normalizedQuery.Length == 0) return int.MaxValue;

        var source = TextSearchNormalizer.Normalize(text);
        if (source.Length == 0) return int.MaxValue;

        if (source == normalizedQuery) return 0;
        if (source.StartsWith(normalizedQuery, StringComparison.Ordinal)) return 1;
        if (source.Contains(normalizedQuery, StringComparison.Ordinal)) return 2;
        return MatchesAllTokens(source, normalizedQuery) ? 3 : int.MaxValue;
    }

    public static int Best(string query, params string?[] fields)
    {
        var best = int.MaxValue;
        var parts = new List<string>(fields.Length);
        foreach (var field in fields)
        {
            best = Math.Min(best, Score(field, query));
            var normalized = TextSearchNormalizer.Normalize(field);
            if (normalized.Length > 0) parts.Add(normalized);
        }

        // Cho phép từ khoá nhiều từ khớp khi mỗi từ nằm ở một trường khác nhau
        // (ví dụ "nguyen 101" = tên khách + mã phòng).
        if (parts.Count > 1 && best > 3)
        {
            best = Math.Min(best, Score(string.Join(' ', parts), query));
        }

        return best;
    }

    private static bool MatchesAllTokens(string source, string query)
    {
        var tokens = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length < 2) return false;

        foreach (var token in tokens)
        {
            if (!source.Contains(token, StringComparison.Ordinal)) return false;
        }

        return true;
    }
}
