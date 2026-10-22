using Microsoft.Playwright;

namespace NhaTro.E2ETests.TestSupport;

/// <summary>
/// VHT-14 (đợt 1A): fixture Playwright cho auth/UI thật.
/// - App phải đang chạy (mặc định http://localhost:5177, đổi bằng NHATRO_E2E_BASE_URL).
/// - Nếu chưa cài browser Playwright hoặc app chưa chạy, test tự bỏ qua để CI không đỏ oan.
/// Cài browser một lần: pwsh NhaTro/NhaTro.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
/// </summary>
public sealed class PlaywrightFixture : IAsyncLifetime
{
    public string BaseUrl { get; } =
        Environment.GetEnvironmentVariable("NHATRO_E2E_BASE_URL") ?? "http://localhost:5177";

    public IPlaywright? Playwright { get; private set; }

    public IBrowser? Browser { get; private set; }

    public bool Available { get; private set; }

    public string? SkipReason { get; private set; }

    public async Task InitializeAsync()
    {
        if (!await ServerIsRunningAsync())
        {
            SkipReason = $"Không thấy app tại {BaseUrl}. Chạy app rồi chạy lại E2E.";
            return;
        }

        try
        {
            Playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            Browser = await Playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
            });
            Available = true;
        }
        catch (Exception ex)
        {
            SkipReason = "Không khởi động được Chromium Playwright: " + ex.Message;
            Playwright?.Dispose();
            Playwright = null;
            Browser = null;
        }
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.CloseAsync();
        }

        Playwright?.Dispose();
    }

    private async Task<bool> ServerIsRunningAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var response = await client.GetAsync(BaseUrl);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
