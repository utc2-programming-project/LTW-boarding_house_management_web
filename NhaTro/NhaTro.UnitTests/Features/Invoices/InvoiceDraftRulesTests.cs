using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;

namespace NhaTro.UnitTests.Features.Invoices;

public class InvoiceDraftRulesTests
{
    [Fact]
    public void NormalizeItems_TrimsDescriptionAndUnit()
    {
        var items = InvoiceDraftRules.NormalizeItems(
        [
            Item(InvoiceItemType.Rent, "  Tiền thuê  ", " tháng ", 1m, 3_000_000m),
        ]);

        Assert.Single(items);
        Assert.Equal("Tiền thuê", items[0].Description);
        Assert.Equal("tháng", items[0].Unit);
    }

    [Fact]
    public void NormalizeItems_RejectsEmptyList()
    {
        var exception = Assert.Throws<InvoiceValidationException>(
            () => InvoiceDraftRules.NormalizeItems([]));

        Assert.Equal(InvoiceErrorCodes.ItemsRequired, exception.ErrorCode);
    }

    [Fact]
    public void NormalizeItems_RejectsInvalidQuantity()
    {
        var exception = Assert.Throws<InvoiceValidationException>(
            () => InvoiceDraftRules.NormalizeItems(
            [
                Item(InvoiceItemType.Rent, "Tiền thuê", "tháng", 0m, 3_000_000m),
            ]));

        Assert.Equal(InvoiceErrorCodes.InvalidItem, exception.ErrorCode);
    }

    [Fact]
    public void NormalizeItems_RejectsNegativeSourceAmount()
    {
        var exception = Assert.Throws<InvoiceValidationException>(
            () => InvoiceDraftRules.NormalizeItems(
            [
                Item(InvoiceItemType.Electric, "Điện", "kWh", 1m, -1m, -1m),
            ]));

        Assert.Equal(InvoiceErrorCodes.InvalidItem, exception.ErrorCode);
    }

    [Fact]
    public void NormalizeItems_RequiresPositiveDiscountSourceAmount()
    {
        var exception = Assert.Throws<InvoiceValidationException>(
            () => InvoiceDraftRules.NormalizeItems(
            [
                Item(InvoiceItemType.Discount, "Giảm giá", "lần", 1m, 0m),
            ]));

        Assert.Equal(InvoiceErrorCodes.InvalidItem, exception.ErrorCode);
    }

    [Fact]
    public void NormalizeItems_RejectsUnknownItemType()
    {
        var exception = Assert.Throws<InvoiceValidationException>(
            () => InvoiceDraftRules.NormalizeItems(
            [
                Item((InvoiceItemType)99, "Loại lạ", null, 1m, 1_000m),
            ]));

        Assert.Equal(InvoiceErrorCodes.InvalidItem, exception.ErrorCode);
    }

    [Fact]
    public void NormalizeItems_AllowsDiscountFromQuantityAndUnitPrice()
    {
        var items = InvoiceDraftRules.NormalizeItems(
        [
            Item(InvoiceItemType.Discount, "Giảm giá", "lần", 1m, 50_000m),
        ]);

        Assert.Equal(-50_000m, InvoiceCalculationRules.CalculateItemAmount(items[0]));
    }

    [Fact]
    public void NormalizeItems_ThenCalculateTotal_MatchesSettledSource()
    {
        var items = InvoiceDraftRules.NormalizeItems(
        [
            Item(InvoiceItemType.Rent, "Tiền thuê", "tháng", 1m, 3_000_000m, 3_000_000m),
            Item(InvoiceItemType.Electric, "Điện", "kWh", 120m, 2_083.333m, 250_000m),
            Item(InvoiceItemType.Water, "Nước", "m³", 6m, 15_000m, 90_000m),
            Item(InvoiceItemType.Service, "Internet", "tháng", 1m, 100_000m),
            Item(InvoiceItemType.Discount, "Giảm giá", "lần", 1m, 50_000m, 50_000m),
        ]);

        // 3.000.000 + 250.000 + 90.000 + 100.000 - 50.000 = 3.390.000.
        Assert.Equal(3_390_000m, InvoiceCalculationRules.CalculateTotal(items));
    }

    private static InvoiceItemInputDto Item(
        InvoiceItemType type,
        string description,
        string? unit,
        decimal quantity,
        decimal unitPrice,
        decimal? amount = null) =>
        new()
        {
            ItemType = type,
            Description = description,
            Unit = unit,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Amount = amount,
        };
}
