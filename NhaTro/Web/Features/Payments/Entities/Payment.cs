using Web.Features.Invoices.Enums;

namespace Web.Features.Payments.Entities;

/// <summary>
/// P6 — yêu cầu thanh toán do Khách thuê gửi kèm minh chứng. Chỉ dòng Confirmed mới
/// được cộng vào tiền đã thu; Pending/Rejected không làm thay đổi công nợ.
/// </summary>
public sealed class Payment
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset TransferredAt { get; set; }

    public string? TransactionCode { get; set; }

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    /// <summary>Danh sách ID tệp minh chứng, lưu JSON để không phụ thuộc storage khi test.</summary>
    public string ProofFileIdsJson { get; set; } = "[]";

    public DateTimeOffset RequestedAt { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    public string SubmittedByUserId { get; set; } = string.Empty;

    public byte[] RowVersion { get; set; } = [];
}
