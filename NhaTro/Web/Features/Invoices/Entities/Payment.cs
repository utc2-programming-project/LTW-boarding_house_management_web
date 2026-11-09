using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.Entities;

/// <summary>
/// Một lần khách thuê gửi yêu cầu thanh toán kèm minh chứng.
/// <see cref="PaymentStatus.Pending"/> chỉ là yêu cầu chờ đối chiếu, không phải tiền đã thu.
/// D13: không trả một phần; khách gửi lại sau khi minh chứng bị từ chối.
/// </summary>
public sealed class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    /// <summary>Số tiền khách báo đã chuyển, VND; phải bằng đúng tổng hóa đơn.</summary>
    public decimal Amount { get; set; }

    /// <summary>Thời điểm khách báo đã chuyển tiền, lưu UTC.</summary>
    public DateTime TransferredAt { get; set; }

    /// <summary>Mã giao dịch ngân hàng nếu khách có nhập (D17 còn để ngỏ nên tùy chọn).</summary>
    public string? TransactionCode { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Thời điểm hệ thống nhận yêu cầu, lưu UTC.</summary>
    public DateTime RequestedAt { get; set; }

    public string RequestedByUserId { get; set; } = string.Empty;

    /// <summary>Thời điểm xác nhận hoặc từ chối; null khi còn Pending.</summary>
    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    /// <summary>Lý do từ chối minh chứng; có khi <see cref="Status"/> = Rejected.</summary>
    public string? RejectionReason { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Invoice Invoice { get; set; } = null!;

    public ICollection<PaymentProof> Proofs { get; set; } = new List<PaymentProof>();
}
