using System.ComponentModel.DataAnnotations;

namespace Web.Features.Invoices.DTOs;

/// <summary>Quản lý/Chủ trọ từ chối minh chứng của một yêu cầu đang chờ.</summary>
public sealed class RejectPaymentRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do từ chối.")]
    [MaxLength(500, ErrorMessage = "Lý do từ chối tối đa 500 ký tự.")]
    public required string Reason { get; init; }
}
