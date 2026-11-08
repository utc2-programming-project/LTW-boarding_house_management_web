using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.Entities;

/// <summary>
/// Nhật ký nghiệp vụ P6 cho từng lần gửi/xác nhận/từ chối thanh toán.
/// Chỉ chứa metadata thao tác, không chứa tên/đường dẫn/nội dung minh chứng.
/// Được ghi cùng transaction với thay đổi <see cref="Payment"/> và <see cref="Invoice"/>.
/// </summary>
public sealed class PaymentAuditLog
{
    public long Id { get; set; }

    public int PaymentId { get; set; }

    public int InvoiceId { get; set; }

    public PaymentAuditAction Action { get; set; }

    public PaymentStatus? FromStatus { get; set; }

    public PaymentStatus ToStatus { get; set; }

    public decimal Amount { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public string ActorRole { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Ghi chú/lý do từ chối; không chứa nội dung tệp.</summary>
    public string? Note { get; set; }

    public Payment Payment { get; set; } = null!;
}
