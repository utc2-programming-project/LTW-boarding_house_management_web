using System.ComponentModel.DataAnnotations;
using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Dữ liệu đầu vào của một dòng hóa đơn. <see cref="Amount"/> chỉ là số tiền nguồn trước làm tròn;
/// thành tiền cuối cùng do <c>InvoiceCalculationRules</c> tính để tránh client gửi số đã làm tròn khác nhau.
/// </summary>
public sealed class InvoiceItemInputDto
{
    public required InvoiceItemType ItemType { get; init; }

    [Required(ErrorMessage = "Nhập nội dung dòng hóa đơn.")]
    [MaxLength(250, ErrorMessage = "Nội dung dòng hóa đơn tối đa 250 ký tự.")]
    public required string Description { get; init; }

    /// <summary>Đơn vị hiển thị, ví dụ kWh, m³, tháng, người.</summary>
    [MaxLength(20, ErrorMessage = "Đơn vị tối đa 20 ký tự.")]
    public string? Unit { get; init; }

    [Range(typeof(decimal), "0.0001", "79228162514264337593543950335",
        ErrorMessage = "Số lượng phải lớn hơn 0.")]
    public decimal Quantity { get; init; } = 1m;

    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Đơn giá không được âm.")]
    public decimal UnitPrice { get; init; }

    /// <summary>
    /// Số tiền nguồn đã snapshot từ P4/P5, trước khi làm tròn VND.
    /// <c>null</c> thì rule tự tính <see cref="Quantity"/> × <see cref="UnitPrice"/>.
    /// Dùng khi chi phí điện/nước đã chốt theo bậc hoặc nguồn không tách được đơn giá.
    /// Dòng <see cref="InvoiceItemType.Discount"/> nhập số dương; rule tự đổi dấu âm.
    /// </summary>
    [Range(typeof(decimal), "0", "79228162514264337593543950335",
        ErrorMessage = "Thành tiền nguồn không được âm.")]
    public decimal? Amount { get; init; }
}
