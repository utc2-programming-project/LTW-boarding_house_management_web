using Microsoft.Playwright;
using NhaTro.E2ETests.TestSupport;

namespace NhaTro.E2ETests.Features.Auth;

/// <summary>
/// VHT-14 (đợt 1D): smoke UI đăng nhập bằng Playwright trên app thật.
/// Test tự bỏ qua nếu app chưa chạy hoặc chưa cài browser Playwright.
/// Đăng nhập thật cần thêm biến môi trường NHATRO_E2E_EMAIL / NHATRO_E2E_PASSWORD.
/// </summary>
public sealed class LoginSmokeTests(PlaywrightFixture fixture) : IClassFixture<PlaywrightFixture>
{
    [Fact]
    public async Task Trang_dang_nhap_hien_thi_form()
    {
        if (!fixture.Available)
        {
            return;
        }

        await using var context = await fixture.Browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        var response = await page.GotoAsync($"{fixture.BaseUrl}/Account/Login");

        Assert.NotNull(response);
        Assert.True(response!.Ok);
        Assert.Contains("/Account/Login", page.Url);

        var heading = await page.Locator("#login-title").TextContentAsync();
        Assert.True(
            heading?.Contains("Log in", StringComparison.OrdinalIgnoreCase) == true
            || heading?.Contains("Đăng nhập", StringComparison.OrdinalIgnoreCase) == true,
            $"Unexpected localized login heading: {heading}");

        await page.Locator("#login-email").WaitForAsync();
        await page.Locator("#login-password").WaitForAsync();
        Assert.True(await page.Locator("#login-email").IsVisibleAsync());
        Assert.True(await page.Locator("#login-password").IsVisibleAsync());
    }

    [Fact]
    public async Task Dang_nhap_that_neu_co_tai_khoan_env()
    {
        var email = Environment.GetEnvironmentVariable("NHATRO_E2E_EMAIL");
        var password = Environment.GetEnvironmentVariable("NHATRO_E2E_PASSWORD");
        if (!fixture.Available || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var context = await fixture.Browser!.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/Account/Login");
        await page.Locator("#login-email").FillAsync(email);
        await page.Locator("#login-password").FillAsync(password);
        var loginForm = page.Locator("#login-password").Locator("xpath=ancestor::form");
        await loginForm.Locator("button[type='submit']").ClickAsync();

        // AppTopbar chỉ có avatar sau khi cookie đăng nhập hợp lệ.
        await page.Locator("a.avatar").WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        Assert.DoesNotContain("/Account/Login", page.Url);
    }
}
