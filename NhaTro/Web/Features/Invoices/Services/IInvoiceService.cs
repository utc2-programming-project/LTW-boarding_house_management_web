using Web.Features.Invoices.DTOs;
using Web.Features.Properties;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Lệnh lập/phát hành hóa đơn P6. Chỉ Chủ trọ/Quản lý được thao tác trên hợp đồng
/// thuộc phạm vi của mình; Khách thuê và Admin bị từ chối.
/// </summary>
public interface IInvoiceService
{
    /// <summary>
    /// Lập hóa đơn nháp từ các dòng nguồn đã chốt. Gửi lặp cùng hợp đồng/kỳ và cùng
    /// dữ liệu trả lại hóa đơn đã có, không tạo bản ghi thứ hai.
    /// </summary>
    Task<InvoiceDto> CreateDraftAsync(
        CreateInvoiceRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Phát hành hóa đơn nháp. Gọi lặp trên hóa đơn đã phát hành trả lại hóa đơn hiện tại.
    /// </summary>
    Task<InvoiceDto> IssueAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lập và phát hành trong cùng transaction. Nếu phát hành lỗi thì hóa đơn nháp
    /// vừa tạo cũng bị rollback.
    /// </summary>
    Task<InvoiceDto> CreateAndIssueAsync(
        CreateInvoiceRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
