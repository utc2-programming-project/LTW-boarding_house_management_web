using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.Rules;

/// <summary>
/// Quy tắc trạng thái và tiền đã thu của P6. Hàm thuần, không DB/HTTP.
/// D13: không trả một phần; có từ chối minh chứng để khách gửi lại; không hủy hóa đơn trong v1.0.
/// Gửi yêu cầu thanh toán (Pending) không làm thay đổi tiền đã thu.
/// </summary>
public static class InvoicePaymentRules
{
    /// <summary>Chỉ hóa đơn nháp mới được sửa dòng.</summary>
    public static bool CanEdit(InvoiceStatus status) => status == InvoiceStatus.Draft;

    /// <summary>Chỉ hóa đơn nháp mới được phát hành; hóa đơn đã phát hành không sửa lại.</summary>
    public static bool CanIssue(InvoiceStatus status) => status == InvoiceStatus.Draft;

    /// <summary>
    /// Khách chỉ gửi yêu cầu cho hóa đơn đã phát hành và số tiền phải bằng đúng tổng hóa đơn.
    /// Trả ít hơn hoặc nhiều hơn đều bị chặn (D13).
    /// </summary>
    public static bool CanSubmitPayment(InvoiceStatus status, decimal invoiceTotal, decimal requestedAmount) =>
        status == InvoiceStatus.Issued
        && invoiceTotal > 0
        && requestedAmount == invoiceTotal;

    /// <summary>Chỉ xác nhận yêu cầu đang Pending, đúng số tiền và hóa đơn còn ở trạng thái đã phát hành.</summary>
    public static bool CanConfirmPayment(InvoiceStatus invoiceStatus, decimal invoiceTotal, PaymentDto payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        return invoiceStatus == InvoiceStatus.Issued
            && invoiceTotal > 0
            && payment.Status == PaymentStatus.Pending
            && payment.Amount == invoiceTotal;
    }

    /// <summary>Chỉ từ chối được yêu cầu đang Pending; từ chối không trả lại tiền vì chưa xác nhận thu.</summary>
    public static bool CanRejectPayment(PaymentStatus status) => status == PaymentStatus.Pending;

    /// <summary>
    /// Tiền đã thu = tổng các Payment Confirmed. Pending (đã gửi yêu cầu) và Rejected không được cộng.
    /// </summary>
    public static decimal ConfirmedPaidAmount(IEnumerable<PaymentDto> payments)
    {
        ArgumentNullException.ThrowIfNull(payments);

        var paid = 0m;
        foreach (var payment in payments)
        {
            if (payment.Status == PaymentStatus.Confirmed)
            {
                paid += payment.Amount;
            }
        }

        return paid;
    }

    /// <summary>Công nợ còn lại của hóa đơn; không trả số âm.</summary>
    public static decimal OutstandingAmount(decimal invoiceTotal, IEnumerable<PaymentDto> payments) =>
        Math.Max(0m, invoiceTotal - ConfirmedPaidAmount(payments));

    /// <summary>Hóa đơn đã thu đủ tiền hay chưa; chỉ tính Payment Confirmed.</summary>
    public static bool IsFullyPaid(decimal invoiceTotal, IEnumerable<PaymentDto> payments) =>
        ConfirmedPaidAmount(payments) >= invoiceTotal;
}
