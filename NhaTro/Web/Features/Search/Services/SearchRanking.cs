using Web.Common.Utils;
namespace Web.Features.Search.Services;
public static class SearchRanking { public static int Score(string? text, string query) { var s=TextSearchNormalizer.Normalize(text); return s==query?0:s.StartsWith(query,StringComparison.Ordinal)?1:s.Contains(query,StringComparison.Ordinal)?2:int.MaxValue; } public static int Best(string query, params string?[] fields)=>fields.Select(x=>Score(x,query)).DefaultIfEmpty(int.MaxValue).Min(); }
