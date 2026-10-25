using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;

namespace NhaTro.UnitTests.Features.Invoices;

public class InvoicePaymentRulesTests
{
    private const decimal InvoiceTotal = 3_870_000m;

    [Fact]
    public void Gui_yeu_cau_thanh_toan_chua_duoc_tinh_la_tien_da_thu()
    {
        var pending = Payment(InvoiceTotal, PaymentStatus.Pending);

        Assert.Equal(0m, InvoicePaymentRules.ConfirmedPaidAmount([pending]));
        Assert.Equal(InvoiceTotal, InvoicePaymentRules.OutstandingAmount(InvoiceTotal, [pending]));
        Assert.False(InvoicePaymentRules.IsFullyPaid(InvoiceTotal, [pending]));
    }

    [Fact]
    public void Chi_payment_da_xac_nhan_moi_duoc_cong()
    {
        var pending = Payment(InvoiceTotal, PaymentStatus.Pending, id: 1);
        var rejected = Payment(InvoiceTotal, PaymentStatus.Rejected, id: 2);
        var confirmed = Payment(InvoiceTotal, PaymentStatus.Confirmed, id: 3);

        Assert.Equal(
            InvoiceTotal,
            InvoicePaymentRules.ConfirmedPaidAmount([pending, rejected, confirmed]));
        Assert.Equal(
            0m,
            InvoicePaymentRules.OutstandingAmount(InvoiceTotal, [pending, rejected, confirmed]));
        Assert.True(InvoicePaymentRules.IsFullyPaid(InvoiceTotal, [confirmed]));
    }

    [Fact]
    public void Tu_choi_minh_chung_khong_tinh_tien_va_cho_khach_gui_lai()
    {
        var rejected = Payment(InvoiceTotal, PaymentStatus.Rejected);

        Assert.Equal(0m, InvoicePaymentRules.ConfirmedPaidAmount([rejected]));
        Assert.True(InvoicePaymentRules.CanRejectPayment(PaymentStatus.Pending));
        Assert.False(InvoicePaymentRules.CanRejectPayment(PaymentStatus.Confirmed));
        Assert.True(InvoicePaymentRules.CanSubmitPayment(
            InvoiceStatus.Issued, InvoiceTotal, InvoiceTotal));
    }

    [Fact]
    public void Khong_cho_tra_mot_phan_hoac_tra_thua()
    {
        Assert.False(InvoicePaymentRules.CanSubmitPayment(
            InvoiceStatus.Issued, InvoiceTotal, InvoiceTotal - 1m));
        Assert.False(InvoicePaymentRules.CanSubmitPayment(
            InvoiceStatus.Issued, InvoiceTotal, InvoiceTotal + 1m));
        Assert.True(InvoicePaymentRules.CanSubmitPayment(
            InvoiceStatus.Issued, InvoiceTotal, InvoiceTotal));
        Assert.False(InvoicePaymentRules.CanSubmitPayment(
            InvoiceStatus.Draft, InvoiceTotal, InvoiceTotal));
        Assert.False(InvoicePaymentRules.CanSubmitPayment(
            InvoiceStatus.Paid, InvoiceTotal, InvoiceTotal));
    }

    [Fact]
    public void Chi_xac_nhan_duoc_yeu_cau_dang_pending_va_du_tien()
    {
        var pending = Payment(InvoiceTotal, PaymentStatus.Pending);
        var partial = Payment(InvoiceTotal - 1m, PaymentStatus.Pending);
        var confirmed = Payment(InvoiceTotal, PaymentStatus.Confirmed);

        Assert.True(InvoicePaymentRules.CanConfirmPayment(InvoiceStatus.Issued, InvoiceTotal, pending));
        Assert.False(InvoicePaymentRules.CanConfirmPayment(InvoiceStatus.Issued, InvoiceTotal, partial));
        Assert.False(InvoicePaymentRules.CanConfirmPayment(InvoiceStatus.Issued, InvoiceTotal, confirmed));
        Assert.False(InvoicePaymentRules.CanConfirmPayment(InvoiceStatus.Draft, InvoiceTotal, pending));
        Assert.False(InvoicePaymentRules.CanConfirmPayment(InvoiceStatus.Paid, InvoiceTotal, pending));
    }

    [Fact]
    public void Chi_sua_va_phat_hanh_duoc_hoa_don_nhap()
    {
        Assert.True(InvoicePaymentRules.CanEdit(InvoiceStatus.Draft));
        Assert.False(InvoicePaymentRules.CanEdit(InvoiceStatus.Issued));
        Assert.False(InvoicePaymentRules.CanEdit(InvoiceStatus.Paid));

        Assert.True(InvoicePaymentRules.CanIssue(InvoiceStatus.Draft));
        Assert.False(InvoicePaymentRules.CanIssue(InvoiceStatus.Issued));
        Assert.False(InvoicePaymentRules.CanIssue(InvoiceStatus.Paid));
    }

    [Fact]
    public void Hoa_don_khong_co_trang_thai_huy_trong_v1()
    {
        var statusNames = Enum.GetNames<InvoiceStatus>();

        Assert.DoesNotContain("Cancelled", statusNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Canceled", statusNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cong_no_khong_am_khi_da_thu_du()
    {
        var confirmed = Payment(InvoiceTotal, PaymentStatus.Confirmed);

        Assert.Equal(0m, InvoicePaymentRules.OutstandingAmount(InvoiceTotal, [confirmed]));
    }

    private static PaymentDto Payment(decimal amount, PaymentStatus status, int id = 1) =>
        new()
        {
            Id = id,
            InvoiceId = 10,
            Amount = amount,
            TransferredAt = new DateTimeOffset(2026, 10, 20, 8, 30, 0, TimeSpan.Zero),
            TransactionCode = "FT261020123456",
            Status = status,
            ProofFileIds = [Guid.NewGuid()],
            RequestedAt = new DateTimeOffset(2026, 10, 20, 8, 35, 0, TimeSpan.Zero),
            ReviewedAt = status == PaymentStatus.Pending
                ? null
                : new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero),
            RejectionReason = status == PaymentStatus.Rejected ? "Số tiền không khớp" : null,
        };
}
