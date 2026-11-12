using System.ComponentModel.DataAnnotations;

namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Yêu cầu Chủ trọ chỉnh sửa hóa đơn đã phát hành (khách chưa gửi yêu cầu thanh toán).
/// Lý do là bắt buộc và sẽ hiển thị cho khách thuê.
/// </summary>
public sealed class ReviseInvoiceRequest
{
    /// <summary>Lý do chỉnh sửa (bắt buộc), tối đa 1000 ký tự.</summary>
    [Required(ErrorMessage = "Vui lòng nhập lý do chỉnh sửa hóa đơn.")]
    [MaxLength(1000, ErrorMessage = "Lý do chỉnh sửa hóa đơn tối đa 1000 ký tự.")]
    public required string Reason { get; init; }

    /// <summary>Các dòng tiền mới của hóa đơn (server tính lại thành tiền và tổng).</summary>
    public required IReadOnlyList<InvoiceItemInputDto> Items { get; init; }
}
