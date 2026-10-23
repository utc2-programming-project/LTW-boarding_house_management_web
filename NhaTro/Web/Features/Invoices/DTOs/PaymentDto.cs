using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Một lần khách thuê gửi thông tin thanh toán và kết quả đối chiếu.
/// <see cref="Status"/> = Pending/Rejected không được cộng vào tiền đã thu; chỉ Confirmed mới được tính.
/// </summary>
public sealed class PaymentDto
{
    public required int Id { get; init; }

    public required int InvoiceId { get; init; }

    /// <summary>Số tiền khách báo đã chuyển, VND. D13: phải bằng đúng tổng hóa đơn, không trả một phần.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Thời điểm khách báo đã chuyển tiền, lưu UTC.</summary>
    public required DateTimeOffset TransferredAt { get; init; }

    /// <summary>Mã giao dịch ngân hàng nếu khách có nhập; D17 (còn `❓`) tạm để tùy chọn.</summary>
    public string? TransactionCode { get; init; }

    public required PaymentStatus Status { get; init; }

    /// <summary>ID các tệp minh chứng (ảnh/PDF) đã upload qua storage; tối thiểu một tệp khi gửi yêu cầu.</summary>
    public required IReadOnlyList<Guid> ProofFileIds { get; init; }

    /// <summary>Thời điểm hệ thống nhận yêu cầu, lưu UTC.</summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>Thời điểm xác nhận hoặc từ chối; null khi còn Pending.</summary>
    public DateTimeOffset? ReviewedAt { get; init; }

    /// <summary>Lý do từ chối minh chứng; có khi Status = Rejected.</summary>
    public string? RejectionReason { get; init; }
}
