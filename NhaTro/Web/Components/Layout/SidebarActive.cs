namespace Web.Components.Layout;

/// <summary>
/// Chọn đúng một mục sidebar đang mở: so khớp theo path đầy đủ hoặc tiền tố
/// ranh giới thư mục, rồi lấy href dài nhất để mục cha không sáng cùng mục con.
/// </summary>
public static class SidebarActive
{
    /// <summary>Trả về href của mục nên active, hoặc null nếu URL không thuộc mục nào.</summary>
    public static string? ResolveActiveHref(string? path, IEnumerable<string?> hrefs)
    {
        string? best = null;
        var bestLength = -1;
        foreach (var href in hrefs)
        {
            if (!IsMatch(path, href)) continue;
            var length = Normalize(href).Length;
            if (length <= bestLength) continue;
            best = href;
            bestLength = length;
        }
        return best;
    }

    /// <summary>True khi path trùng href hoặc nằm dưới href (không khớp tiền tố nửa chừng).</summary>
    public static bool IsMatch(string? path, string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return false;
        var current = Normalize(path);
        var target = Normalize(href);
        if (target == "/") return current == "/";
        return current.Equals(target, StringComparison.OrdinalIgnoreCase)
            || current.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Bỏ query/fragment, thêm "/" đầu và bỏ "/" cuối (giữ "/" cho trang chủ).</summary>
    public static string Normalize(string? value)
    {
        var path = (value ?? string.Empty).Split('?', '#')[0].Trim();
        if (path.Length == 0) return "/";
        if (path[0] != '/') path = "/" + path;
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}
