namespace Web.Components.Layout;

/// <summary>Kết quả khớp path với cây điều hướng.</summary>
public sealed record NavigationMatch(
    NavigationSection Section,
    IReadOnlyList<NavigationItem> Chain,
    NavigationItem Leaf,
    NavigationItem? SidebarItem);

/// <summary>Khớp URL hiện tại với item sâu nhất trong <see cref="AppNavigation"/>.</summary>
public static class NavigationResolver
{
    public static NavigationMatch? Resolve(string? path)
    {
        var current = SidebarActive.Normalize(path);
        NavigationMatch? best = null;
        var bestLength = -1;

        foreach (var section in AppNavigation.Sections)
        {
            foreach (var item in section.Items)
            {
                Visit(section, item, [], current, ref best, ref bestLength);
            }
        }

        return best;

        static void Visit(
            NavigationSection section,
            NavigationItem item,
            IReadOnlyList<NavigationItem> parents,
            string current,
            ref NavigationMatch? best,
            ref int bestLength)
        {
            var chain = new List<NavigationItem>(parents.Count + 1);
            chain.AddRange(parents);
            chain.Add(item);

            var length = MatchLength(current, item);
            if (length > bestLength)
            {
                bestLength = length;
                best = new NavigationMatch(section, chain, item, SidebarItemOf(chain));
            }

            if (item.Children is null) return;
            foreach (var child in item.Children)
            {
                Visit(section, child, chain, current, ref best, ref bestLength);
            }
        }
    }

    private static int MatchLength(string current, NavigationItem item)
    {
        var length = SidebarActive.IsMatch(current, item.Href)
            ? SidebarActive.Normalize(item.Href).Length
            : -1;
        if (item.Aliases is null) return length;

        foreach (var alias in item.Aliases)
        {
            if (!SidebarActive.IsMatch(current, alias)) continue;
            length = Math.Max(length, SidebarActive.Normalize(alias).Length);
        }
        return length;
    }

    private static NavigationItem? SidebarItemOf(IReadOnlyList<NavigationItem> chain)
    {
        for (var i = chain.Count - 1; i >= 0; i--)
        {
            if (chain[i].ShowInSidebar) return chain[i];
        }
        return null;
    }
}
