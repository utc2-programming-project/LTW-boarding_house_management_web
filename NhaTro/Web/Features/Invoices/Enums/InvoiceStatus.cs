namespace Web.Features.Invoices.Enums;

/// <summary>
/// Trạng thái hóa đơn P6.
/// D13 chốt không hủy hóa đơn trong v1.0 nên enum không có <c>Cancelled</c>.
/// Gửi yêu cầu thanh toán không đổi trạng thái <see cref="Issued"/>; chỉ khi người có quyền
/// xác nhận đủ tiền thì hóa đơn mới chuyển <see cref="Paid"/>.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Đang lập, có thể sửa dòng hóa đơn.</summary>
    Draft = 0,

    /// <summary>Đã phát hành, còn phải thu.</summary>
    Issued = 1,

    /// <summary>Đã xác nhận thu đủ tiền; trạng thái cuối của hóa đơn.</summary>
    Paid = 2,
}
