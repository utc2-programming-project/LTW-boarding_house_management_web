using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Web.Features.Search.Services;

/// <summary>
/// D01 / O-TK-02: lọc thô phía DB trước khi nạp dòng về bộ nhớ.
/// <para>
/// Từ khoá đã được <c>TextSearchNormalizer</c> bỏ dấu ở C#, nên điều kiện DB phải so khớp
/// không phân biệt dấu/hoa-thường — dùng collation <c>Latin1_General_100_CI_AI</c> (đã kiểm chứng:
/// 'Phòng' LIKE '%phong%' và 'Đức' LIKE '%duc%' đều khớp trên SQL Server/LocalDB).
/// </para>
/// <para>
/// Predicate DB chỉ là <b>tập cha</b> của các khớp mà <see cref="SearchRanking"/> chấp nhận
/// (mọi token phải nằm trong ít nhất một trường), nên kết quả cuối vẫn do ranking trong bộ nhớ quyết định.
/// Nhờ vậy không phải nạp cả bảng về RAM mà vẫn giữ đúng thứ tự khớp.
/// </para>
/// </summary>
public static class SearchDbFilter
{
    public const string Collation = "Latin1_General_100_CI_AI";

    private static readonly MethodInfo LikeMethod = typeof(DbFunctionsExtensions).GetMethod(
        nameof(DbFunctionsExtensions.Like),
        [typeof(DbFunctions), typeof(string), typeof(string)])!;

    // EF.Functions.Collate là generic (Collate<T>) ở package Relational; đóng lại cho string.
    private static readonly MethodInfo CollateMethod = typeof(RelationalDbFunctionsExtensions)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(RelationalDbFunctionsExtensions.Collate) && method.IsGenericMethodDefinition)
        .MakeGenericMethod(typeof(string));

    /// <summary>Các token của từ khoá đã chuẩn hoá (bỏ dấu, thường hoá).</summary>
    public static string[] Tokens(string normalizedQuery) =>
        normalizedQuery.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Điều kiện "mọi token đều khớp vào ít nhất một trường", tương đương bậc khớp tổng hợp
    /// của <see cref="SearchRanking.Best"/> nhưng chạy trong SQL.
    /// </summary>
    public static IQueryable<T> WhereMatches<T>(
        this IQueryable<T> source,
        IReadOnlyList<string> tokens,
        params Expression<Func<T, string?>>[] fields)
    {
        if (tokens.Count == 0) return source;

        var parameter = Expression.Parameter(typeof(T), "row");
        Expression? allTokens = null;

        foreach (var token in tokens)
        {
            Expression? anyField = null;
            foreach (var field in fields)
            {
                var like = BuildLike(parameter, field, token);
                anyField = anyField is null ? like : Expression.OrElse(anyField, like);
            }

            if (anyField is null) continue;
            allTokens = allTokens is null ? anyField : Expression.AndAlso(allTokens, anyField);
        }

        if (allTokens is null) return source;
        return source.Where(Expression.Lambda<Func<T, bool>>(allTokens, parameter));
    }

    /// <summary>
    /// Điều kiện "ít nhất một token khớp một trường". Dùng cho nguồn phụ (ví dụ tên khách
    /// trong bảng thành viên hợp đồng) rồi hợp bằng UNION, để tập DB luôn là tập cha của khớp C#
    /// khi các token nằm rải ở nhiều nguồn khác nhau.
    /// </summary>
    public static IQueryable<T> WhereMatchesAnyToken<T>(
        this IQueryable<T> source,
        IReadOnlyList<string> tokens,
        params Expression<Func<T, string?>>[] fields)
    {
        if (tokens.Count == 0) return source;

        var parameter = Expression.Parameter(typeof(T), "row");
        Expression? anyMatch = null;

        foreach (var token in tokens)
        {
            foreach (var field in fields)
            {
                var like = BuildLike(parameter, field, token);
                anyMatch = anyMatch is null ? like : Expression.OrElse(anyMatch, like);
            }
        }

        if (anyMatch is null) return source;
        return source.Where(Expression.Lambda<Func<T, bool>>(anyMatch, parameter));
    }

    /// <summary>
    /// Mẫu LIKE của một token. Bỏ ký tự đại diện để điều kiện DB không bao giờ hẹp hơn khớp C#
    /// (bỏ rộng ra vẫn an toàn vì ranking trong bộ nhớ lọc lại).
    /// </summary>
    public static string Pattern(string token)
    {
        var cleaned = new string(token.Where(ch => ch is not ('%' or '_' or '[' or ']')).ToArray());
        return $"%{cleaned}%";
    }

    private static Expression BuildLike<T>(ParameterExpression parameter, Expression<Func<T, string?>> field, string token)
    {
        var body = new ParameterRebinder(field.Parameters[0], parameter).Visit(field.Body)!;
        var collated = Expression.Call(CollateMethod, Expression.Constant(EF.Functions), body, Expression.Constant(Collation));
        return Expression.Call(LikeMethod, Expression.Constant(EF.Functions), collated, Expression.Constant(Pattern(token)));
    }

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
