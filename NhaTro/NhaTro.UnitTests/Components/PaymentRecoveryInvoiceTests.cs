using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NhaTro.UnitTests.TestSupport;
using Web.Features.Accounts;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Pages;
using Web.Features.Invoices.Services;
using Web.Features.Properties.Services;

namespace NhaTro.UnitTests.Components;

/// <summary>
/// VHT-143: trang hóa đơn của khách thuê sau reconnect/tải lại chỉ được tra kết quả đọc,
/// không tự gửi lại yêu cầu thanh toán và giữ khóa khi kết quả trước chưa rõ.
/// </summary>
public sealed class PaymentRecoveryInvoiceTests : P6RecoveryBunitContext
{
    private const string Target = "invoice:1";
    private readonly P6RecoveryBackend _backend = new();
    private readonly BunitJSModuleInterop _module;

    public PaymentRecoveryInvoiceTests()
    {
        var auth = new P6AuthProvider(P6RecoveryData.UserId, Roles.KhachThue);
        _module = P6RecoveryBackend.Register(
            this,
            auth,
            _backend,
            bankAccount: new OwnerBankAccount(
                "VCB", "Vietcombank", "0011002233", "Nguyen Van A"));

        Services.AddSingleton(auth);
        Services.AddSingleton(_backend.CreateInvoiceQuery());
        Services.AddSingleton(_backend.CreateInvoiceService());
        Services.AddSingleton(_backend.CreateNotificationService());
        Services.AddSingleton(new P2CallerProvider(auth));
    }

    private IRenderedComponent<InvoiceDetailView> RenderInvoice() =>
        Render<InvoiceDetailView>(p => p.Add(x => x.Id, 1));

    [Fact]
    public async Task Khong_co_marker_mo_form_sau_khi_san_sang()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(false);

        var cut = RenderInvoice();

        await WaitUntilAsync(() =>
        {
            var buttons = cut.FindAll(".payment-form button[type=submit]");
            return buttons.Count == 1 && !buttons[0].HasAttribute("disabled");
        });
        Assert.True(cut.Find(".payment-form button[type=submit]").HasAttribute("data-payment-write"));
        Assert.Empty(_backend.LatestCalls);
    }

    [Fact]
    public async Task Marker_hop_le_khoa_form_va_chi_tra_mot_lan()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        _backend.Latest = _ => Task.FromResult<PaymentDto?>(
            P6RecoveryData.Payment(5, 1, PaymentStatus.Pending));

        var cut = RenderInvoice();

        await WaitUntilAsync(() => _backend.LatestCalls.Count == 1);
        // Tra thấy Pending: hiển thị trạng thái chờ xác nhận và không còn form gửi.
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".payment-notice.warning")));
        Assert.Empty(cut.FindAll(".payment-form"));
        Assert.Empty(cut.FindAll(".payment-result-check"));
        Assert.Empty(_backend.SubmitCalls);
    }

    [Fact]
    public async Task Marker_cua_tai_khoan_khac_bi_bo_qua()
    {
        _module.Setup<bool>("has", Target, "user-khac").SetResult(true);

        var cut = RenderInvoice();

        await WaitUntilAsync(() =>
        {
            var buttons = cut.FindAll(".payment-form button[type=submit]");
            return buttons.Count == 1 && !buttons[0].HasAttribute("disabled");
        });
        Assert.Empty(_backend.LatestCalls);
    }

    [Fact]
    public async Task Marker_cua_hoa_don_khac_khong_khoa_trang_nay()
    {
        _module.Setup<bool>("has", "invoice:2", P6RecoveryData.UserId).SetResult(true);

        var cut = RenderInvoice();

        await WaitUntilAsync(() =>
        {
            var buttons = cut.FindAll(".payment-form button[type=submit]");
            return buttons.Count == 1 && !buttons[0].HasAttribute("disabled");
        });
        Assert.Empty(_backend.LatestCalls);
    }

    [Fact]
    public async Task Reconnect_chi_goi_truy_van_doc()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        _backend.Latest = _ => Task.FromResult<PaymentDto?>(
            P6RecoveryData.Payment(5, 1, PaymentStatus.Pending));
        var cut = RenderInvoice();
        await WaitUntilAsync(() => _backend.LatestCalls.Count == 1);

        await cut.InvokeAsync(() => cut.Instance.OnReconnectedAsync());

        await WaitUntilAsync(() => _backend.LatestCalls.Count == 2);
        Assert.Empty(_backend.SubmitCalls);
    }

    [Fact]
    public async Task Ket_qua_confirmed_hien_da_thanh_toan_va_khoa_form()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        _backend.Latest = _ => Task.FromResult<PaymentDto?>(
            P6RecoveryData.Payment(5, 1, PaymentStatus.Confirmed));

        var cut = RenderInvoice();

        await WaitUntilAsync(() => cut.FindAll(".payment-notice.success").Count == 1);
        Assert.Empty(cut.FindAll(".payment-form"));
        Assert.Empty(cut.FindAll(".payment-result-check"));
        Assert.Empty(_backend.SubmitCalls);
    }

    [Fact]
    public async Task Ket_qua_rejected_hien_ly_do_va_mo_form_lai()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        _backend.Latest = _ => Task.FromResult<PaymentDto?>(
            P6RecoveryData.Payment(5, 1, PaymentStatus.Rejected, "Sai số tiền"));

        var cut = RenderInvoice();

        await WaitUntilAsync(() => cut.FindAll(".payment-notice.danger").Count == 1);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".payment-form")));
        Assert.Empty(cut.FindAll(".payment-result-check"));
        Assert.Contains("Sai số tiền", cut.Markup);
    }

    [Fact]
    public async Task Khong_co_ban_ghi_mo_form_va_bao_chua_co_ket_qua()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);

        var cut = RenderInvoice();

        await WaitUntilAsync(() => cut.FindAll(".payment-notice.info").Count == 1);
        Assert.Single(cut.FindAll(".payment-form"));
        Assert.Empty(cut.FindAll(".payment-result-check"));
        Assert.Empty(_backend.SubmitCalls);
    }

    [Fact]
    public async Task Loi_tra_cuu_giu_khoa_va_cho_tra_lai()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        _backend.Latest = _ => Task.FromException<PaymentDto?>(new InvalidOperationException("network"));

        var cut = RenderInvoice();

        await WaitUntilAsync(() => _backend.LatestCalls.Count == 1);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".payment-result-check")));
        Assert.Empty(cut.FindAll(".payment-form"));
        Assert.Empty(_backend.SubmitCalls);
    }
}
