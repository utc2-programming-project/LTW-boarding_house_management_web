using System.ComponentModel.DataAnnotations;

namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Khách thuê gửi yêu cầu thanh toán cho một hóa đơn.
/// D17 còn `❓`; DTO tạm theo đề xuất: bắt buộc số tiền và thời điểm chuyển, mã giao dịch tùy chọn,
/// minh chứng là ảnh/PDF theo giới hạn tệp chung. Việc kiểm định dạng/dung lượng tệp do storage thực hiện.
/// </summary>
public sealed class SubmitPaymentRequest
{
    [Range(typeof(decimal), "0.01", "79228162514264337593543950335",
        ErrorMessage = "Số tiền thanh toán phải lớn hơn 0.")]
    public required decimal Amount { get; init; }

    /// <summary>Thời điểm khách báo đã chuyển tiền; service kiểm không ở tương lai và lưu UTC.</summary>
    public required DateTimeOffset TransferredAt { get; init; }

    [MaxLength(100, ErrorMessage = "Mã giao dịch tối đa 100 ký tự.")]
    public string? TransactionCode { get; init; }

    /// <summary>ID tệp minh chứng đã upload; tối thiểu một tệp.</summary>
    [Required(ErrorMessage = "Vui lòng gửi minh chứng thanh toán.")]
    [MinLength(1, ErrorMessage = "Vui lòng gửi ít nhất một minh chứng thanh toán.")]
    public required List<Guid> ProofFileIds { get; init; }
}
