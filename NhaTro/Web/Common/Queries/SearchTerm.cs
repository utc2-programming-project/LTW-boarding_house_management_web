namespace Web.Common.Queries;

/// <summary>Chuẩn hóa từ khóa tìm kiếm: trim, bỏ rỗng, chặn từ khóa quá ngắn nếu cần.</summary>
public static class SearchTerm
{
    public const int MinLength = 1;

    public static string? Normalize(string? raw, int minLength = MinLength)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        return trimmed.Length < minLength ? null : trimmed;
    }
}
