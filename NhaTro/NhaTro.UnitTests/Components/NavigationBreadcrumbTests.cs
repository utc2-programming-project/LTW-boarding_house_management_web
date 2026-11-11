using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Web.Components.Layout;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

public sealed class NavigationBreadcrumbTests : BunitContext
{
    public NavigationBreadcrumbTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
    }

    [Fact]
    public void Dashboard_hien_mot_cap_khong_co_nhom()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/dashboard");

        var cut = Render<NavBreadcrumb>();

        var items = cut.FindAll("ol li");
        Assert.Single(items);
        Assert.Equal("Home", items[0].TextContent.Trim());
    }

    [Fact]
    public void Properties_hien_hai_cap_nhom_va_trang()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/properties");

        var cut = Render<NavBreadcrumb>();

        var items = cut.FindAll("ol li");
        Assert.Equal(2, items.Count);
        Assert.Contains("NavGroupProperty", items[0].TextContent);
        Assert.Contains("Properties", items[1].TextContent);

        // Cap nhom khong phai trang cuoi co chevron phan cach; trang cuoi co aria-current va khong co chevron
        Assert.NotNull(items[0].QuerySelector(".lucide-icon, svg"));
        Assert.Null(items[1].QuerySelector(".lucide-icon, svg"));
        Assert.NotNull(items[1].QuerySelector("[aria-current='page']"));
    }

    [Fact]
    public void Route_con_co_CurrentLabel_hien_ba_cap()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("http://localhost/contracts/123");

        var cut = Render<NavBreadcrumb>(parameters => parameters.Add(p => p.CurrentLabel, "HD-001"));

        var items = cut.FindAll("ol li");
        Assert.Equal(3, items.Count);
        Assert.Contains("NavGroupProperty", items[0].TextContent);
        Assert.Contains("Contracts", items[1].TextContent);
        Assert.Contains("HD-001", items[2].TextContent);

        Assert.NotNull(items[0].QuerySelector(".lucide-icon, svg"));
        Assert.NotNull(items[1].QuerySelector(".lucide-icon, svg"));
        Assert.Null(items[2].QuerySelector(".lucide-icon, svg"));
        Assert.NotNull(items[2].QuerySelector("[aria-current='page']"));
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

