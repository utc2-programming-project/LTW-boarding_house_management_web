using Microsoft.Playwright;
using NhaTro.E2ETests.Rooms;

namespace NhaTro.E2ETests.Features.Vehicles;

/// <summary>
/// Smoke E2E cho module xe (spec §13.2): Chủ trọ đăng nhập và mở được các trang
/// /vehicles, /vehicles/guests, /vehicles/zones, /vehicles/settings, /vehicles/audit
/// mà không bị đá về trang đăng nhập hay hiển thị khối lỗi.
/// Cần app đang chạy (Development) + tài khoản mẫu:
///   E2E_BASE_URL, E2E_PASSWORD (bắt buộc), E2E_OWNER_EMAIL (mặc định chutro@nhatro.local).
/// </summary>
public sealed class VehiclesUiSmokeTests : IAsyncLifetime
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL")?.TrimEnd('/');
    private static readonly string? Password = Environment.GetEnvironmentVariable("E2E_PASSWORD");
    private static readonly string OwnerEmail =
        Environment.GetEnvironmentVariable("E2E_OWNER_EMAIL") ?? "chutro@nhatro.local";

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage _page = null!;

    public async Task InitializeAsync()
    {
        if (!E2EFactAttribute.Configured) return;

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
        _page = await (await _browser.NewContextAsync()).NewPageAsync();

        await _page.GotoAsync($"{BaseUrl}/login");
        await _page.FillAsync("#login-email", OwnerEmail);
        await _page.FillAsync("#login-password", Password!);
        await _page.ClickAsync("button[type=submit]");
        await _page.WaitForURLAsync(url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase));
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.CloseAsync();
        _playwright?.Dispose();
    }

    [E2EFact]
    public async Task ChuTro_MoDuocTrangQuanLyXe()
    {
        await _page.GotoAsync($"{BaseUrl}/vehicles");

        await Assertions.Expect(_page.Locator(".vehicle-page")).ToBeVisibleAsync();
        await Assertions.Expect(_page.Locator(".vehicle-alert.error")).ToHaveCountAsync(0);
        // Có nhà trọ trong dữ liệu mẫu: KPI + danh sách hiển thị, tab điều hướng đầy đủ.
        await Assertions.Expect(_page.Locator(".stat-grid")).ToBeVisibleAsync();
        await Assertions.Expect(_page.GetByRole(AriaRole.Link).First).ToBeVisibleAsync();
    }

    [E2EFact]
    public async Task ChuTro_MoDuocCacTabConCuaModuleXe()
    {
        foreach (var path in new[] { "/vehicles/guests", "/vehicles/zones", "/vehicles/settings", "/vehicles/audit" })
        {
            await _page.GotoAsync($"{BaseUrl}{path}");
            await Assertions.Expect(_page.Locator(".vehicle-page")).ToBeVisibleAsync();
            await Assertions.Expect(_page).ToHaveURLAsync($"{BaseUrl}{path}");
        }
    }
}