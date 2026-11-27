using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Search;
using Web.Features.Search.Abstractions;
using Web.Features.Search.Components;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace NhaTro.UnitTests.Components;

public sealed class GlobalSearchBoxTests
{
    private static SearchHitDto Hit(int i) =>
        new("rooms", "door", $"Phòng {i:000}", "Nhà A", $"/rooms?roomId={i}");

    private sealed class FakeProvider : ISearchProvider
    {
        public string GroupKey => "rooms";
        public string Icon => "door";
        public int Order => 1;

        public Task<SearchGroupResult> SearchAsync(SearchContext context, CancellationToken ct)
        {
            var hits = context.Query.Contains("b1")
                ? new[] { Hit(9) }
                : Enumerable.Range(1, 5).Select(Hit).ToArray();
            return Task.FromResult(new SearchGroupResult(GroupKey, Icon, hits, HasMore: true));
        }
    }

    private sealed class FakeCallers(string role) : ISearchCallerProvider
    {
        public Task<PropertyCaller> GetCallerAsync() =>
            Task.FromResult(new PropertyCaller("user-1", role));
    }

    private sealed class KeyLocalizer : IStringLocalizer<SearchResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private static BunitContext CreateContext(string role = Roles.ChuTro)
    {
        var ctx = new BunitContext();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        ctx.Services.AddLogging();
        ctx.Services.AddSingleton<IStringLocalizer<SearchResources>>(new KeyLocalizer());
        ctx.Services.AddSingleton(new GlobalSearchService(
            new ISearchProvider[] { new FakeProvider() },
            new FakeCallers(role),
            NullLogger<GlobalSearchService>.Instance));
        return ctx;
    }

    [Fact]
    public void Click_ket_qua_dieu_huong_va_dong_dropdown()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<GlobalSearchBox>();
        cut.Find("input").Input("phong");
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("a[role=option]").Count), TimeSpan.FromSeconds(5));

        cut.Find("a[role=option]").Click();

        // E01: bUnit không tự đi theo href — kiểm tra điều hướng do component chủ động gọi
        // (NavigationManager.NavigateTo), chờ ngắn để tránh phụ thuộc thời điểm render lại.
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.Contains("roomId=1", nav.Uri, StringComparison.Ordinal));
        Assert.Empty(cut.FindAll("a[role=option]"));
    }

    [Fact]
    public void Esc_roi_bam_phim_mui_ten_va_enter_khong_con_dieu_huong()
    {
        // Dropdown đóng rồi thì phím mũi tên/Enter không được điều hướng vào item vô hình.
        using var ctx = CreateContext();
        var cut = ctx.Render<GlobalSearchBox>();
        cut.Find("input").Input("phong");
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("a[role=option]").Count), TimeSpan.FromSeconds(5));

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        var before = nav.Uri;

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("a[role=option]")));

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(before, nav.Uri);
        Assert.Empty(cut.FindAll("a[role=option]"));
    }

    [Fact]
    public void Bam_lai_vao_o_sau_Esc_mo_lai_ket_qua()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<GlobalSearchBox>();
        cut.Find("input").Input("phong");
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("a[role=option]").Count), TimeSpan.FromSeconds(5));

        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("a[role=option]")));

        // Từ khoá còn nguyên nên bấm lại vào ô phải mở lại dropdown.
        cut.Find("input").Click();
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("a[role=option]").Count), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Enter_AfterResultsShrink_NoThrow()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<GlobalSearchBox>();
        cut.Find("input").Input("phong");
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("a[role=option]").Count), TimeSpan.FromSeconds(5));
        for (var i = 0; i < 5; i++) cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.Find("input").Input("phong b1");
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("a[role=option]")), TimeSpan.FromSeconds(5));
        Assert.Null(Record.Exception(() => cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" })));
    }

    [Fact]
    public void Focus_khi_chua_go_khong_mo_dropdown()
    {
        // C01: trạng thái nhàn rỗng thì ô tìm bình thường, không mở popover.
        using var ctx = CreateContext();
        var cut = ctx.Render<GlobalSearchBox>();
        cut.Find("input").Focus();
        Assert.Empty(cut.FindAll(".global-search-popover"));
    }

    [Fact]
    public void Chi_go_dau_cach_khong_bao_khong_tim_thay()
    {
        // C02: query toàn dấu cách không được hiển thị "Không tìm thấy kết quả".
        using var ctx = CreateContext();
        var cut = ctx.Render<GlobalSearchBox>();
        cut.Find("input").Input("   ");
        Assert.Empty(cut.FindAll(".global-search-popover"));
    }

    [Fact]
    public void Dropdown_chia_nhom_co_tieu_de_phu_de_va_con_them()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<GlobalSearchBox>();
        cut.Find("input").Input("phong");
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("a[role=option]").Count), TimeSpan.FromSeconds(5));

        // B01: có tiêu đề nhóm, phụ đề và đường dẫn trên từng dòng.
        var title = cut.Find(".global-search-group-title");
        Assert.Contains("SearchGroupRooms", title.TextContent, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("a[role=option] .global-search-subtitle"));
        Assert.All(cut.FindAll("a[role=option]"), a => Assert.False(string.IsNullOrEmpty(a.GetAttribute("href"))));

        // B04: cờ HasMore được dùng để báo còn kết quả.
        Assert.NotEmpty(cut.FindAll(".global-search-more"));

        // ARIA: aria-selected phải là token "true"/"false", không phải chuỗi rỗng.
        Assert.All(cut.FindAll("a[role=option]"),
            a => Assert.Equal("false", a.GetAttribute("aria-selected")));
        cut.Find("input").KeyDown(new KeyboardEventArgs { Key = "ArrowDown" });
        cut.WaitForAssertion(() =>
            Assert.Equal("true", cut.FindAll("a[role=option]")[0].GetAttribute("aria-selected")));
    }

    [Fact]
    public void JsRegister_IsInvoked()
    {
        using var ctx = CreateContext();
        ctx.Render<GlobalSearchBox>();
        ctx.JSInterop.VerifyInvoke("globalSearch.register");
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.ChuTro)]
    [InlineData(Roles.QuanLy)]
    [InlineData(Roles.KhachThue)]
    public void O_tim_hien_voi_moi_vai_tro_duoc_phep(string role)
    {
        // A01/E04: ô tìm hiển thị theo AuthorizeView; mọi vai trò hệ thống đều thấy.
        using var ctx = CreateContext(role);
        Assert.NotEmpty(ctx.Render<GlobalSearchBox>().FindAll("input"));
    }
}
