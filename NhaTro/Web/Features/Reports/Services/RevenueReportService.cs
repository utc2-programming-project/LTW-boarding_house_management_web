using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Infrastructure.Time;

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
/// VHT-57 — nguồn doanh thu/công nợ P8.
/// D5: báo cáo tổng hợp chỉ dành cho Chủ trọ; Quản lý xem công nợ từng hóa đơn ở P6
/// ("Xem hóa đơn"), Admin và Khách thuê bị chặn.
/// Chỉ <see cref="PaymentStatus.Confirmed"/> mới là tiền thu; yêu cầu chờ/từ chối không
/// bao giờ được cộng vào doanh thu. Công nợ = hóa đơn hợp lệ (Issued/Paid) trừ tiền đã
/// xác nhận, và chỉ hóa đơn <see cref="InvoiceStatus.Issued"/> mới còn công nợ.
/// </summary>
public sealed class RevenueReportService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider? time = null)
{
    private DateOnly Today => DateOnly.FromDateTime(
        VietnamTime.ToVietnam((time ?? TimeProvider.System).GetUtcNow().UtcDateTime));

    /// <summary>
    /// Bản gọn cho dashboard/Home: chỉ aggregate KPI + nhóm kỳ, không dựng toàn bộ
    /// danh sách dòng và dòng phí. Bản đầy đủ vẫn dùng <see cref="GetAsync"/>.
    /// </summary>
    public async Task<RevenueReportDto> GetSummaryAsync(
        RevenueReportFilter? filter,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        filter ??= new RevenueReportFilter();
        if (filter.DateBasis != RevenueDateBasis.InvoicePeriod)
        {
            return await GetAsync(filter, caller, cancellationToken);
        }

        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro)
        {
            throw new RevenueReportAuthorizationException(
                "Báo cáo doanh thu/công nợ tổng hợp chỉ dành cho Chủ trọ.");
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var ownedPropertyIds = db.Properties.AsNoTracking()
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id);

        var invoiceQuery =
            from invoice in db.Invoices.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on invoice.ContractId equals contract.ContractId
            join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
            join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            where ownedPropertyIds.Contains(room.PropertyId)
                && (filter.PropertyId == null || room.PropertyId == filter.PropertyId)
                && invoice.Status != InvoiceStatus.Draft
                && (filter.From == null || invoice.PeriodEnd >= filter.From)
                && (filter.To == null || invoice.PeriodStart <= filter.To)
            select new
            {
                invoice.Id,
                invoice.TotalAmount,
                invoice.Status,
                invoice.PeriodStart,
                invoice.PeriodEnd,
                invoice.DueDate,
            };

        var invoiced = await invoiceQuery
            .GroupBy(row => new { row.PeriodStart.Year, row.PeriodStart.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                TotalInvoiced = group.Sum(row => row.TotalAmount),
                InvoiceCount = group.Count(),
                PaidCount = group.Count(row => row.Status == InvoiceStatus.Paid),
                IssuedTotal = group.Sum(row => row.Status == InvoiceStatus.Issued ? row.TotalAmount : 0m),
            })
            .ToListAsync(cancellationToken);

        var paid = await (
                from payment in db.Set<Payment>().AsNoTracking()
                join row in invoiceQuery on payment.InvoiceId equals row.Id
                where payment.Status == PaymentStatus.Confirmed
                group payment by new { row.PeriodStart.Year, row.PeriodStart.Month }
                into grouped
                select new { grouped.Key.Year, grouped.Key.Month, Amount = grouped.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);

        var paidIssued = await (
                from payment in db.Set<Payment>().AsNoTracking()
                join row in invoiceQuery on payment.InvoiceId equals row.Id
                where payment.Status == PaymentStatus.Confirmed && row.Status == InvoiceStatus.Issued
                group payment by new { row.PeriodStart.Year, row.PeriodStart.Month }
                into grouped
                select new { grouped.Key.Year, grouped.Key.Month, Amount = grouped.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);

        var paidByMonth = paid.ToDictionary(row => (row.Year, row.Month), row => row.Amount);
        var paidIssuedByMonth = paidIssued.ToDictionary(row => (row.Year, row.Month), row => row.Amount);
        var months = invoiced.Select(row => (row.Year, row.Month))
            .Union(paidByMonth.Keys)
            .OrderBy(key => key.Year).ThenBy(key => key.Month)
            .ToList();

        var periods = months.Select(key =>
        {
            var periodStart = new DateOnly(key.Year, key.Month, 1);
            var periodEnd = periodStart.AddMonths(1).AddDays(-1);
            var invoicedRow = invoiced.FirstOrDefault(row => row.Year == key.Year && row.Month == key.Month);
            var recognized = paidByMonth.GetValueOrDefault(key);
            var outstanding = Math.Max(0m,
                (invoicedRow?.IssuedTotal ?? 0m) - paidIssuedByMonth.GetValueOrDefault(key));
            return new RevenuePeriodGroupDto(
                periodStart.ToString("yyyy-MM"),
                periodStart,
                periodEnd,
                invoicedRow?.TotalInvoiced ?? 0m,
                recognized,
                outstanding,
                invoicedRow?.InvoiceCount ?? 0,
                invoicedRow?.PaidCount ?? 0);
        }).ToList();

        return new RevenueReportDto(
            TotalRevenue: periods.Sum(period => period.RecognizedRevenue),
            Outstanding: periods.Sum(period => period.Outstanding),
            InvoiceCount: periods.Sum(period => period.InvoiceCount),
            PaidInvoiceCount: periods.Sum(period => period.PaidInvoiceCount),
            Lines: [],
            TotalInvoiced: periods.Sum(period => period.TotalInvoiced),
            DateBasis: RevenueDateBasis.InvoicePeriod,
            Periods: periods,
            Properties: [],
            FeeBreakdown: []);
    }

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

        // Mỗi lần đọc dùng context riêng: Dashboard/Home có thể chạy nền trong khi
        // người dùng mở modal hợp đồng trên cùng circuit Blazor.
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Biên ngày thu theo giờ Việt Nam nhưng so sánh trên ReviewedAt lưu UTC.
        var fromUtc = filter.From is { } fromDate
            ? VietnamTime.DayRangeUtc(fromDate).StartUtc
            : (DateTime?)null;
        var toUtcExclusive = filter.To is { } toDate
            ? VietnamTime.DayRangeUtc(toDate).EndUtc
            : (DateTime?)null;

        var ownedPropertyIds = db.Properties.AsNoTracking()
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id);

        var invoiceQuery =
            from invoice in db.Invoices.AsNoTracking()
            join contract in db.Contracts.AsNoTracking()
                on invoice.ContractId equals contract.ContractId
            join room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                on contract.RoomId equals room.Id
            join property in db.Properties.AsNoTracking()
                on room.PropertyId equals property.Id
            where ownedPropertyIds.Contains(room.PropertyId)
                && (filter.PropertyId == null || room.PropertyId == filter.PropertyId)
                // Hóa đơn hợp lệ: đã phát hành hoặc đã thu; bản nháp không vào báo cáo.
                && invoice.Status != InvoiceStatus.Draft
            select new
            {
                InvoiceId = invoice.Id,
                invoice.TotalAmount,
                invoice.Status,
                invoice.PeriodStart,
                invoice.PeriodEnd,
                invoice.DueDate,
                RoomId = room.Id,
                RoomCode = room.Code,
                PropertyId = property.Id,
                PropertyName = property.Name,
            };

        if (filter.DateBasis == RevenueDateBasis.InvoicePeriod)
        {
            // Mặc định VHT-57: chọn hóa đơn có kỳ giao với khoảng lọc.
            invoiceQuery = invoiceQuery.Where(row =>
                (filter.From == null || row.PeriodEnd >= filter.From)
                && (filter.To == null || row.PeriodStart <= filter.To));
        }
        else
        {
            // Theo ngày thu: giữ hóa đơn trong kỳ (để có công nợ) và bổ sung hóa đơn
            // có khoản đã xác nhận trong khoảng ngày thu.
            invoiceQuery = invoiceQuery.Where(row =>
                ((filter.From == null || row.PeriodEnd >= filter.From)
                    && (filter.To == null || row.PeriodStart <= filter.To))
                || db.Payments.Any(payment =>
                    payment.InvoiceId == row.InvoiceId
                    && payment.Status == PaymentStatus.Confirmed
                    && (fromUtc == null || payment.ReviewedAt >= fromUtc)
                    && (toUtcExclusive == null || payment.ReviewedAt < toUtcExclusive)));
        }

        var rows = await invoiceQuery
            .OrderBy(row => row.PropertyId)
            .ThenBy(row => row.RoomCode)
            .ThenBy(row => row.InvoiceId)
            .ToListAsync(cancellationToken);

        var invoiceIds = rows.Select(row => row.InvoiceId).ToList();
        var payments = await db.Set<Payment>().AsNoTracking()
            .Where(payment => invoiceIds.Contains(payment.InvoiceId))
            .Select(payment => new
            {
                payment.InvoiceId,
                payment.Amount,
                payment.Status,
                payment.ReviewedAt,
            })
            .ToListAsync(cancellationToken);

        var confirmedByInvoice = payments
            .Where(payment => payment.Status == PaymentStatus.Confirmed)
            .GroupBy(payment => payment.InvoiceId)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));

        var recognizedByInvoice = filter.DateBasis == RevenueDateBasis.InvoicePeriod
            ? confirmedByInvoice
            : payments
                .Where(payment => payment.Status == PaymentStatus.Confirmed
                    && (fromUtc == null || payment.ReviewedAt >= fromUtc)
                    && (toUtcExclusive == null || payment.ReviewedAt < toUtcExclusive))
                .GroupBy(payment => payment.InvoiceId)
                .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));

        var pendingAmount = payments
            .Where(payment => payment.Status == PaymentStatus.Pending)
            .Sum(payment => payment.Amount);
        var rejectedAmount = payments
            .Where(payment => payment.Status == PaymentStatus.Rejected)
            .Sum(payment => payment.Amount);

        // VHT-141: cơ cấu giá trị hóa đơn theo loại phí, cùng tập hóa đơn hợp lệ.
        var feeAmounts = await db.InvoiceItems.AsNoTracking()
            .Where(item => invoiceIds.Contains(item.InvoiceId))
            .GroupBy(item => item.ItemType)
            .Select(group => new { Type = group.Key, Amount = group.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);

        var today = Today;
        var lines = rows
            .Select(row =>
            {
                var paid = confirmedByInvoice.GetValueOrDefault(row.InvoiceId);
                var recognized = recognizedByInvoice.GetValueOrDefault(row.InvoiceId);
                var outstanding = row.Status == InvoiceStatus.Issued
                    ? Math.Max(0m, row.TotalAmount - paid)
                    : 0m;
                var overdueDays = row.DueDate is { } dueDate
                    && row.Status == InvoiceStatus.Issued
                    && outstanding > 0m
                    && dueDate < today
                    ? today.DayNumber - dueDate.DayNumber
                    : 0;
                return new RevenueReportLineDto(
                    row.InvoiceId,
                    row.RoomId,
                    row.RoomCode,
                    row.TotalAmount,
                    paid,
                    outstanding,
                    row.Status.ToString(),
                    row.PropertyId,
                    row.PropertyName,
                    row.PeriodStart,
                    row.PeriodEnd,
                    recognized,
                    row.DueDate,
                    overdueDays);
            })
            .ToList();

        var feeBreakdown = new[]
            {
                InvoiceItemType.Rent,
                InvoiceItemType.Electric,
                InvoiceItemType.Water,
                InvoiceItemType.Service,
                InvoiceItemType.Discount,
            }
            .Select(type => new RevenueFeeTypeDto(
                type.ToString(),
                feeAmounts.FirstOrDefault(item => item.Type == type)?.Amount ?? 0m))
            .Where(fee => fee.Amount != 0m)
            .ToList();

        var propertyGroups = lines
            .GroupBy(line => new { line.PropertyId, line.PropertyName })
            .OrderBy(group => group.Key.PropertyId)
            .Select(group =>
            {
                var periodGroups = BuildPeriodGroups(group);
                return new RevenuePropertyGroupDto(
                    group.Key.PropertyId,
                    group.Key.PropertyName,
                    group.Sum(line => line.Total),
                    group.Sum(line => line.RecognizedRevenue),
                    group.Sum(line => line.Outstanding),
                    group.Count(),
                    group.Count(line => line.Status == nameof(InvoiceStatus.Paid)),
                    periodGroups);
            })
            .ToList();

        // Nhóm kỳ toàn phạm vi: cộng gộp các nhóm cùng kỳ của mọi nhà.
        var periodGroupsAll = lines
            .GroupBy(line => PeriodStartOf(line))
            .OrderBy(group => group.Key)
            .Select(group => BuildPeriodGroup(group.Key, group))
            .ToList();

        return new RevenueReportDto(
            TotalRevenue: lines.Sum(line => line.RecognizedRevenue),
            Outstanding: lines.Sum(line => line.Outstanding),
            InvoiceCount: lines.Count,
            PaidInvoiceCount: lines.Count(line => line.Status == nameof(InvoiceStatus.Paid)),
            Lines: lines,
            TotalInvoiced: lines.Sum(line => line.Total),
            PendingAmount: pendingAmount,
            RejectedAmount: rejectedAmount,
            DateBasis: filter.DateBasis,
            Periods: periodGroupsAll,
            Properties: propertyGroups,
            FeeBreakdown: feeBreakdown,
            OverdueOutstanding: lines.Sum(line => line.OverdueDays > 0 ? line.Outstanding : 0m),
            OverdueInvoiceCount: lines.Count(line => line.OverdueDays > 0));
    }

    private static IReadOnlyList<RevenuePeriodGroupDto> BuildPeriodGroups(
        IEnumerable<RevenueReportLineDto> lines) =>
        lines
            .GroupBy(line => PeriodStartOf(line))
            .OrderBy(group => group.Key)
            .Select(group => BuildPeriodGroup(group.Key, group))
            .ToList();

    private static DateOnly PeriodStartOf(RevenueReportLineDto line) =>
        line.PeriodStart == default
            ? default
            : new DateOnly(line.PeriodStart.Year, line.PeriodStart.Month, 1);

    private static RevenuePeriodGroupDto BuildPeriodGroup(
        DateOnly key,
        IEnumerable<RevenueReportLineDto> lines)
    {
        var materialized = lines.ToList();
        var start = key;
        var end = key == default ? default : start.AddMonths(1).AddDays(-1);
        var periodKey = key == default ? "unknown" : key.ToString("yyyy-MM");
        return new RevenuePeriodGroupDto(
            periodKey,
            start,
            end,
            materialized.Sum(line => line.Total),
            materialized.Sum(line => line.RecognizedRevenue),
            materialized.Sum(line => line.Outstanding),
            materialized.Count,
            materialized.Count(line => line.Status == nameof(InvoiceStatus.Paid)));
    }
}
