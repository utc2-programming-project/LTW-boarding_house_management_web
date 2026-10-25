namespace Web.Features.Invoices.Enums;

/// <summary>
/// Trạng thái một lần khách thuê gửi thông tin thanh toán.
/// <see cref="Pending"/> chỉ là yêu cầu chờ đối chiếu, không phải tiền đã thu.
/// D13: không trả một phần; từ chối minh chứng thì khách được gửi lại yêu cầu mới.
/// </summary>
public enum PaymentStatus
{
    /// <summary>Khách đã gửi yêu cầu/minh chứng, chờ Quản lý hoặc Chủ trọ xác nhận.</summary>
    Pending = 0,

    /// <summary>Đã đối chiếu và xác nhận thu đủ tiền cho hóa đơn.</summary>
    Confirmed = 1,

    /// <summary>Minh chứng bị từ chối; không tính là tiền đã thu.</summary>
    Rejected = 2,
}
