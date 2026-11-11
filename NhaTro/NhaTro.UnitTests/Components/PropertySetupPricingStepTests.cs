using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using System.Security.Claims;
using Web.Data;
using Web.Features.Accounts;
using Web.Components.Shared.Feedback;
using Web.Features.Properties.Components;
using Web.Features.Properties.Entities;
using Web.Features.Utilities;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

/// <summary>PLAN-013: bước giá trong wizard thiết lập nhà trọ.</summary>
public sealed class PropertySetupPricingStepTests : BunitContext
{
    private const string OwnerId = "chu-tro-1";
    private const int TargetPropertyId = 1;
    private const int SourcePropertyId = 2;

    private static readonly DateTimeOffset Now = new(2026, 11, 9, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly ServiceProvider _innerProvider;

    public PropertySetupPricingStepTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLogging();
        Services.AddSingleton<TimeProvider>(_clock);
        Services.AddSingleton(new ToastService());
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer<SharedResource>());
        Services.AddSingleton<IStringLocalizer<UtilityResources>>(new KeyLocalizer<UtilityResources>());

        var inner = new ServiceCollection();
        inner.AddScoped(_ => CreateContext());
        inner.AddScoped<IServiceCatalogService>(sp =>
            new ServiceCatalogService(sp.GetRequiredService<ApplicationDbContext>(), _clock));
        _innerProvider = inner.BuildServiceProvider();

        Services.AddSingleton(new ServiceCatalogPageService(
            new FixedAuthenticationStateProvider(Owner()),
            _innerProvider.GetRequiredService<IServiceScopeFactory>(),
            _clock,
            NullLogger<ServiceCatalogPageService>.Instance));
    }

    [Fact]
    public async Task Copy_tu_nha_tro_nguon_bao_ket_qua_va_cap_nhat_trang_thai()
    {
        await SeedAsync();
        var cut = RenderStep();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".setup-price-card").Count));
        Assert.Contains("P2SetupPriceNoServices", cut.FindAll(".setup-price-card")[2].TextContent);

        cut.Find("input[value='copy']").Change("copy");
        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#setup-copy-source")));
        cut.Find("#setup-copy-source").Change(SourcePropertyId.ToString());
        cut.Find(".setup-option-body button.btn-primary").Click();

        cut.WaitForAssertion(() => Assert.Contains("P2SetupPriceCopyResult", cut.Find(".p2-alert.success").TextContent));
        cut.WaitForAssertion(() => Assert.Contains("P2SetupPriceServiceCount", cut.FindAll(".setup-price-card")[2].TextContent));
    }

    [Fact]
    public async Task Nut_quay_lai_va_hoan_tat_goi_callback()
    {
        await SeedAsync();
        var back = 0;
        var finish = 0;
        var cut = RenderStep(parameters => parameters
            .Add(component => component.OnBack, EventCallback.Factory.Create(this, () => back++))
            .Add(component => component.OnFinish, EventCallback.Factory.Create(this, () => finish++)));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".setup-step-actions button.btn-secondary")));
        cut.Find(".setup-step-actions button.btn-secondary").Click();
        cut.Find(".setup-step-actions button.btn-primary").Click();

        Assert.Equal(1, back);
        Assert.Equal(1, finish);
    }

    [Fact]
    public async Task Chon_tu_nhap_mo_modal_tao_dich_vu_cua_nha_tro()
    {
        await SeedAsync();
        var cut = RenderStep();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("input[value='manual']")));
        cut.Find("input[value='manual']").Change("manual");

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(".setup-option-body button.btn-secondary")));
        cut.Find(".setup-option-body button.btn-secondary").Click();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll("#service-name-input")));
        Assert.NotEmpty(cut.FindAll("#create-modal-title"));
    }

    private IRenderedComponent<PropertySetupPricingStep> RenderStep(
        Action<ComponentParameterCollectionBuilder<PropertySetupPricingStep>>? configure = null) =>
        Render<PropertySetupPricingStep>(parameters =>
        {
            parameters
                .Add(component => component.PropertyId, TargetPropertyId)
                .Add(component => component.PropertyName, "Trọ đích");
            configure?.Invoke(parameters);
        });

    private async Task SeedAsync()
    {
        await using var db = CreateContext();
        db.Properties.AddRange(
            new Property { Id = TargetPropertyId, Name = "Trọ đích", OwnerId = OwnerId, Address = "1 A" },
            new Property { Id = SourcePropertyId, Name = "Trọ nguồn", OwnerId = OwnerId, Address = "2 B" });

        var catalog = new ServiceCatalog
        {
            PropertyId = SourcePropertyId,
            IsActive = true,
            CreatedAt = Now.UtcDateTime,
        };
        catalog.PriceHistory.Add(new ServicePriceHistory
        {
            Name = "Vệ sinh",
            Unit = "Tháng",
            CalculationMethod = "Cố định",
            UnitPrice = 50_000m,
            EffectiveDate = new DateOnly(2026, 1, 1),
            CreatedAt = Now.UtcDateTime,
            CreatedByUserId = OwnerId,
        });
        db.ServiceCatalogs.Add(catalog);

        await db.SaveChangesAsync();
    }

    private ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .Options);

    private static ClaimsPrincipal Owner()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, OwnerId),
            new Claim(ClaimTypes.Role, Roles.ChuTro),
        ], "test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class FixedAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }

    private sealed class KeyLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
