using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.Rules;

/// <summary>
/// Kiểm tra dữ liệu nguồn trước khi lập hóa đơn. Hàm thuần, không DB/HTTP.
/// Không tính lại tiền theo công thức khác: mọi phép tính vẫn đi qua
/// <see cref="InvoiceCalculationRules"/>.
/// </summary>
public static class InvoiceDraftRules
{
    public const int MaxDescriptionLength = 250;
    public const int MaxUnitLength = 30;

    /// <summary>
    /// Chuẩn hóa và kiểm tra các dòng nguồn. Trả về bản sao đã trim mô tả/đơn vị
    /// để phần lưu snapshot không phụ thuộc khoảng trắng từ client.
    /// </summary>
    public static IReadOnlyList<InvoiceItemInputDto> NormalizeItems(
        IReadOnlyList<InvoiceItemInputDto>? items)
    {
        if (items is null || items.Count == 0)
        {
            throw new InvoiceValidationException(
                InvoiceErrorMessages.ItemsRequired,
                InvoiceErrorCodes.ItemsRequired,
                nameof(items));
        }

        var result = new List<InvoiceItemInputDto>(items.Count);
        foreach (var item in items)
        {
            if (item is null)
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.InvalidItem,
                    InvoiceErrorCodes.InvalidItem,
                    nameof(items));
            }

            if (!Enum.IsDefined(item.ItemType))
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.InvalidItem,
                    InvoiceErrorCodes.InvalidItem,
                    nameof(item.ItemType));
            }

            var description = item.Description?.Trim() ?? string.Empty;
            if (description.Length is 0 or > MaxDescriptionLength)
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.InvalidItem,
                    InvoiceErrorCodes.InvalidItem,
                    nameof(item.Description));
            }

            var unit = string.IsNullOrWhiteSpace(item.Unit) ? null : item.Unit.Trim();
            if (unit is { Length: > MaxUnitLength })
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.InvalidItem,
                    InvoiceErrorCodes.InvalidItem,
                    nameof(item.Unit));
            }

            if (item.Quantity <= 0m)
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.InvalidItem,
                    InvoiceErrorCodes.InvalidItem,
                    nameof(item.Quantity));
            }

            if (item.UnitPrice < 0m)
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.InvalidItem,
                    InvoiceErrorCodes.InvalidItem,
                    nameof(item.UnitPrice));
            }

            // Số tiền nguồn của dòng giảm giá cũng nhập dương; rule đổi dấu âm.
            if (item.Amount is < 0m)
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.InvalidItem,
                    InvoiceErrorCodes.InvalidItem,
                    nameof(item.Amount));
            }

            var sourceAmount = item.Amount ?? (item.Quantity * item.UnitPrice);
            if (item.ItemType == InvoiceItemType.Discount && sourceAmount <= 0m)
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.InvalidItem,
                    InvoiceErrorCodes.InvalidItem,
                    nameof(item.Amount));
            }

            result.Add(new InvoiceItemInputDto
            {
                ItemType = item.ItemType,
                Description = description,
                Unit = unit,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Amount,
            });
        }

        return result;
    }
}
