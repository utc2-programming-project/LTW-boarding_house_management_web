using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Một dòng hóa đơn đã chốt. <see cref="Amount"/> là số nguyên VND sau khi làm tròn riêng dòng này.
/// </summary>
public sealed class InvoiceItemDto
{
    public required InvoiceItemType ItemType { get; init; }

    public required string Description { get; init; }

    /// <summary>Đơn vị hiển thị, ví dụ kWh, m³, tháng, người.</summary>
    public string? Unit { get; init; }

    public required decimal Quantity { get; init; }

    public required decimal UnitPrice { get; init; }

    /// <summary>
    /// Thành tiền VND đã làm tròn. Dòng <see cref="InvoiceItemType.Discount"/> lưu số âm
    /// để tổng hóa đơn trừ đúng khoản giảm.
    /// </summary>
    public required decimal Amount { get; init; }
}
