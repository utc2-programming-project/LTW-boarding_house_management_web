using Web.Features.Invoices.DTOs;
using Web.Features.Properties;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Lập/phát hành hóa đơn từ nguồn thật (VHT-137 / 3B): dựng dòng tiền thuê từ hợp đồng,
/// điện/nước từ VHT-51 và dịch vụ theo đơn giá hiệu lực, rồi chuyển cho <see cref="IInvoiceService"/>
/// để lưu snapshot và chống trùng kỳ. Chỉ Chủ trọ và Quản lý trong phạm vi được gọi.
/// </summary>
public interface IInvoiceIssuanceService
{
    /// <summary>Dựng trước các dòng và tổng của một kỳ mà không ghi DB.</summary>
    Task<InvoicePeriodSourceDto> PreviewPeriodAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Lập hóa đơn nháp từ nguồn thật; gửi lặp cùng kỳ và cùng dữ liệu trả lại hóa đơn cũ.</summary>
    Task<InvoiceDto> CreateDraftAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Lập và phát hành trong một transaction; lỗi phát hành rollback cả hóa đơn nháp.</summary>
    Task<InvoiceDto> CreateAndIssueAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
