using System.Globalization;
using System.Text;

namespace Web.Common.Utils;

/// <summary>
/// Chuỗi tìm kiếm đã bỏ dấu và lowercase — để người dùng gõ "cccd mat truoc" vẫn khớp "CCCD mặt trước".
/// </summary>
public static class TextSearchNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        // NFD không tách "đ" thành "d", phải thay thủ công.
        return builder.ToString().Normalize(NormalizationForm.FormC).Replace('đ', 'd');
    }

    public static bool Contains(string? source, string? keyword)
    {
        var needle = Normalize(keyword);
        if (needle.Length == 0) return true;
        return Normalize(source).Contains(needle, StringComparison.Ordinal);
    }
}
