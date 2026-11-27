using Microsoft.Playwright;
using NhaTro.E2ETests.Rooms;

namespace NhaTro.E2ETests.Features.Search;

/// <summary>
/// E2E cho tìm kiếm toàn cục (O-tim-kiem). Yêu cầu app đang chạy + tài khoản seed:
///   E2E_BASE_URL, E2E_PASSWORD (thiếu thì bỏ qua), E2E_OWNER_EMAIL (mặc định chutro@nhatro.local).
/// Phủ: dropdown nhóm + bàn phím + điều hướng, nút "Xem tất cả" sang /search, ô tìm rỗng không mở.
/// </summary>
public sealed class GlobalSearchE2ETests : IAsyncLifetime
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL")?.TrimEnd('/');
    private static readonly string? Password = Environment.GetEnvironmentVariable("E2E_PASSWORD");
    private static readonly string OwnerEmail = Environment.GetEnvironmentVariable("E2E_OWNER_EMAIL") ?? "chutro@nhatro.local";

    private const string InputSelector = ".topbar-search-input";
    private const string OptionSelector = "#global-search-results a[role=option]";

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

    private async Task<IPage> LoginAsync()
    {
        var context = await _browser!.NewContextAsync(new()
        {
            ViewportSize = new() { Width = 1280, Height = 800 },
            IgnoreHTTPSErrors = true,
        });
        var page = await context.NewPageAsync();
        await page.GotoAsync($"{BaseUrl}/login");
        await page.FillAsync("#login-email", OwnerEmail);
        await page.FillAsync("#login-password", Password!);
        await page.ClickAsync("button[type=submit]");
        await page.WaitForURLAsync(
            url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 20_000 });

        // Tài khoản nhiều ngữ cảnh (vd chutro01) dừng ở /select-role; phải chọn vai trò trước.
        if (page.Url.Contains("/select-role", StringComparison.OrdinalIgnoreCase))
        {
            await page.ClickAsync("button.role-context-card[name='Input.Role'][value='ChuTro']");
            await page.WaitForURLAsync(
                url => !url.Contains("/select-role", StringComparison.OrdinalIgnoreCase),
                new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 20_000 });
        }

        // Vào một trang ổn định để circuit Blazor Server kết nối xong trước khi gõ tìm kiếm.
        await page.GotoAsync($"{BaseUrl}/rooms");
        await page.Locator(InputSelector).WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 20_000 });
        await page.WaitForTimeoutAsync(1_000);
        return page;
    }

    private static async Task TypeAndWaitAsync(IPage page, string query)
    {
        await page.FillAsync(InputSelector, query);
        // Dropdown chờ 250ms debounce rồi query DB; đợi tới khi có option thật.
        await page.Locator(OptionSelector).First.WaitForAsync(
            new() { State = WaitForSelectorState.Visible, Timeout = 20_000 });
    }

    [E2EFact]
    public async Task Go_khong_dau_mo_dropdown_theo_nhom_va_di_chuyen_bang_ban_phim()
    {
        var page = await LoginAsync();
        await TypeAndWaitAsync(page, "phong");

        Assert.Equal("true", await page.Locator(InputSelector).GetAttributeAsync("aria-expanded"));
        Assert.True(await page.Locator("#global-search-results .global-search-group-title").CountAsync() >= 1);

        var before = page.Url;
        await page.Locator(InputSelector).PressAsync("ArrowDown");
        await page.WaitForFunctionAsync(
            "() => document.querySelector('.topbar-search-input')?.getAttribute('aria-activedescendant') === 'search-option-0'",
            null, new() { Timeout = 10_000 });

        await page.Locator(InputSelector).PressAsync("Enter");
        await page.WaitForFunctionAsync(
            "before => location.href !== before", before, new() { Timeout = 15_000 });
        Assert.NotEqual(before, page.Url);
    }

    [E2EFact]
    public async Task Nut_xem_tat_ca_mo_trang_tim_kiem_day_du()
    {
        var page = await LoginAsync();
        await TypeAndWaitAsync(page, "phong");

        await page.ClickAsync(".global-search-viewall");
        await page.WaitForURLAsync(
            url => url.Contains("/search", StringComparison.OrdinalIgnoreCase),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 20_000 });

        // Chip nhóm do GlobalSearchService.GetGroupsAsync dựng; kết quả nhóm do provider trả.
        await page.Locator(".search-chip").First.WaitForAsync(new() { Timeout = 20_000 });
        Assert.True(await page.Locator(".search-chip").CountAsync() >= 2);
        await page.Locator(".search-result-group").First.WaitForAsync(
            new() { State = WaitForSelectorState.Visible, Timeout = 20_000 });

        // Đổi bộ lọc nhóm trên cùng trang: chỉ còn đúng nhóm Phòng, không giữ kết quả cũ.
        await page.ClickAsync(".search-chip[href*='group=rooms']");
        await page.WaitForURLAsync(
            url => url.Contains("group=rooms", StringComparison.Ordinal),
            new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 20_000 });
        await page.WaitForFunctionAsync(
            "() => document.querySelectorAll('.search-result-group').length === 1",
            null, new() { Timeout = 20_000 });
    }

    [E2EFact]
    public async Task O_tim_rong_khi_focus_khong_mo_dropdown()
    {
        var page = await LoginAsync();
        await page.Locator(InputSelector).ClickAsync();
        await page.WaitForTimeoutAsync(500);

        Assert.Equal("false", await page.Locator(InputSelector).GetAttributeAsync("aria-expanded"));
        Assert.Equal(0, await page.Locator("#global-search-results:visible").CountAsync());
    }
}
