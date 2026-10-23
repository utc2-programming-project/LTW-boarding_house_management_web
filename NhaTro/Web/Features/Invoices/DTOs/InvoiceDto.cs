using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Hóa đơn trả cho màn hình P6. Kỳ là khoảng ngày do P5 chốt
/// (<see cref="PeriodStart"/>–<see cref="PeriodEnd"/>, cùng tính cả hai đầu).
/// </summary>
public sealed class InvoiceDto
{
    public required int Id { get; init; }

    public required int ContractId { get; init; }

    /// <summary>Ngày đầu kỳ (bao gồm), ngày thuần không cộng trừ múi giờ.</summary>
    public required DateOnly PeriodStart { get; init; }

    /// <summary>Ngày cuối kỳ (bao gồm), ngày thuần không cộng trừ múi giờ.</summary>
    public required DateOnly PeriodEnd { get; init; }

    public required InvoiceStatus Status { get; init; }

    public required IReadOnlyList<InvoiceItemDto> Items { get; init; }

    /// <summary>Tổng hóa đơn = cộng thành tiền đã làm tròn của từng dòng.</summary>
    public required decimal TotalAmount { get; init; }

    /// <summary>Chỉ cộng các Payment đã Confirmed; Pending/Rejected không được tính.</summary>
    public required decimal PaidAmount { get; init; }

    /// <summary>Tổng hóa đơn trừ tiền đã xác nhận, không âm.</summary>
    public required decimal OutstandingAmount { get; init; }

    /// <summary>Yêu cầu thanh toán mới nhất để hiển thị trạng thái chờ/đã từ chối; null nếu chưa gửi.</summary>
    public PaymentDto? LatestPayment { get; init; }
}
