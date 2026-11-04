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

    /// <summary>
    /// Các dòng tiền thuê/dịch vụ/giảm giá đã chốt từ nguồn thật.
    /// Dòng giảm giá nhập số dương; rule đổi dấu âm khi lưu.
    /// </summary>
    public required IReadOnlyList<InvoiceItemInputDto> Items { get; init; }
}
