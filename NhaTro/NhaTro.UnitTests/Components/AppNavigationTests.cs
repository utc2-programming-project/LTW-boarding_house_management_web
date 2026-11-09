using Web.Components.Layout;

namespace NhaTro.UnitTests.Components;

public sealed class AppNavigationTests
{
    [Theory]
    [InlineData("Pricing")]
    [InlineData("MeterReadings")]
    [InlineData("Permissions")]
    [InlineData("Trash")]
    [InlineData("RoomTrash")]
    public void Sidebar_bo_muc_trung_va_muc_chua_co_route(string key) =>
        Assert.DoesNotContain(AppNavigation.Items, item => item.Key == key);

    [Fact]
    public void Sidebar_moi_muc_deu_co_route() =>
        Assert.All(AppNavigation.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.Href)));

    [Theory]
    [InlineData("/", "/", true)]
    [InlineData("/services", "/services", true)]
    [InlineData("/services/", "/services", true)]
    [InlineData("/services/prices", "/services", true)]
    [InlineData("/contracts/abc", "/contracts", true)]
    [InlineData("/servicesx", "/services", false)]
    [InlineData("/my/invoices", "/invoices", false)]
    public void IsMatch_chi_khop_nguyen_path_hoac_thu_muc_con(string path, string href, bool expected) =>
        Assert.Equal(expected, SidebarActive.IsMatch(path, href));

    [Fact]
    public void ResolveActiveHref_chon_muc_co_href_dai_nhat()
    {
        var hrefs = new[] { "/services", "/services/prices", "/services/readings" };

        Assert.Equal("/services/prices", SidebarActive.ResolveActiveHref("/services/prices?serviceId=1", hrefs));
        Assert.Equal("/services", SidebarActive.ResolveActiveHref("/services", hrefs));
        Assert.Equal("/services/readings", SidebarActive.ResolveActiveHref("/services/readings", hrefs));
    }

    [Fact]
    public void ResolveActiveHref_tra_null_khi_khong_khop() =>
        Assert.Null(SidebarActive.ResolveActiveHref("/khong-ton-tai", new[] { "/services", "/invoices" }));

    [Fact]
    public void Resolver_tra_dung_section_chain_va_item_sidebar()
    {
        var properties = NavigationResolver.Resolve("/properties");
        Assert.NotNull(properties);
        Assert.Equal("NavGroupProperty", properties!.Section.TitleKey);
        Assert.Equal(new[] { "Properties" }, properties.Chain.Select(item => item.Key));
        Assert.Equal("Properties", properties.SidebarItem?.Key);

        var trash = NavigationResolver.Resolve("/properties/trash");
        Assert.Equal(new[] { "Properties", "P2PropertyTrashTitle" }, trash!.Chain.Select(item => item.Key));
        Assert.Equal("Properties", trash.SidebarItem?.Key);

        var prices = NavigationResolver.Resolve("/services/prices?serviceId=1");
        Assert.Equal(new[] { "Services", "PricingNav" }, prices!.Chain.Select(item => item.Key));
        Assert.Equal("Services", prices.SidebarItem?.Key);

        var readings = NavigationResolver.Resolve("/meter-readings");
        Assert.Equal(new[] { "Services", "MeterReadingNavReadings" }, readings!.Chain.Select(item => item.Key));
        Assert.Equal("Services", readings.SidebarItem?.Key);
    }

    [Fact]
    public void Resolver_dashboard_la_trang_dich_khong_co_cap_cha()
    {
        var dashboard = NavigationResolver.Resolve("/dashboard");

        Assert.NotNull(dashboard);
        Assert.Null(dashboard!.Section.TitleKey);
        Assert.Equal("Dashboard", dashboard.Leaf.Key);
        Assert.Equal("Dashboard", dashboard.SidebarItem?.Key);
    }

    [Fact]
    public void Resolver_khong_nham_hoa_don_cua_toi_voi_hoa_don()
    {
        Assert.Equal("MyInvoices", NavigationResolver.Resolve("/my/invoices")!.Leaf.Key);
        Assert.Equal("MyInvoices", NavigationResolver.Resolve("/my-invoices")!.Leaf.Key);
        Assert.Equal("Invoices", NavigationResolver.Resolve("/invoices")!.Leaf.Key);
    }
}
