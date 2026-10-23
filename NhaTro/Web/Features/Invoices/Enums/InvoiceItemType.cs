namespace Web.Features.Invoices.Enums;

/// <summary>
/// Loại dòng hóa đơn. P6 nhận tiền thuê từ P4 và chi phí điện/nước/dịch vụ từ P5;
/// không tạo loại dòng cho nghiệp vụ ngoài D1–D17.
/// </summary>
public enum InvoiceItemType
{
    Rent = 0,
    Electric = 1,
    Water = 2,
    Service = 3,

    /// <summary>Giảm giá; số tiền nguồn nhập dương, rule đổi dấu khi cộng vào hóa đơn.</summary>
    Discount = 4,
}
