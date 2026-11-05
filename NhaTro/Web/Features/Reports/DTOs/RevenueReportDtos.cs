namespace Web.Features.Reports.DTOs;

public sealed record RevenueReportFilter(DateOnly? From = null, DateOnly? To = null);

public sealed record RevenueReportLineDto(
    int InvoiceId,
    int RoomId,
    string RoomCode,
    decimal Total,
    decimal Paid,
    decimal Outstanding,
    string Status);

public sealed record RevenueReportDto(
    decimal TotalRevenue,
    decimal Outstanding,
    int InvoiceCount,
    int PaidInvoiceCount,
    IReadOnlyList<RevenueReportLineDto> Lines);
