using System.ComponentModel.DataAnnotations;
using Web.Features.Invoices.Enums;

namespace Web.Features.Invoices.DTOs;

/// <summary>Một dòng trong danh sách hóa đơn P6.</summary>
public sealed class InvoiceListItemDto
{
    public required int Id { get; init; }

    public required Guid ContractId { get; init; }

    public required string ContractNumber { get; init; }

    public required string RoomCode { get; init; }

    public required string PropertyName { get; init; }

    public required IReadOnlyList<string> TenantNames { get; init; }

    public required DateOnly PeriodStart { get; init; }

    public required DateOnly PeriodEnd { get; init; }

    public DateOnly? DueDate { get; init; }

    public required InvoiceStatus Status { get; init; }

    public required decimal TotalAmount { get; init; }

    /// <summary>Chỉ cộng Payment Confirmed; Pending/Rejected không được tính.</summary>
    public required decimal PaidAmount { get; init; }

    /// <summary>Tổng hóa đơn trừ tiền đã xác nhận, không âm.</summary>
    public required decimal OutstandingAmount { get; init; }
}

/// <summary>Trạng thái thu tiền của hóa đơn, suy từ các Payment và hạn thanh toán.</summary>
public enum InvoicePaymentState
{
    Unpaid,
    Partial,
    Paid,
    PendingConfirmation,
    PaymentRejected,
    Expired,
}

/// <summary>Bộ lọc danh sách hóa đơn theo kỳ, hợp đồng, trạng thái và số tiền.</summary>
public sealed class InvoiceFilter
{
    /// <summary>Tìm theo mã hóa đơn, số hợp đồng, mã phòng, tên nhà hoặc tên khách thuê.</summary>
    public string? Keyword { get; set; }

    public int? Year { get; set; }

    public int? Month { get; set; }

    public Guid? ContractId { get; set; }

    public InvoiceStatus? Status { get; set; }

    public InvoicePaymentState? PaymentState { get; set; }

    public decimal? MinAmount { get; set; }

    public decimal? MaxAmount { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>Chuẩn hóa tháng/năm và kẹp trang để query không nhận giá trị lạ từ UI.</summary>
    public void Normalize()
    {
        Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword.Trim();

        if (MinAmount is < 0)
        {
            MinAmount = null;
        }

        if (MaxAmount is < 0)
        {
            MaxAmount = null;
        }

        if (MinAmount is { } min && MaxAmount is { } max && min > max)
        {
            (MinAmount, MaxAmount) = (max, min);
        }

        if (Month is < 1 or > 12 || Year is < 2000 or > 2100)
        {
            Month = null;
            Year = null;
        }

        if (Month is null || Year is null)
        {
            Month = null;
            Year = null;
        }

        Page = Math.Max(1, Page);
        PageSize = Math.Clamp(PageSize, 1, 100);
    }

    /// <summary>Khoảng ngày của tháng đang lọc, tính cả hai đầu; null khi không lọc kỳ.</summary>
    public (DateOnly Start, DateOnly End)? PeriodRange()
    {
        if (Year is not { } year || Month is not { } month)
        {
            return null;
        }

        var start = new DateOnly(year, month, 1);
        return (start, start.AddMonths(1).AddDays(-1));
    }
}

/// <summary>Lựa chọn hợp đồng cho bộ lọc, đã giới hạn theo phạm vi người gọi.</summary>
public sealed record InvoiceContractOptionDto(
    Guid ContractId,
    string ContractNumber,
    string RoomCode,
    string PropertyName);

/// <summary>Tổng hợp nhanh trên toàn bộ kết quả lọc (không chỉ trang hiện tại).</summary>
public sealed class InvoiceSummaryDto
{
    public required int DraftCount { get; init; }

    public required int IssuedCount { get; init; }

    public required int PaidCount { get; init; }

    /// <summary>Tổng còn phải thu của các hóa đơn chưa Paid.</summary>
    public required decimal OutstandingAmount { get; init; }
}

/// <summary>Kết quả danh sách hóa đơn kèm tổng hợp cho thẻ KPI.</summary>
public sealed class InvoiceListResult
{
    public required IReadOnlyList<InvoiceListItemDto> Items { get; init; }

    public required int TotalCount { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required InvoiceSummaryDto Summary { get; init; }
}
