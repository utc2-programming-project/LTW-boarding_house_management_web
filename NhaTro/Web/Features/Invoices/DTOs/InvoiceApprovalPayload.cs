namespace Web.Features.Invoices.DTOs;

/// <summary>
/// Dữ liệu đề xuất của yêu cầu SW-10 (lập hóa đơn nháp). Đủ để applier dựng lại đúng hóa đơn
/// mà Quản lý đã xin tạo khi Chủ trọ duyệt, vì bản thân yêu cầu không ghi gì vào bảng hóa đơn.
/// </summary>
public sealed record InvoiceDraftApprovalPayload(
    Guid ContractId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    IReadOnlyList<InvoiceItemInputDto> Items);

/// <summary>Dữ liệu đề xuất của yêu cầu SW-11 (phát hành hóa đơn) — trỏ tới chính hóa đơn nháp.</summary>
public sealed record InvoiceIssueApprovalPayload(int InvoiceId);
