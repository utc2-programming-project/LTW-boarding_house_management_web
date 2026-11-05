using Microsoft.Playwright;
using NhaTro.E2ETests.Rooms;

namespace NhaTro.E2ETests.Features.Utilities;

/// <summary>
/// VHT-126 TF-14: Playwright E2E for P5 service catalog (/services).
/// Requires a running app + seeded dev accounts:
///   E2E_BASE_URL, E2E_PASSWORD (required; tests are reported as Skipped otherwise),
///   E2E_OWNER_EMAIL (default chutro@nhatro.local), E2E_MANAGER_EMAIL / E2E_TENANT_EMAIL
///   (required only by their corresponding role tests; those tests are reported as Skipped otherwise).
/// Every selector below exists in the Utilities Razor components; unavailable prerequisites are skipped.
/// </summary>
public sealed class ServiceCatalogE2ETests : IAsyncLifetime
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL")?.TrimEnd('/');
    private static readonly string? Password = Environment.GetEnvironmentVariable("E2E_PASSWORD");
    private static readonly string OwnerEmail = Environment.GetEnvironmentVariable("E2E_OWNER_EMAIL") ?? "chutro@nhatro.local";
    private static readonly string? ManagerEmail = Environment.GetEnvironmentVariable("E2E_MANAGER_EMAIL");
    private static readonly string? TenantEmail = Environment.GetEnvironmentVariable("E2E_TENANT_EMAIL");

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public async Task InitializeAsync()
    {
        if (!E2EFactAttribute.Configured) return;
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    private async Task<IPage> LoginAsync(string email, int width = 1280)
    {
        var context = await _browser!.NewContextAsync(new()
        {
            ViewportSize = new() { Width = width, Height = 800 },
            // Local Development certificates are commonly self-signed on Windows.
            IgnoreHTTPSErrors = true,
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/login");
        await page.FillAsync("#login-email", email);
        await page.FillAsync("#login-password", Password!);
        await page.ClickAsync("button[type=submit]");
        await page.WaitForURLAsync(
            url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 20_000 });
        await page.GotoAsync($"{BaseUrl}/services");
        // Wait past the prerender-to-interactive handoff; prerendered rows can disappear
        // briefly while the Blazor Server circuit initializes.
        await page.WaitForFunctionAsync(
            "() => !document.querySelector('.page-loading') && document.querySelectorAll('tr.service-row').length >= 2",
            null,
            new() { Timeout = 20_000 });
        // The prerendered HTML may satisfy the row predicate before the interactive
        // circuit replaces it. Let that handoff finish, then require the common filter
        // toolbar to be present for the settled page.
        await page.WaitForTimeoutAsync(1_000);
        await page.Locator("#service-status-filter").WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20_000 });
        await page.WaitForFunctionAsync(
            "() => !document.querySelector('.page-loading') && document.querySelectorAll('tr.service-row').length >= 2",
            null,
            new() { Timeout = 20_000 });
        return page;
    }

    // Blazor Server only handles clicks once the circuit is connected; retry until the modal opens.
    private static async Task OpenCreateModalAsync(IPage page)
    {
        for (var i = 0; i < 10; i++)
        {
            await page.ClickAsync("#service-create-open-btn");
            try
            {
                await page.WaitForSelectorAsync("#service-name-input", new() { Timeout = 1500 });
                return;
            }
            catch (PlaywrightException exception) when (exception.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase)) { }
        }
        throw new InvalidOperationException("Create modal did not open.");
    }

    [E2EFact]
    public async Task Owner_sees_catalog_with_create_button_and_readonly_tariff_rows()
    {
        var page = await LoginAsync(OwnerEmail);

        Assert.True(await page.Locator("#service-create-open-btn").IsVisibleAsync());
        Assert.True(await page.Locator("tr.service-row").CountAsync() > 0);

        // UtilityTariff (electricity/water) rows: lock badge, disabled switch, no edit-price action.
        var readOnlyRows = page.Locator("tr.service-row:has(.readonly-badge)");
        Assert.True(await readOnlyRows.CountAsync() >= 2, "Expected seeded electricity and water tariff rows.");
        foreach (var row in await readOnlyRows.AllAsync())
        {
            Assert.True(await row.Locator("button.ui-switch").IsDisabledAsync());
            Assert.Equal(0, await row.Locator("[data-action='edit-price']").CountAsync());
        }
    }

    [E2EFact]
    public async Task Owner_creates_service_with_description_and_it_persists_after_reload()
    {
        var page = await LoginAsync(OwnerEmail);
        var name = $"E2E Service {Guid.NewGuid():N}"[..24];
        const string description = "Mo ta E2E luu SQL";

        await OpenCreateModalAsync(page);
        await page.FillAsync("#service-name-input", name);
        await page.FillAsync("#create-rate-input", "150000");
        var buildingSelect = page.Locator("#create-building-select");
        var selectedBuildingId = await buildingSelect.InputValueAsync();
        Assert.False(string.IsNullOrWhiteSpace(selectedBuildingId));
        Assert.NotEqual("all", selectedBuildingId);
        await page.FillAsync("#service-desc-input", description);
        await page.FillAsync("#create-note-input", "Ghi chu gia E2E");
        await page.ClickAsync("#service-create-submit-btn");

        await page.WaitForSelectorAsync($"tr.service-row:has-text('{name}')", new() { Timeout = 15_000 });
        await page.ReloadAsync();
        var row = page.Locator($"tr.service-row:has-text('{name}')");
        await row.WaitForAsync(new() { Timeout = 15_000 });
        Assert.Contains(description, await row.InnerTextAsync());
    }

    [E2EFact]
    public async Task Owner_toggle_switch_changes_state_and_survives_reload()
    {
        var page = await LoginAsync(OwnerEmail);
        var row = page.Locator("tr.service-row:not(:has(.readonly-badge))").First;
        var id = await row.GetAttributeAsync("data-service-id");
        var sw = row.Locator("button.ui-switch");
        var before = await sw.GetAttributeAsync("aria-checked");
        Assert.NotNull(id);
        Assert.NotNull(before);

        try
        {
            await sw.ClickAsync();
            await page.WaitForFunctionAsync(
                "([id, before]) => document.querySelector(`tr[data-service-id=\"${id}\"] button.ui-switch`)?.getAttribute('aria-checked') !== before",
                new object[] { id, before });

            await page.ReloadAsync();
            await page.WaitForSelectorAsync("tr.service-row");
            var after = await page.Locator($"tr[data-service-id='{id}'] button.ui-switch").GetAttributeAsync("aria-checked");
            Assert.NotEqual(before, after);
        }
        finally
        {
            await page.ReloadAsync();
            var currentSwitch = page.Locator($"tr[data-service-id='{id}'] button.ui-switch");
            await currentSwitch.WaitForAsync();
            if (await currentSwitch.GetAttributeAsync("aria-checked") != before)
            {
                await currentSwitch.ClickAsync();
                await page.WaitForFunctionAsync(
                    "([id, before]) => document.querySelector(`tr[data-service-id=\"${id}\"] button.ui-switch`)?.getAttribute('aria-checked') === before",
                    new object[] { id, before });
            }
        }
    }

    [E2EFact]
    public async Task Price_modal_shows_readonly_property_and_rejects_end_date_equal_to_start()
    {
        var page = await LoginAsync(OwnerEmail);
        for (var i = 0; i < 10; i++)
        {
            await page.Locator("tr.service-row [data-action='edit-price']").First.ClickAsync();
            try { await page.WaitForSelectorAsync("#new-rate-input", new() { Timeout = 1500 }); break; }
            catch (PlaywrightException exception) when (exception.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase)) { }
        }

        Assert.True(await page.Locator("#building-scope-readonly").IsDisabledAsync());

        await page.FillAsync("#new-rate-input", "99000");
        await page.FillAsync("#effective-from-input", "2030-01-10");
        await page.FillAsync("#effective-to-input", "2030-01-10");
        await page.Locator("#effective-to-input").BlurAsync();

        await page.Locator("#price-modal-title").WaitForAsync();
        Assert.True(await page.Locator(".field-error-text[role=alert]").First.IsVisibleAsync());
    }

    [E2EFact]
    public async Task Create_modal_has_no_all_buildings_option()
    {
        var page = await LoginAsync(OwnerEmail);
        await OpenCreateModalAsync(page);
        Assert.Equal(0, await page.Locator("#create-building-select option[value='all']").CountAsync());
    }

    [E2EFact]
    public async Task Owner_status_filter_returns_services_matching_active_state()
    {
        var page = await LoginAsync(OwnerEmail);
        var filter = page.Locator("#service-status-filter");
        Assert.True(await filter.IsVisibleAsync());

        var editableRow = page.Locator("tr.service-row:not(:has(.readonly-badge))").First;
        await editableRow.WaitForAsync();
        var serviceId = await editableRow.GetAttributeAsync("data-service-id");
        var originalState = await editableRow.Locator("button.ui-switch").GetAttributeAsync("aria-checked");
        Assert.NotNull(serviceId);
        Assert.NotNull(originalState);
        var wasActive = bool.Parse(originalState);

        try
        {
            if (wasActive)
            {
                await editableRow.Locator("button.ui-switch").ClickAsync();
                await page.WaitForFunctionAsync(
                    "id => document.querySelector(`tr[data-service-id=\"${id}\"] button.ui-switch`)?.getAttribute('aria-checked')?.toLowerCase() === 'false'",
                    serviceId);
            }

            await filter.SelectOptionAsync("inactive");
            var inactiveRows = page.Locator("tr.service-row");
            try
            {
                await page.WaitForFunctionAsync(
                    "id => { const rows = [...document.querySelectorAll('tr.service-row')]; return rows.some(row => row.getAttribute('data-service-id') === id) && rows.every(row => row.querySelector('button.ui-switch')?.getAttribute('aria-checked') === 'false'); }",
                    serviceId,
                    new() { Timeout = 15_000 });
            }
            catch (TimeoutException exception)
            {
                var selectedStatus = await filter.InputValueAsync();
                var rowStates = await inactiveRows.EvaluateAllAsync<string[]>(
                    "rows => rows.map(row => `${row.getAttribute('data-service-id')}:${row.querySelector('button.ui-switch')?.getAttribute('aria-checked')}`)");
                throw new InvalidOperationException(
                    $"Inactive filter diagnostic: selected='{selectedStatus}', expectedServiceId='{serviceId}', rows=[{string.Join(", ", rowStates)}].",
                    exception);
            }

            Assert.Contains(serviceId, await inactiveRows.EvaluateAllAsync<string[]>("rows => rows.map(row => row.getAttribute('data-service-id') || '')"));
            foreach (var row in await inactiveRows.AllAsync())
            {
                Assert.Equal("false", await row.Locator("button.ui-switch").GetAttributeAsync("aria-checked"));
            }

            await filter.SelectOptionAsync("active");
            await page.WaitForFunctionAsync(
                "id => { const rows = [...document.querySelectorAll('tr.service-row')]; return rows.length > 0 && !rows.some(row => row.getAttribute('data-service-id') === id) && rows.every(row => row.querySelector('button.ui-switch')?.getAttribute('aria-checked') === 'true'); }",
                serviceId,
                new() { Timeout = 15_000 });
            var activeRows = page.Locator("tr.service-row");
            Assert.DoesNotContain(serviceId, await activeRows.EvaluateAllAsync<string[]>("rows => rows.map(row => row.getAttribute('data-service-id') || '')"));
            Assert.True(await activeRows.CountAsync() > 0, "Expected active services, including read-only tariffs.");
            foreach (var row in await activeRows.AllAsync())
            {
                Assert.Equal("true", await row.Locator("button.ui-switch").GetAttributeAsync("aria-checked"));
            }
        }
        finally
        {
            await page.ReloadAsync();
            var switchButton = page.Locator($"tr[data-service-id='{serviceId}'] button.ui-switch");
            await switchButton.WaitForAsync();
            if (bool.Parse((await switchButton.GetAttributeAsync("aria-checked"))!) != wasActive)
            {
                await switchButton.ClickAsync();
                await page.WaitForFunctionAsync(
                    "([id, state]) => document.querySelector(`tr[data-service-id=\"${id}\"] button.ui-switch`)?.getAttribute('aria-checked')?.toLowerCase() === String(state).toLowerCase()",
                    new object[] { serviceId, wasActive });
            }
        }
    }

    [E2EFact("E2E_MANAGER_EMAIL")]
    public async Task Manager_has_no_create_button_and_switches_are_disabled()
    {
        var page = await LoginAsync(ManagerEmail!);
        Assert.Equal(0, await page.Locator("#service-create-open-btn").CountAsync());
        Assert.Equal(0, await page.Locator("tr.service-row button.ui-switch:not([disabled])").CountAsync());
    }

    [E2EFact("E2E_TENANT_EMAIL")]
    public async Task Tenant_has_no_write_actions()
    {
        var page = await LoginAsync(TenantEmail!);
        Assert.Equal(0, await page.Locator("#service-create-open-btn").CountAsync());
        Assert.Equal(0, await page.Locator("[data-action='edit-price']").CountAsync());
    }

    [E2EFact]
    public async Task Language_switch_to_English_changes_page_text()
    {
        var page = await LoginAsync(OwnerEmail);
        var viBefore = await page.Locator("h1").First.InnerTextAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "EN", Exact = true }).ClickAsync();
        await page.WaitForFunctionAsync("b => document.querySelector('h1')?.innerText !== b", viBefore);
        Assert.NotEqual(viBefore, await page.Locator("h1").First.InnerTextAsync());
    }

    [E2EFact]
    public async Task Page_and_create_modal_do_not_overflow_horizontally_on_small_screens()
    {
        foreach (var width in new[] { 320, 375, 390 })
        {
            var page = await LoginAsync(OwnerEmail, width);
            Assert.True(await HasNoHorizontalOverflowAsync(page), $"page overflows at {width}px");

            await OpenCreateModalAsync(page);
            Assert.True(await HasNoHorizontalOverflowAsync(page), $"create modal overflows at {width}px");
            await page.Context.CloseAsync();
        }
    }

    private static Task<bool> HasNoHorizontalOverflowAsync(IPage page) =>
        page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1");
}
