namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Yêu cầu lập hóa đơn từ nguồn chi phí đã chốt (VHT-51).
/// Service chỉ nhận số tiền nguồn, tự làm tròn từng dòng và cộng tổng theo
/// <c>InvoiceCalculationRules</c>; client không được gửi tổng đã tính sẵn.
/// </summary>
public sealed class CreateInvoiceRequest
{
    /// <summary>Hợp đồng nguồn của hóa đơn.</summary>
    public required Guid ContractId { get; init; }

    /// <summary>Ngày đầu kỳ (bao gồm), do P5 chốt.</summary>
    public required DateOnly PeriodStart { get; init; }

    /// <summary>Ngày cuối kỳ (bao gồm), do P5 chốt.</summary>
    public required DateOnly PeriodEnd { get; init; }

    /// <summary>Hạn thanh toán.</summary>
    public DateOnly? DueDate { get; init; }

    /// <summary>
    /// Hóa đơn tạo từ yêu cầu đăng ký dịch vụ (nếu có). Khi có, hóa đơn được phép
    /// cùng kỳ với hóa đơn theo tháng/P5 (quy tắc 1 hóa đơn/kỳ không áp dụng cho
    /// hóa đơn dịch vụ theo yêu cầu).
    /// </summary>
    public int? RegistrationId { get; init; }

    /// <summary>
    /// Các dòng tiền thuê/dịch vụ/giảm giá đã chốt từ nguồn thật.
    /// Dòng giảm giá nhập số dương; rule đổi dấu âm khi lưu.
    /// </summary>
    public required IReadOnlyList<InvoiceItemInputDto> Items { get; init; }
}
