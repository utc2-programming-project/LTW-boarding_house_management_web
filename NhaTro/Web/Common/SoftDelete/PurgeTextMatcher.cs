namespace Web.Common.SoftDelete;

/// <summary>So khớp chuỗi xác nhận xóa vĩnh viễn: bỏ khoảng trắng hai đầu, không phân biệt hoa/thường.</summary>
public static class PurgeTextMatcher
{
    public static bool Matches(string? input, string? expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return string.Equals(input?.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
