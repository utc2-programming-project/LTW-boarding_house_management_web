using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Nghiệp vụ P6 cho yêu cầu thanh toán và xác nhận thu tiền.
/// <para>
/// Luồng tách biệt: khách gửi yêu cầu (<see cref="PaymentStatus.Pending"/>) không đổi tiền đã thu;
/// chỉ Quản lý/Chủ trọ xác nhận mới ghi tiền và chuyển hóa đơn sang Paid. Khách thuê và Admin
/// không được gọi thao tác xác nhận. Mọi thay đổi tiền/hóa đơn/audit nằm trong cùng transaction.
/// </para>
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Khách thuê gửi yêu cầu thanh toán kèm minh chứng cho hóa đơn đã phát hành.
    /// Gửi lặp cùng dữ liệu khi đang chờ trả lại yêu cầu đã có, không tạo bản ghi thứ hai.
    /// </summary>
    Task<PaymentDto> SubmitAsync(
        int invoiceId,
        SubmitPaymentRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Quản lý/Chủ trọ xác nhận đã thu đủ tiền. Ghi Payment Confirmed, hóa đơn Paid và audit
    /// trong cùng transaction; gọi lặp sau khi đã xác nhận trả lại kết quả hiện tại.
    /// </summary>
    Task<PaymentDto> ConfirmAsync(
        int paymentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Từ chối minh chứng đang chờ; không cộng tiền và không đổi hóa đơn.</summary>
    Task<PaymentDto> RejectAsync(
        int paymentId,
        RejectPaymentRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tra kết quả mới nhất theo hóa đơn để UI kiểm trước khi gửi lại sau mất kết nối.
    /// Trả null khi hóa đơn chưa có yêu cầu thanh toán nào.
    /// </summary>
    Task<PaymentDto?> GetLatestPaymentAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Danh sách các lần gửi của một hóa đơn, mới nhất trước.</summary>
    Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết một yêu cầu thanh toán trong phạm vi người gọi.</summary>
    Task<PaymentDto> GetPaymentAsync(
        int paymentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
