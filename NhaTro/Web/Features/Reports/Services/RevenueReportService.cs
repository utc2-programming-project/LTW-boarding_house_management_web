using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;

namespace Web.Features.Reports.Services;

public static class RevenueReportErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
}

public sealed class RevenueReportAuthorizationException(string message)
    : UnauthorizedAccessException(message)
{
    public string ErrorCode { get; } = RevenueReportErrorCodes.Forbidden;
}

/// <summary>
/// D5 — báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.
/// Quản lý xem công nợ từng hóa đơn ở P6, không được dùng báo cáo tổng hợp này.
/// </summary>
public sealed class RevenueReportService(ApplicationDbContext db)
{
    public async Task<RevenueReportDto> GetAsync(
        RevenueReportFilter? filter,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsChuTro)
        {
            throw new RevenueReportAuthorizationException(
                "Báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.");
        }

        filter ??= new RevenueReportFilter();

        var ownedPropertyIds = db.Properties.AsNoTracking()
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id);

        var rows = await (
            from invoice in db.Invoices.AsNoTracking()
            join contract in db.Contracts.AsNoTracking()
                on invoice.ContractId equals contract.ContractId
            join room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                on contract.RoomId equals room.Id
            where ownedPropertyIds.Contains(room.PropertyId)
                && (filter.From == null || invoice.PeriodEnd >= filter.From)
                && (filter.To == null || invoice.PeriodStart <= filter.To)
            select new
            {
                InvoiceId = invoice.Id,
                invoice.TotalAmount,
                invoice.Status,
                RoomId = room.Id,
                RoomCode = room.Code,
            })
            .ToListAsync(cancellationToken);

        var invoiceIds = rows.Select(row => row.InvoiceId).ToList();
        var payments = await db.Set<Payment>().AsNoTracking()
            .Where(payment => invoiceIds.Contains(payment.InvoiceId)
                && payment.Status == PaymentStatus.Confirmed)
            .Select(payment => new { payment.InvoiceId, payment.Amount })
            .ToListAsync(cancellationToken);

        var paidByInvoice = payments
            .GroupBy(payment => payment.InvoiceId)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));

        var lines = rows
            .Select(row =>
            {
                var paid = paidByInvoice.GetValueOrDefault(row.InvoiceId);
                var outstanding = row.Status == InvoiceStatus.Issued
                    ? Math.Max(0m, row.TotalAmount - paid)
                    : 0m;
                return new RevenueReportLineDto(
                    row.InvoiceId, row.RoomId, row.RoomCode, row.TotalAmount, paid, outstanding, row.Status.ToString());
            })
            .ToList();

        return new RevenueReportDto(
            TotalRevenue: paidByInvoice.Values.Sum(),
            Outstanding: lines.Sum(line => line.Outstanding),
            InvoiceCount: lines.Count,
            PaidInvoiceCount: lines.Count(line => line.Outstanding == 0m && line.Paid > 0m),
            Lines: lines);
    }
}
