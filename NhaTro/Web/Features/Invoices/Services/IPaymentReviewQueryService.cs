using Web.Features.Invoices.DTOs;
using Web.Features.Properties;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Hàng đợi yêu cầu thanh toán cho Quản lý/Chủ trọ (VHT-56). Chỉ đọc dữ liệu trong phạm vi
/// nhà trọ được giao; Admin và Khách thuê không được dùng màn hình xác nhận.
/// </summary>
public interface IPaymentReviewQueryService
{
    Task<PaymentReviewListResult> GetRequestsAsync(
        PaymentReviewFilter filter,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    /// <summary>Chi tiết một yêu cầu trong phạm vi người gọi; ném lỗi nếu ngoài phạm vi.</summary>
    Task<PaymentReviewItemDto> GetRequestAsync(
        int paymentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
