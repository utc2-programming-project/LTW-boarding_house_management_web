using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.Entities;

/// <summary>
/// Một dòng đã chốt của hóa đơn. <see cref="Amount"/> đã làm tròn VND; dòng giảm giá lưu số âm.
/// </summary>
public sealed class InvoiceItem
{
    public int Id { get; set; }

    public int InvoiceId { get; set; }

    public InvoiceItemType ItemType { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>Đơn vị hiển thị, ví dụ kWh, m³, tháng, người.</summary>
    public string? Unit { get; set; }

    public decimal Quantity { get; set; } = 1m;

    public decimal UnitPrice { get; set; }

    /// <summary>Thành tiền VND đã làm tròn; âm chỉ với <see cref="InvoiceItemType.Discount"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>Thứ tự hiển thị ổn định trong hóa đơn.</summary>
    public int DisplayOrder { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
