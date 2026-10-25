using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;

namespace NhaTro.UnitTests.Features.Invoices;

public class InvoiceCalculationRulesTests
{
    [Fact]
    public void Tinh_tong_mau_VHT9_bang_tay()
    {
        // Mẫu đối chiếu VHT-9:
        // Thuê 3.000.000 + điện 120 kWh theo bậc 250.000 + nước 90.000 + dịch vụ 130.000
        // = 3.470.000.
        var items = new[]
        {
            Item(InvoiceItemType.Rent, "Tiền thuê phòng P101", 1m, 3_000_000m, "tháng"),
            Item(InvoiceItemType.Electric, "Điện bậc 1", 50m, 1_800m, "kWh"),
            Item(InvoiceItemType.Electric, "Điện bậc 2", 50m, 2_000m, "kWh"),
            Item(InvoiceItemType.Electric, "Điện bậc 3", 20m, 3_000m, "kWh"),
            Item(InvoiceItemType.Water, "Nước kỳ 10/2026", 6m, 15_000m, "m³"),
            Item(InvoiceItemType.Service, "Internet", 1m, 100_000m, "tháng"),
            Item(InvoiceItemType.Service, "Rác", 1m, 30_000m, "tháng"),
        };

        Assert.Equal(3_470_000m, InvoiceCalculationRules.CalculateTotal(items));

        var savedItems = items.Select(InvoiceCalculationRules.ToItem).ToList();
        Assert.Equal(3_470_000m, InvoiceCalculationRules.TotalOf(savedItems));
    }

    [Fact]
    public void Mau_VHT14_co_giam_gia_ra_dung_tong()
    {
        // Mẫu VHT-14: 3.000.000 + 700.000 + 120.000 + 100.000 - 50.000 = 3.870.000.
        var items = new[]
        {
            Item(InvoiceItemType.Rent, "Tiền thuê", 1m, 3_000_000m, "tháng", 3_000_000m),
            Item(InvoiceItemType.Electric, "Điện", 1m, 700_000m, "kỳ", 700_000m),
            Item(InvoiceItemType.Water, "Nước", 1m, 120_000m, "kỳ", 120_000m),
            Item(InvoiceItemType.Service, "Dịch vụ", 1m, 100_000m, "kỳ", 100_000m),
            Item(InvoiceItemType.Discount, "Giảm giá", 1m, 50_000m, "lần", 50_000m),
        };

        var discount = InvoiceCalculationRules.ToItem(items[^1]);
        Assert.Equal(-50_000m, discount.Amount);

        var discountFromQuantity = Item(InvoiceItemType.Discount, "Giảm giá", 1m, 50_000m, "lần");
        Assert.Equal(-50_000m, InvoiceCalculationRules.CalculateItemAmount(discountFromQuantity));

        Assert.Equal(3_870_000m, InvoiceCalculationRules.CalculateTotal(items));
    }

    [Fact]
    public void Uu_tien_so_tien_da_snapshot_tu_P5()
    {
        // P5 có thể gửi chi phí điện đã chốt theo bậc thành một số tiền, không tách đơn giá.
        var electricity = Item(
            InvoiceItemType.Electric,
            "Điện 120 kWh theo bậc",
            quantity: 120m,
            unitPrice: 0m,
            unit: "kWh",
            amount: 250_000m);

        Assert.Equal(250_000m, InvoiceCalculationRules.CalculateItemAmount(electricity));
        Assert.Equal(250_000m, InvoiceCalculationRules.CalculateTotal([electricity]));
    }

    [Fact]
    public void Lam_tron_tung_dong_roi_moi_cong()
    {
        // Từng dòng 10,5 × 1 = 10,5 -> 11. Hai dòng cộng lại phải là 22, không phải làm tròn tổng thô 21.
        var items = new[]
        {
            Item(InvoiceItemType.Service, "Dòng A", 10.5m, 1m, "lần"),
            Item(InvoiceItemType.Service, "Dòng B", 10.5m, 1m, "lần"),
        };

        Assert.Equal(11m, InvoiceCalculationRules.CalculateItemAmount(10.5m, 1m));
        Assert.Equal(22m, InvoiceCalculationRules.CalculateTotal(items));
    }

    [Fact]
    public void Tien_le_dung_5_lam_tron_len_va_4_lam_tron_xuong()
    {
        Assert.Equal(1_235m, InvoiceCalculationRules.CalculateItemAmount(1_234.5m, 1m));
        Assert.Equal(1_234m, InvoiceCalculationRules.CalculateItemAmount(1_234.4m, 1m));
    }

    [Fact]
    public void Khong_co_dong_nao_thi_tong_bang_0()
    {
        Assert.Equal(0m, InvoiceCalculationRules.CalculateTotal([]));
    }

    private static InvoiceItemInputDto Item(
        InvoiceItemType itemType,
        string description,
        decimal quantity,
        decimal unitPrice,
        string unit,
        decimal? amount = null) =>
        new()
        {
            ItemType = itemType,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice,
            Unit = unit,
            Amount = amount,
        };
}
