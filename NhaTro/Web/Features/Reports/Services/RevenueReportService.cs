using Microsoft.EntityFrameworkCore;
using Web.Common.Queries;
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

        var today = Today;
        var pageSize = PagingExtensions.ClampPageSize(filter.PageSize);
        var page = PagingExtensions.ClampPage(filter.Page);
        var confirmed = db.Set<Payment>().AsNoTracking()
            .Where(payment => payment.Status == PaymentStatus.Confirmed);
        var recognizedPayments = filter.DateBasis == RevenueDateBasis.PaymentDate
            ? confirmed.Where(payment => (fromUtc == null || payment.ReviewedAt >= fromUtc)
                && (toUtcExclusive == null || payment.ReviewedAt < toUtcExclusive))
            : confirmed;

        // Công nợ = hóa đơn Issued còn thiếu tiền thu đã xác nhận (đối chiếu theo từng hóa đơn).
        var debtSource = invoiceQuery.Where(row => row.Status == InvoiceStatus.Issued
            && row.TotalAmount > (confirmed
                .Where(payment => payment.InvoiceId == row.InvoiceId)
                .Sum(payment => (decimal?)payment.Amount) ?? 0m));
        var linesSource = filter.OutstandingOnly ? debtSource : invoiceQuery;

        var totalCount = await linesSource.CountAsync(cancellationToken);
        var orderedLines = linesSource
            .OrderBy(row => row.PropertyId)
            .ThenBy(row => row.RoomCode)
            .ThenBy(row => row.InvoiceId);
        var pageRows = await orderedLines
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Bảng công nợ: Page áp cho bảng công nợ khi filter.OutstandingOnly, ngược lại trả trang đầu.
        var debtPageSize = pageSize;
        var outstandingCount = filter.OutstandingOnly
            ? totalCount
            : await debtSource.CountAsync(cancellationToken);
        var orderedDebt = debtSource
            .OrderBy(row => row.PropertyId)
            .ThenBy(row => row.RoomCode)
            .ThenBy(row => row.InvoiceId);
        var debtRows = filter.OutstandingOnly && page == 1
            ? pageRows
            : filter.OutstandingOnly
                ? await orderedDebt.Skip((page - 1) * debtPageSize).Take(debtPageSize).ToListAsync(cancellationToken)
                : await orderedDebt.Take(debtPageSize).ToListAsync(cancellationToken);

        // Chỉ nạp tiền thu cho hóa đơn trên trang hiện tại (≤ 2 × PageSize id).
        var pageIds = pageRows.Select(row => row.InvoiceId)
            .Concat(debtRows.Select(row => row.InvoiceId))
            .Distinct()
            .ToList();
        var paidByInvoice = pageIds.Count == 0
            ? new Dictionary<int, decimal>()
            : await confirmed
                .Where(payment => pageIds.Contains(payment.InvoiceId))
                .GroupBy(payment => payment.InvoiceId)
                .Select(group => new { InvoiceId = group.Key, Amount = group.Sum(payment => payment.Amount) })
                .ToDictionaryAsync(item => item.InvoiceId, item => item.Amount, cancellationToken);
        var recognizedByInvoice = filter.DateBasis == RevenueDateBasis.InvoicePeriod || pageIds.Count == 0
            ? paidByInvoice
            : await recognizedPayments
                .Where(payment => pageIds.Contains(payment.InvoiceId))
                .GroupBy(payment => payment.InvoiceId)
                .Select(group => new { InvoiceId = group.Key, Amount = group.Sum(payment => payment.Amount) })
                .ToDictionaryAsync(item => item.InvoiceId, item => item.Amount, cancellationToken);

        var lines = pageRows.Select(row => ToLine(row.TotalAmount, paidByInvoice.GetValueOrDefault(row.InvoiceId),
            row.Status, row.InvoiceId, row.RoomId, row.RoomCode, row.PropertyId, row.PropertyName,
            row.PeriodStart, row.PeriodEnd, recognizedByInvoice.GetValueOrDefault(row.InvoiceId),
            row.DueDate, today)).ToList();
        var debtLines = debtRows.Select(row => ToLine(row.TotalAmount, paidByInvoice.GetValueOrDefault(row.InvoiceId),
            row.Status, row.InvoiceId, row.RoomId, row.RoomCode, row.PropertyId, row.PropertyName,
            row.PeriodStart, row.PeriodEnd, recognizedByInvoice.GetValueOrDefault(row.InvoiceId),
            row.DueDate, today)).ToList();

        // Số liệu/nhóm kỳ tính bằng GROUP BY ở SQL; client chỉ gộp các nhóm nhỏ (nhà × tháng).
        var monthRows = await invoiceQuery
            .GroupBy(row => new { row.PropertyId, row.PropertyName, row.PeriodStart.Year, row.PeriodStart.Month })
            .Select(group => new
            {
                group.Key.PropertyId,
                group.Key.PropertyName,
                group.Key.Year,
                group.Key.Month,
                InvoiceCount = group.Count(),
                PaidInvoiceCount = group.Count(row => row.Status == InvoiceStatus.Paid),
                TotalInvoiced = group.Sum(row => row.TotalAmount),
            })
            .ToListAsync(cancellationToken);

        var recognizedRows = await (
                from payment in recognizedPayments
                join row in invoiceQuery on payment.InvoiceId equals row.InvoiceId
                group payment by new { row.PropertyId, row.PropertyName, row.PeriodStart.Year, row.PeriodStart.Month }
                into grouped
                select new
                {
                    grouped.Key.PropertyId,
                    grouped.Key.PropertyName,
                    grouped.Key.Year,
                    grouped.Key.Month,
                    Amount = grouped.Sum(item => item.Amount),
                })
            .ToListAsync(cancellationToken);

        var outstandingRows = await invoiceQuery
            .Where(row => row.Status == InvoiceStatus.Issued)
            .Select(row => new
            {
                row.PropertyId,
                row.PropertyName,
                row.PeriodStart.Year,
                row.PeriodStart.Month,
                Amount = row.TotalAmount - (confirmed
                    .Where(payment => payment.InvoiceId == row.InvoiceId)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m),
            })
            .Where(item => item.Amount > 0m)
            .GroupBy(item => new { item.PropertyId, item.PropertyName, item.Year, item.Month })
            .Select(group => new
            {
                group.Key.PropertyId,
                group.Key.PropertyName,
                group.Key.Year,
                group.Key.Month,
                Amount = group.Sum(item => item.Amount),
            })
            .ToListAsync(cancellationToken);

        var overdue = await invoiceQuery
            .Where(row => row.Status == InvoiceStatus.Issued && row.DueDate != null && row.DueDate < today)
            .Select(row => new
            {
                row.TotalAmount,
                Paid = confirmed
                    .Where(payment => payment.InvoiceId == row.InvoiceId)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m,
            })
            .Where(item => item.TotalAmount > item.Paid)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Outstanding = group.Sum(item => item.TotalAmount - item.Paid),
            })
            .SingleOrDefaultAsync(cancellationToken);

        var periods = monthRows
            .GroupBy(row => (row.Year, row.Month))
            .OrderBy(group => group.Key)
            .Select(group => BuildPeriodGroup(
                group.Key.Year,
                group.Key.Month,
                group.Sum(row => row.TotalInvoiced),
                recognizedRows.Where(row => row.Year == group.Key.Year && row.Month == group.Key.Month)
                    .Sum(row => row.Amount),
                outstandingRows.Where(row => row.Year == group.Key.Year && row.Month == group.Key.Month)
                    .Sum(row => row.Amount),
                group.Sum(row => row.InvoiceCount),
                group.Sum(row => row.PaidInvoiceCount)))
            .ToList();

        var propertyGroups = monthRows
            .GroupBy(row => new { row.PropertyId, row.PropertyName })
            .OrderBy(group => group.Key.PropertyId)
            .Select(group => new RevenuePropertyGroupDto(
                group.Key.PropertyId,
                group.Key.PropertyName,
                group.Sum(row => row.TotalInvoiced),
                recognizedRows.Where(row => row.PropertyId == group.Key.PropertyId).Sum(row => row.Amount),
                outstandingRows.Where(row => row.PropertyId == group.Key.PropertyId).Sum(row => row.Amount),
                group.Sum(row => row.InvoiceCount),
                group.Sum(row => row.PaidInvoiceCount),
                group
                    .GroupBy(row => (row.Year, row.Month))
                    .OrderBy(month => month.Key)
                    .Select(month => BuildPeriodGroup(
                        month.Key.Year,
                        month.Key.Month,
                        month.Sum(row => row.TotalInvoiced),
                        recognizedRows.Where(row => row.PropertyId == group.Key.PropertyId
                            && row.Year == month.Key.Year && row.Month == month.Key.Month).Sum(row => row.Amount),
                        outstandingRows.Where(row => row.PropertyId == group.Key.PropertyId
                            && row.Year == month.Key.Year && row.Month == month.Key.Month).Sum(row => row.Amount),
                        month.Sum(row => row.InvoiceCount),
                        month.Sum(row => row.PaidInvoiceCount)))
                    .ToList()))
            .ToList();

        var totalRevenue = recognizedRows.Sum(row => row.Amount);
        var outstanding = outstandingRows.Sum(row => row.Amount);
        var invoiceCount = monthRows.Sum(row => row.InvoiceCount);
        var paidInvoiceCount = monthRows.Sum(row => row.PaidInvoiceCount);
        var totalInvoiced = monthRows.Sum(row => row.TotalInvoiced);

        var statusTotals = await (
                from payment in db.Set<Payment>().AsNoTracking()
                join row in invoiceQuery on payment.InvoiceId equals row.InvoiceId
                where payment.Status == PaymentStatus.Pending || payment.Status == PaymentStatus.Rejected
                group payment by payment.Status
                into grouped
                select new { Status = grouped.Key, Amount = grouped.Sum(payment => payment.Amount) })
            .ToDictionaryAsync(item => item.Status, item => item.Amount, cancellationToken);

        var pendingAmount = statusTotals.GetValueOrDefault(PaymentStatus.Pending);
        var rejectedAmount = statusTotals.GetValueOrDefault(PaymentStatus.Rejected);

        // VHT-141: cơ cấu giá trị hóa đơn theo loại phí, cùng tập hóa đơn hợp lệ.
        var feeAmounts = await (
                from item in db.InvoiceItems.AsNoTracking()
                join row in invoiceQuery on item.InvoiceId equals row.InvoiceId
                group item by item.ItemType
                into grouped
                select new { Type = grouped.Key, Amount = grouped.Sum(item => item.Amount) })
            .ToListAsync(cancellationToken);

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

        return new RevenueReportDto(
            TotalRevenue: totalRevenue,
            Outstanding: outstanding,
            InvoiceCount: invoiceCount,
            PaidInvoiceCount: paidInvoiceCount,
            Lines: lines,
            TotalInvoiced: totalInvoiced,
            PendingAmount: pendingAmount,
            RejectedAmount: rejectedAmount,
            DateBasis: filter.DateBasis,
            Periods: periods,
            Properties: propertyGroups,
            FeeBreakdown: feeBreakdown,
            OverdueOutstanding: overdue?.Outstanding ?? 0m,
            OverdueInvoiceCount: overdue?.Count ?? 0,
            TotalCount: totalCount,
            OutstandingCount: outstandingCount,
            Page: page,
            PageSize: pageSize,
            DebtLines: debtLines);
    }

    private static RevenueReportLineDto ToLine(
        decimal total,
        decimal paid,
        InvoiceStatus status,
        int invoiceId,
        int roomId,
        string roomCode,
        int propertyId,
        string propertyName,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal recognized,
        DateOnly? dueDate,
        DateOnly today)
    {
        var outstanding = status == InvoiceStatus.Issued ? Math.Max(0m, total - paid) : 0m;
        var overdueDays = dueDate is { } due
            && status == InvoiceStatus.Issued
            && outstanding > 0m
            && due < today
            ? today.DayNumber - due.DayNumber
            : 0;
        return new RevenueReportLineDto(
            invoiceId,
            roomId,
            roomCode,
            total,
            paid,
            outstanding,
            status.ToString(),
            propertyId,
            propertyName,
            periodStart,
            periodEnd,
            recognized,
            dueDate,
            overdueDays);
    }

    private static RevenuePeriodGroupDto BuildPeriodGroup(
        int year,
        int month,
        decimal totalInvoiced,
        decimal recognizedRevenue,
        decimal outstanding,
        int invoiceCount,
        int paidInvoiceCount)
    {
        var start = year == 1 ? default : new DateOnly(year, month, 1);
        var end = start == default ? default : start.AddMonths(1).AddDays(-1);
        var periodKey = start == default ? "unknown" : start.ToString("yyyy-MM");
        return new RevenuePeriodGroupDto(
            periodKey,
            start,
            end,
            totalInvoiced,
            recognizedRevenue,
            outstanding,
            invoiceCount,
            paidInvoiceCount);
    }
}
