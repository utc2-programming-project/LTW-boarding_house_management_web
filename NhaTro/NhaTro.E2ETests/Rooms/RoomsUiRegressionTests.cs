using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace NhaTro.E2ETests.Rooms;

/// <summary>
/// Hồi quy UI nhánh 16 trên trình duyệt thật (B16-01, B16-02, B16-04).
/// Cần app đang chạy (Development) và đã gieo tài khoản mẫu. Thiếu biến môi trường thì Skip.
///   E2E_BASE_URL  ví dụ http://localhost:5177
///   E2E_PASSWORD  trùng DevAccounts:Password
///   E2E_OWNER_EMAIL (tùy chọn, mặc định chutro@nhatro.local)
/// Lần đầu cần cài trình duyệt: pwsh NhaTro.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
/// Test không phụ thuộc dữ liệu có sẵn (không cần phòng/nhà trọ nào).
/// </summary>
public sealed class RoomsUiRegressionTests : IAsyncLifetime
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("E2E_BASE_URL")?.TrimEnd('/');
    private static readonly string? Password = Environment.GetEnvironmentVariable("E2E_PASSWORD");
    private static readonly string OwnerEmail =
        Environment.GetEnvironmentVariable("E2E_OWNER_EMAIL") ?? "chutro@nhatro.local";

    // Ngôn ngữ mặc định là vi-VN; chấp nhận cả EN để không phụ thuộc cookie ngôn ngữ.
    private static readonly Regex AddRoomText = new("Thêm phòng|Add room");

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

    // Blazor Server chỉ nhận click sau khi circuit kết nối; thử lại đến khi dialog thật sự mở.
    private async Task OpenDialogAsync(ILocator trigger)
    {
        var dialog = _page.Locator("dialog[open]");
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await trigger.ClickAsync();
            try
            {
                await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 1500 });
                return;
            }
            catch (PlaywrightException)
            {
                // circuit chưa sẵn sàng, thử lại
            }
        }

        await Assertions.Expect(dialog).ToBeVisibleAsync();
    }

    // B16-01: số hợp lệ không chia hết 1.000 không bị validation gốc của trình duyệt chặn.
    [E2EFact]
    public async Task B16_01_o_loc_gia_nhan_so_khong_chia_het_1000()
    {
        await _page.GotoAsync($"{BaseUrl}/rooms");

        foreach (var id in new[] { "#room-min-price", "#room-max-price" })
        {
            var input = _page.Locator(id);
            await input.FillAsync("2500500");
            Assert.True(
                await input.EvaluateAsync<bool>("el => el.validity.valid"),
                $"{id} vẫn bị validation gốc chặn (stepMismatch).");
        }
    }

    // B16-02: lọc không ra kết quả thì hiện "Xóa bộ lọc", không hiện nút Thêm phòng.
    [E2EFact]
    public async Task B16_02_loc_khong_co_ket_qua_khong_hien_nut_them()
    {
        await _page.GotoAsync($"{BaseUrl}/rooms");

        var emptyState = _page.Locator(".empty-state");
        for (var attempt = 0; attempt < 8; attempt++)
        {
            await _page.FillAsync("#room-keyword", $"ZZZ-{Guid.NewGuid():N}"[..12]);
            await _page.PressAsync("#room-keyword", "Enter");
            try
            {
                await Assertions.Expect(emptyState).ToBeVisibleAsync(new() { Timeout = 2000 });
                break;
            }
            catch (PlaywrightException)
            {
                // circuit chưa sẵn sàng, thử lại
            }
        }

        await Assertions.Expect(emptyState).ToBeVisibleAsync();
        await Assertions.Expect(emptyState.GetByRole(AriaRole.Button, new() { NameRegex = AddRoomText }))
            .ToHaveCountAsync(0);
        await Assertions.Expect(emptyState.GetByRole(AriaRole.Button)).ToHaveCountAsync(1);
    }

    // B16-04: đóng dialog bằng Escape hoặc nút Close thì focus quay về nút đã mở.
    // Dùng dialog "Thêm nhà trọ" vì không cần dữ liệu; cùng FeatureDialog với dialog phòng.
    [E2EFact]
    public async Task B16_04_dong_dialog_tra_focus_ve_nut_mo()
    {
        await _page.GotoAsync($"{BaseUrl}/properties");
        var trigger = _page.Locator(".p2-heading button.btn-primary");
        var dialog = _page.Locator("dialog[open]");

        await OpenDialogAsync(trigger);
        await _page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(trigger).ToBeFocusedAsync();

        await OpenDialogAsync(trigger);
        await dialog.Locator(".feature-dialog-header button").ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(trigger).ToBeFocusedAsync();
    }
}

/// <summary>[Fact] chỉ chạy khi đã cấu hình E2E_BASE_URL và E2E_PASSWORD.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class E2EFactAttribute : FactAttribute
{
    public static bool Configured { get; } =
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_BASE_URL"))
        && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("E2E_PASSWORD"));

    public E2EFactAttribute()
    {
        if (!Configured)
        {
            Skip = "Chưa đặt E2E_BASE_URL và E2E_PASSWORD (bỏ qua test E2E).";
        }
    }
}
