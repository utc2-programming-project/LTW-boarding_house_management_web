using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Features.Accounts;
using Web.Features.Properties.Services;
using Web.Features.Search;
using Web.Features.Search.Abstractions;
using Web.Features.Search.Components;
using Web.Features.Search.DTOs;
using Web.Features.Search.Services;

namespace NhaTro.UnitTests.Components;

public sealed class TempSearchProbeTests
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
            return Task.FromResult(new SearchGroupResult(GroupKey, Icon, hits, false));
        }
    }

    private sealed class CallerAuth(string role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var id = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Role, role)],
                "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(id)));
        }
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
        ctx.Services.AddSingleton<AuthenticationStateProvider>(new CallerAuth(role));
        ctx.Services.AddSingleton<IStringLocalizer<SearchResources>>(new KeyLocalizer());
        ctx.Services.AddSingleton(new GlobalSearchService(
            new ISearchProvider[] { new FakeProvider() },
            new P2CallerProvider(new CallerAuth(role), null!),
            NullLogger<GlobalSearchService>.Instance));
        return ctx;
    }

    [Fact]
    public async Task Popover_PreventsMousedownDefault_AndClickNavigates()
    {
        using var ctx = CreateContext();
        var cut = ctx.Render<GlobalSearchBox>();
        cut.Find("input").Input("phong");
        cut.WaitForAssertion(() => Assert.Equal(5, cut.FindAll("a[role=option]").Count), TimeSpan.FromSeconds(5));

        var popover = cut.Find(".global-search-popover");
        await File.WriteAllTextAsync(
            Path.Combine(Path.GetTempPath(), "search-popover.html"),
            string.Join("\n", popover.Attributes.Select(a => $"{a.Name}={a.Value}")) + "\n---\n" + popover.OuterHtml);

        cut.Find("a[role=option]").Click();

        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        Assert.Contains("roomId=1", nav.Uri, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("a[role=option]"));
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
    public void JsRegister_IsInvoked()
    {
        using var ctx = CreateContext();
        ctx.Render<GlobalSearchBox>();
        ctx.JSInterop.VerifyInvoke("globalSearch.register");
    }

    [Fact]
    public void Admin_Hidden()
    {
        using var ctx = CreateContext(Roles.Admin);
        Assert.Empty(ctx.Render<GlobalSearchBox>().FindAll("input"));
    }
}
