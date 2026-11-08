namespace Web.Features.Reports.DTOs;

/// <summary>
/// VHT-57 — cơ sở xác định doanh thu theo kỳ.
/// <see cref="InvoicePeriod"/> (mặc định) ghi nhận tiền thu theo kỳ của hóa đơn;
/// <see cref="PaymentDate"/> ghi nhận theo ngày xác nhận thanh toán (ReviewedAt).
/// Công nợ luôn tính trên hóa đơn hợp lệ trong kỳ lọc.
/// </summary>
public enum RevenueDateBasis
{
    InvoicePeriod = 0,
    PaymentDate = 1,
}

/// <summary>
/// Bộ lọc báo cáo doanh thu/công nợ. Giữ nguyên tham số vị trí (From, To) để
/// Home/Excel hiện tại không phải đổi lời gọi.
/// </summary>
public sealed record RevenueReportFilter(
    DateOnly? From = null,
    DateOnly? To = null,
    int? PropertyId = null,
    RevenueDateBasis DateBasis = RevenueDateBasis.InvoicePeriod);

/// <summary>
/// Một dòng đối chiếu theo hóa đơn. <see cref="Paid"/> là tổng tiền đã xác nhận
/// (Confirmed) của hóa đơn; <see cref="RecognizedRevenue"/> là phần được ghi nhận
/// vào kỳ theo <see cref="RevenueReportFilter.DateBasis"/>.
/// </summary>
public sealed record RevenueReportLineDto(
    int InvoiceId,
    int RoomId,
    string RoomCode,
    decimal Total,
    decimal Paid,
    decimal Outstanding,
    string Status,
    int PropertyId = 0,
    string PropertyName = "",
    DateOnly PeriodStart = default,
    DateOnly PeriodEnd = default,
    decimal RecognizedRevenue = 0m);

/// <summary>Nhóm đối chiếu theo kỳ (tháng bắt đầu của kỳ hóa đơn).</summary>
public sealed record RevenuePeriodGroupDto(
    string PeriodKey,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal TotalInvoiced,
    decimal RecognizedRevenue,
    decimal Outstanding,
    int InvoiceCount,
    int PaidInvoiceCount);

/// <summary>Nhóm đối chiếu theo phạm vi (nhà trọ), kèm các kỳ con.</summary>
public sealed record RevenuePropertyGroupDto(
    int PropertyId,
    string PropertyName,
    decimal TotalInvoiced,
    decimal RecognizedRevenue,
    decimal Outstanding,
    int InvoiceCount,
    int PaidInvoiceCount,
    IReadOnlyList<RevenuePeriodGroupDto> Periods);

/// <summary>
/// Kết quả báo cáo doanh thu/công nợ.
/// Bất biến đối chiếu: <see cref="TotalRevenue"/> = tổng <c>RecognizedRevenue</c>
/// của các dòng = tổng các nhóm kỳ = tổng các nhóm nhà;
/// <see cref="Outstanding"/> = tổng công nợ các hóa đơn <c>Issued</c>;
/// <see cref="TotalInvoiced"/> = tổng tiền hóa đơn hợp lệ (Issued + Paid).
/// <see cref="PendingAmount"/> và <see cref="RejectedAmount"/> chỉ để đối chiếu,
/// không bao giờ cộng vào doanh thu.
/// </summary>
public sealed record RevenueReportDto(
    decimal TotalRevenue,
    decimal Outstanding,
    int InvoiceCount,
    int PaidInvoiceCount,
    IReadOnlyList<RevenueReportLineDto> Lines,
    decimal TotalInvoiced = 0m,
    decimal PendingAmount = 0m,
    decimal RejectedAmount = 0m,
    RevenueDateBasis DateBasis = RevenueDateBasis.InvoicePeriod,
    IReadOnlyList<RevenuePeriodGroupDto>? Periods = null,
    IReadOnlyList<RevenuePropertyGroupDto>? Properties = null);
