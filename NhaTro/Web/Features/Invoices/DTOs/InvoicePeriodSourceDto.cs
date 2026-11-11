namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Tổng hợp nguồn thật của một kỳ trước khi lập hóa đơn P6: tiền thuê từ hợp đồng,
/// điện/nước từ VHT-51 và dịch vụ theo đơn giá đang hiệu lực. Dùng cho màn hình xác nhận
/// và để kiểm tra tổng trước khi gọi <c>IInvoiceService</c>.
/// </summary>
public sealed record InvoicePeriodSourceDto(
    Guid ContractId,
    string ContractNumber,
    string RoomCode,
    string PropertyName,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<InvoiceItemInputDto> Items,
    decimal TotalAmount,
    int? UtilityReadingId);
