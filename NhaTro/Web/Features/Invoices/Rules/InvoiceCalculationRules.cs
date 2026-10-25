using Web.Common.Utils;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.Rules;

/// <summary>
/// Quy tắc tính tiền hóa đơn P6. Hàm thuần, không DB/HTTP.
/// Quy ước chung: làm tròn từng dòng về số nguyên VND (đúng .5 làm tròn lên) rồi mới cộng;
/// không lấy tổng thô rồi làm tròn một lần.
/// </summary>
public static class InvoiceCalculationRules
{
    /// <summary>Làm tròn một số tiền nguồn (P4/P5 snapshot) về VND.</summary>
    public static decimal CalculateItemAmount(decimal amount) =>
        MoneyFormatter.RoundVnd(amount);

    /// <summary>
    /// Thành tiền một dòng tính từ số lượng và đơn giá, dùng cho dòng đơn giản
    /// (thuê, nước, dịch vụ hoặc từng bậc điện đã tách dòng).
    /// </summary>
    public static decimal CalculateItemAmount(decimal quantity, decimal unitPrice) =>
        CalculateItemAmount(quantity * unitPrice);

    /// <summary>
    /// Thành tiền một dòng đầu vào: ưu tiên <see cref="InvoiceItemInputDto.Amount"/> đã snapshot;
    /// nếu null mới tính Quantity × UnitPrice. Dòng <see cref="InvoiceItemType.Discount"/> được đổi dấu âm
    /// để tổng hóa đơn trừ đúng khoản giảm.
    /// </summary>
    public static decimal CalculateItemAmount(InvoiceItemInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var amount = input.Amount is { } sourceAmount
            ? CalculateItemAmount(sourceAmount)
            : CalculateItemAmount(input.Quantity, input.UnitPrice);

        return input.ItemType == InvoiceItemType.Discount ? -amount : amount;
    }

    /// <summary>Tạo dòng hóa đơn đã chốt từ dữ liệu đầu vào, chỉ tính thành tiền một lần tại đây.</summary>
    public static InvoiceItemDto ToItem(InvoiceItemInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new InvoiceItemDto
        {
            ItemType = input.ItemType,
            Description = input.Description,
            Unit = input.Unit,
            Quantity = input.Quantity,
            UnitPrice = input.UnitPrice,
            Amount = CalculateItemAmount(input),
        };
    }

    /// <summary>Tổng hóa đơn = cộng thành tiền đã làm tròn của từng dòng.</summary>
    public static decimal CalculateTotal(IEnumerable<InvoiceItemInputDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var total = 0m;
        foreach (var item in items)
        {
            total += CalculateItemAmount(item);
        }

        return total;
    }

    /// <summary>Tổng từ các dòng đã chốt; dùng khi đọc lại dữ liệu đã lưu, không tính lại từ quantity/unit price.</summary>
    public static decimal TotalOf(IEnumerable<InvoiceItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var total = 0m;
        foreach (var item in items)
        {
            total += item.Amount;
        }

        return total;
    }
}
