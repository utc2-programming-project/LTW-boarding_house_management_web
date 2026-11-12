using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Web.Components.Layout;
using Web.Features.Accounts;
using Web.Features.Invoices.Services;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

/// <summary>
/// PLAN-028: Chủ trọ chưa nhập tài khoản nhận tiền thì banner nhắc hiện trên mọi trang,
/// các vai trò khác không thấy, và banner tự ẩn ngay sau khi lưu ở /profile.
/// </summary>
public sealed class OwnerBankAccountBannerTests : BunitContext
{
    private static readonly OwnerBankAccount Configured =
        new("VCB", "Vietcombank", "0123456789", "NGUYEN VAN A");

    public OwnerBankAccountBannerTests()
    {
        Services.AddLogging();
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
    }

    [Fact]
    public void Owner_chua_nhap_tai_khoan_thi_thay_canh_bao_va_loi_tat_toi_profile()
    {
        var component = RenderBanner(Roles.ChuTro, account: null);

        Assert.Single(component.FindAll(".owner-bank-banner"));
        var action = Assert.Single(component.FindAll(".owner-bank-banner-action"));
        Assert.Equal("/profile", action.GetAttribute("href"));
        Assert.Equal("status", component.Find(".owner-bank-banner").GetAttribute("role"));
    }

    [Fact]
    public void Owner_da_nhap_tai_khoan_thi_khong_hien_banner()
    {
        var component = RenderBanner(Roles.ChuTro, Configured);

        Assert.Empty(component.FindAll(".owner-bank-banner"));
    }

    [Theory]
    [InlineData(Roles.QuanLy)]
    [InlineData(Roles.KhachThue)]
    [InlineData(Roles.Admin)]
    public void Vai_tro_khac_khong_thay_banner(string role)
    {
        var component = RenderBanner(role, account: null);

        Assert.Empty(component.FindAll(".owner-bank-banner"));
    }

    [Fact]
    public void Banner_tu_an_khi_luu_tai_khoan_o_trang_ho_so()
    {
        var accounts = new MutableBankAccounts(account: null);
        var component = RenderBanner(Roles.ChuTro, accounts);
        Assert.Single(component.FindAll(".owner-bank-banner"));

        accounts.Account = Configured;
        Services.GetRequiredService<OwnerBankAccountNotifier>().NotifyChanged();

        component.WaitForAssertion(() => Assert.Empty(component.FindAll(".owner-bank-banner")));
    }

    private IRenderedComponent<OwnerBankAccountBanner> RenderBanner(string role, OwnerBankAccount? account) =>
        RenderBanner(role, new MutableBankAccounts(account));

    private IRenderedComponent<OwnerBankAccountBanner> RenderBanner(string role, IOwnerBankAccountService accounts)
    {
        Services.AddSingleton<AuthenticationStateProvider>(new StubAuthenticationStateProvider(role));
        Services.AddSingleton(accounts);
        Services.AddSingleton<OwnerBankAccountNotifier>();
        return Render<OwnerBankAccountBanner>();
    }

    private sealed class MutableBankAccounts(OwnerBankAccount? account) : IOwnerBankAccountService
    {
        public OwnerBankAccount? Account { get; set; } = account;

        public Task<OwnerBankAccount?> GetForUserAsync(string userId, CancellationToken ct = default) =>
            Task.FromResult(Account);

        public Task<OwnerBankAccount?> GetForContractAsync(Guid contractId, CancellationToken ct = default) =>
            Task.FromResult(Account);

        public Task<OwnerBankAccount?> GetForInvoiceAsync(int invoiceId, CancellationToken ct = default) =>
            Task.FromResult(Account);
    }

    private sealed class StubAuthenticationStateProvider(string role) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Role, role)], "test");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    /// <summary>Trả lại chính key làm văn bản, đủ để kiểm tra cấu trúc UI.</summary>
    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, name);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
