using Web.Common.Models;
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

    Task DeleteDraftAsync(
        int invoiceId, PropertyCaller caller, CancellationToken cancellationToken = default);

    /// <summary>
    /// Chủ trọ chỉnh sửa hóa đơn đã phát hành khi khách CHƯA gửi yêu cầu thanh toán.
    /// Bắt buộc có lý do; lý do được lưu vào lịch sử để khách thuê thấy.
    /// Hóa đơn vẫn giữ trạng thái Issued để khách thanh toán được bình thường.
    /// Thao tác này KHÔNG tự gửi thông báo cho khách: chủ trọ bấm "Gửi bill đến khách"
    /// thì bản mới nhất mới được gửi (<see cref="MarkBillSentAsync"/>).
    /// </summary>
    Task<InvoiceDto> ReviseAsync(
        int invoiceId,
        ReviseInvoiceRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ghi nhận hoá đơn đã gửi (kèm bản chỉnh sửa mới nhất) cho khách.
    /// </summary>
    Task MarkBillSentAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chủ trọ xóa hóa đơn đã phát hành khi khách CHƯA gửi yêu cầu thanh toán.
    /// Sau khi xóa, khách không còn thấy hoá đơn nào; kỳ chỉ số trở lại trạng thái chờ lập hoá đơn.
    /// </summary>
    Task DeleteIssuedAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        int invoiceId, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InvoiceTrashItemDto>> ListDeletedAsync(
        PropertyCaller caller, CancellationToken cancellationToken = default);

    Task PurgeAsync(
        int invoiceId, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<TrashPurgeSummary> PurgeAllAsync(
        PropertyCaller caller, CancellationToken cancellationToken = default);
}
