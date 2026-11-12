using Bunit;
using NhaTro.UnitTests.TestSupport;
using Web.Features.Accounts;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Pages;

namespace NhaTro.UnitTests.Components;

/// <summary>
/// VHT-143: trang xác nhận/từ chối P6 sau reconnect/tải lại chỉ được tra kết quả đọc,
/// giữ khóa khi thao tác trước chưa rõ và không tự gọi lại lệnh ghi.
/// </summary>
public sealed class PaymentRecoveryReviewTests : P6RecoveryBunitContext
{
    private const string Target = "payment:1";
    private readonly P6RecoveryBackend _backend = new();
    private readonly BunitJSModuleInterop _module;

    public PaymentRecoveryReviewTests()
    {
        _module = P6RecoveryBackend.Register(this, new P6AuthProvider(P6RecoveryData.UserId, Roles.ChuTro), _backend);
    }

    private IRenderedComponent<PaymentReviewDetailView> RenderReview(int paymentId = 1) =>
        Render<PaymentReviewDetailView>(p => p.Add(x => x.PaymentId, paymentId));

    [Fact]
    public async Task Marker_hop_le_khoa_thao_tac_va_chi_tra_mot_lan()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);

        var cut = RenderReview();

        await WaitUntilAsync(() => _backend.DetailCalls.Count == 2);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".review-notice.warning")));
        Assert.Single(cut.FindAll(".review-result-check button"));
        Assert.Empty(cut.FindAll(".action-bar"));
        Assert.Empty(_backend.ConfirmCalls);
        Assert.Empty(_backend.RejectCalls);
    }

    [Fact]
    public async Task Marker_cua_tai_khoan_khac_bi_bo_qua()
    {
        // JS chỉ trả true khi marker khớp đúng tài khoản hiện tại; đặt marker của user khác
        // để chắc trang không tự coi là còn thao tác chưa rõ.
        _module.Setup<bool>("has", Target, "user-khac").SetResult(true);

        var cut = RenderReview();

        await WaitUntilAsync(() => cut.FindAll(".action-bar").Count == 1);
        Assert.Single(_backend.DetailCalls);
        Assert.Empty(cut.FindAll(".review-notice.warning"));
    }

    [Fact]
    public async Task Marker_cua_yeu_cau_khac_khong_khoa_trang_moi()
    {
        _module.Setup<bool>("has", "payment:2", P6RecoveryData.UserId).SetResult(true);

        var cut = RenderReview();

        await WaitUntilAsync(() => cut.FindAll(".action-bar").Count == 1);
        Assert.Single(_backend.DetailCalls);
        Assert.Empty(cut.FindAll(".review-notice.warning"));
    }

    [Fact]
    public async Task Reconnect_chi_goi_truy_van_doc()
    {
        var cut = RenderReview();
        await WaitUntilAsync(() => _backend.DetailCalls.Count == 1);

        await cut.InvokeAsync(() => cut.Instance.OnReconnectedAsync());

        Assert.Single(_backend.DetailCalls);
        Assert.Empty(_backend.ConfirmCalls);
        Assert.Empty(_backend.RejectCalls);
    }

    [Fact]
    public async Task Ket_qua_van_pending_giu_khoa_va_cho_tra_lai()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);

        var cut = RenderReview();

        await WaitUntilAsync(() => _backend.DetailCalls.Count == 2);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".review-notice.warning")));
        Assert.Single(cut.FindAll(".review-result-check button"));
        Assert.Empty(cut.FindAll(".action-bar"));
    }

    [Fact]
    public async Task Ket_qua_confirmed_mo_khoa_va_khong_cho_xac_nhan_lai()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        _backend.Detail = id => Task.FromResult(P6RecoveryBackend.ReviewItem(
            P6RecoveryData.Payment(id, 1, _backend.DetailCalls.Count >= 2
                ? PaymentStatus.Confirmed
                : PaymentStatus.Pending)));

        var cut = RenderReview();

        await WaitUntilAsync(() => cut.FindAll(".review-notice.success").Count == 1);
        Assert.Empty(cut.FindAll(".review-notice.warning"));
        Assert.Empty(cut.FindAll(".action-bar"));
    }

    [Fact]
    public async Task Ket_qua_rejected_mo_khoa_va_hien_ly_do()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        _backend.Detail = id => Task.FromResult(P6RecoveryBackend.ReviewItem(
            P6RecoveryData.Payment(id, 1, _backend.DetailCalls.Count >= 2
                ? PaymentStatus.Rejected
                : PaymentStatus.Pending)));

        var cut = RenderReview();

        await WaitUntilAsync(() => cut.FindAll(".review-notice.danger").Count == 1);
        Assert.Empty(cut.FindAll(".review-notice.warning"));
    }

    [Fact]
    public async Task Loi_tra_cuu_giu_khoa_va_nut_tra_lai()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        _backend.Detail = id => _backend.DetailCalls.Count >= 2
            ? Task.FromException<PaymentReviewItemDto>(new InvalidOperationException("network"))
            : Task.FromResult(P6RecoveryBackend.ReviewItem(P6RecoveryData.Payment(id, 1, PaymentStatus.Pending)));

        var cut = RenderReview();

        await WaitUntilAsync(() => _backend.DetailCalls.Count == 2);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".review-notice.warning")));
        Assert.Single(cut.FindAll(".review-result-check button"));
        Assert.Empty(cut.FindAll(".action-bar"));
    }

    [Fact]
    public async Task Doi_yeu_cau_khong_dung_lai_ket_qua_cu()
    {
        _module.Setup<bool>("has", Target, P6RecoveryData.UserId).SetResult(true);
        var cut = RenderReview();
        await WaitUntilAsync(() => _backend.DetailCalls.Count == 2);

        cut.Render(p => p.Add(x => x.PaymentId, 2));

        await WaitUntilAsync(() => _backend.DetailCalls.Contains(2));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".review-notice.warning")));
        Assert.Single(cut.FindAll(".action-bar"));
        var callsForSecond = _backend.DetailCalls.Count(id => id == 2);

        await cut.InvokeAsync(() => cut.Instance.OnReconnectedAsync());

        Assert.Equal(callsForSecond, _backend.DetailCalls.Count(id => id == 2));
        Assert.Empty(_backend.ConfirmCalls);
        Assert.Empty(_backend.RejectCalls);
    }
}
