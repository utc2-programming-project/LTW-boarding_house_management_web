using System.Globalization;
using System.Resources;
using Web.Features.Dashboard;

namespace NhaTro.UnitTests.Features.Dashboard;

public sealed class DashboardResourceTests
{
    [Fact]
    public void Both_languages_have_matching_nonempty_keys()
    {
        var manager = new ResourceManager("Web.Resources.Features.Dashboard.DashboardResource", typeof(DashboardResource).Assembly);
        var vi = Read("vi-VN");
        var en = Read("en-US");
        Assert.Equal(vi.Keys.Order(), en.Keys.Order());
        Assert.All(vi.Values.Concat(en.Values), v => Assert.False(string.IsNullOrWhiteSpace(v)));
        foreach (var key in new[] { "Loading", "Empty", "Error", "RoomsDenied", "TenantsDenied", "Unwired" }) Assert.Contains(key, vi.Keys);

        Dictionary<string, string> Read(string culture)
        {
            using var set = manager.GetResourceSet(CultureInfo.GetCultureInfo(culture), true, false)!;
            Assert.NotNull(set);
            return set.Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
        }
    }
}
