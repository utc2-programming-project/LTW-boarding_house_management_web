using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Invoices.Enums;
using Web.Features.Payments.Entities;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Settings;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;

namespace Web.Features.Reports.Services;

public static class ReportExportErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string ApprovalRequired = nameof(ApprovalRequired);
    public const string ApprovalDenied = nameof(ApprovalDenied);
    public const string ApprovalFailed = nameof(ApprovalFailed);
}

public sealed class ReportExportAuthorizationException(string message, string errorCode)
    : UnauthorizedAccessException(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class ReportExportException(string message, string errorCode)
    : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>
/// D8 — xuất Excel báo cáo doanh thu (chỉ P8). Chủ trọ xuất toàn bộ; Quản lý xuất trong
/// phạm vi tòa được giao và phải qua SW-16. Không đưa số CCCD/ảnh giấy tờ vào workbook.
/// </summary>
public sealed class ReportExportService(
    ApplicationDbContext db,
    IManagerTogglePolicy? togglePolicy = null,
    IManagerApprovalService? approvals = null)
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<byte[]> ExportRevenueExcelAsync(
        PropertyCaller caller,
        RevenueReportFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        filter ??= new RevenueReportFilter();

        if (caller.IsAdmin || caller.IsKhachThue)
        {
            throw new ReportExportAuthorizationException(
                "Chỉ Chủ trọ hoặc Quản lý được xuất báo cáo Excel.",
                ReportExportErrorCodes.Forbidden);
        }

        if (caller.IsQuanLy)
        {
            var decision = togglePolicy is null
                ? RoomChangeDecision.RequiresApproval
                : await togglePolicy.EvaluateAsync(ManagerAction.SW16, caller.Role, cancellationToken);

            if (decision == RoomChangeDecision.Denied)
            {
                throw new ReportExportException(
                    "Công tắc SW-16 đang chặn xuất báo cáo.",
                    ReportExportErrorCodes.ApprovalDenied);
            }

            if (decision == RoomChangeDecision.RequiresApproval)
            {
                await CreateApprovalRequestAsync(filter, caller, cancellationToken);
                throw new ReportExportException(
                    "Xuất báo cáo đang chờ Chủ trọ duyệt.",
                    ReportExportErrorCodes.ApprovalRequired);
            }
        }

        var propertyIds = await VisiblePropertyIds(caller).ToListAsync(cancellationToken);

        var rows = await (
            from invoice in db.Invoices.AsNoTracking()
            join contract in db.Contracts.AsNoTracking()
                on invoice.ContractId equals contract.ContractId
            join room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                on contract.RoomId equals room.Id
            where propertyIds.Contains(room.PropertyId)
                && (filter.From == null || invoice.PeriodEnd >= filter.From)
                && (filter.To == null || invoice.PeriodStart <= filter.To)
            orderby invoice.Id
            select new
            {
                InvoiceId = invoice.Id,
                room.Code,
                invoice.TotalAmount,
                invoice.Status,
            })
            .ToListAsync(cancellationToken);

        var invoiceIds = rows.Select(row => row.InvoiceId).ToList();
        var paid = await db.Set<Payment>().AsNoTracking()
            .Where(payment => invoiceIds.Contains(payment.InvoiceId)
                && payment.Status == PaymentStatus.Confirmed)
            .GroupBy(payment => payment.InvoiceId)
            .Select(group => new { InvoiceId = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToDictionaryAsync(item => item.InvoiceId, item => item.Amount, cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("DoanhThu");
        string[] headers = ["Mã hóa đơn", "Phòng", "Tổng hóa đơn", "Đã thu", "Còn lại", "Trạng thái"];
        for (var index = 0; index < headers.Length; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            var paidAmount = paid.GetValueOrDefault(row.InvoiceId);
            var outstanding = row.Status == InvoiceStatus.Issued
                ? Math.Max(0m, row.TotalAmount - paidAmount)
                : 0m;
            sheet.Cell(rowIndex, 1).Value = row.InvoiceId;
            sheet.Cell(rowIndex, 2).Value = row.Code;
            sheet.Cell(rowIndex, 3).Value = row.TotalAmount;
            sheet.Cell(rowIndex, 4).Value = paidAmount;
            sheet.Cell(rowIndex, 5).Value = outstanding;
            sheet.Cell(rowIndex, 6).Value = row.Status.ToString();
            rowIndex++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private IQueryable<int> VisiblePropertyIds(PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return db.Properties.AsNoTracking()
                .Where(property => property.OwnerId == caller.UserId)
                .Select(property => property.Id);
        }

        return db.PropertyManagers.AsNoTracking()
            .Where(assignment => assignment.ManagerUserId == caller.UserId
                && assignment.RevokedAt == null)
            .Select(assignment => assignment.PropertyId);
    }

    private async Task CreateApprovalRequestAsync(
        RevenueReportFilter filter,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (approvals is null)
        {
            throw new ReportExportException(
                "Không thể gửi yêu cầu duyệt xuất báo cáo.",
                ReportExportErrorCodes.ApprovalFailed);
        }

        var targetKey = $"export:{filter.From?.ToString("yyyy-MM-dd") ?? "all"}:{filter.To?.ToString("yyyy-MM-dd") ?? "all"}";
        var approval = await approvals.CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = ManagerToggleCatalog.ToCode(ManagerAction.SW16),
                TargetType = ApprovalTargetType.Report,
                TargetKey = targetKey,
                PayloadJson = JsonSerializer.Serialize(filter, PayloadJsonOptions),
            },
            new ApprovalCaller(caller.UserId, caller.Role),
            cancellationToken);

        if (!approval.Succeeded)
        {
            throw new ReportExportException(
                approval.ErrorMessage ?? "Không thể gửi yêu cầu duyệt xuất báo cáo.",
                ReportExportErrorCodes.ApprovalFailed);
        }
    }
}
