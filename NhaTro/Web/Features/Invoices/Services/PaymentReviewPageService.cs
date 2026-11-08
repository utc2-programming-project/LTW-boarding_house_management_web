using Web.Features.Invoices.DTOs;
using Web.Features.Storage.DTOs;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Adapter UI VHT-56: lấy caller thật từ principal, đọc hàng đợi và gọi xác nhận/từ chối qua
/// <see cref="IPaymentService"/>. Trang Blazor chỉ giữ trạng thái hiển thị, không tự ghi DB.
/// </summary>
public sealed class PaymentReviewPageService(
    PaymentPageService paymentPage,
    IPaymentReviewQueryService reviewQuery,
    IPaymentService payments)
{
    public async Task<PaymentReviewListResult> ListAsync(
        PaymentReviewFilter filter,
        CancellationToken cancellationToken = default) =>
        await reviewQuery.GetRequestsAsync(filter, await paymentPage.CallerAsync(), cancellationToken);

    public async Task<PaymentReviewItemDto> DetailAsync(
        int paymentId,
        CancellationToken cancellationToken = default) =>
        await reviewQuery.GetRequestAsync(paymentId, await paymentPage.CallerAsync(), cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, FileMetadataDto>> ProofFilesAsync(
        int invoiceId,
        CancellationToken cancellationToken = default) =>
        (await paymentPage.ProofFilesAsync(invoiceId, cancellationToken))
        .ToDictionary(file => file.Id);

    /// <summary>
    /// Xác nhận thu tiền. Service là idempotent: gọi lại sau khi mất kết nối trả kết quả đã ghi
    /// thay vì cộng tiền lần nữa; UI vẫn bắt buộc tra kết quả trước khi thao tác tiếp.
    /// </summary>
    public async Task<PaymentDto> ConfirmAsync(
        int paymentId,
        CancellationToken cancellationToken = default) =>
        await payments.ConfirmAsync(paymentId, await paymentPage.CallerAsync(), cancellationToken);

    public async Task<PaymentDto> RejectAsync(
        int paymentId,
        string reason,
        CancellationToken cancellationToken = default) =>
        await payments.RejectAsync(
            paymentId,
            new RejectPaymentRequest { Reason = reason },
            await paymentPage.CallerAsync(),
            cancellationToken);
}
