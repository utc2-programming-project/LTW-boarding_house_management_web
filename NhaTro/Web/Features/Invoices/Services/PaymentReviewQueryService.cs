using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Mapping;
using Web.Features.Properties;
using Web.Features.Rooms.Entities;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Đọc hàng đợi yêu cầu thanh toán P6 theo phạm vi Quản lý/Chủ trọ. Service chỉ đọc:
/// thao tác xác nhận/từ chối vẫn đi qua <see cref="IPaymentService"/> để giữ transaction và audit.
/// </summary>
public sealed class PaymentReviewQueryService(ApplicationDbContext context) : IPaymentReviewQueryService
{
    private const string ForbiddenMessage =
        "Vai trò này không được xem yêu cầu thanh toán ngoài phạm vi được giao.";

    public async Task<PaymentReviewListResult> GetRequestsAsync(
        PaymentReviewFilter filter,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(caller);
        EnsureManager(caller);
        filter.Normalize();

        var scoped = ScopePayments(caller);
        var summaryRow = await scoped
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                PendingCount = group.Count(payment => payment.Status == PaymentStatus.Pending),
                ConfirmedCount = group.Count(payment => payment.Status == PaymentStatus.Confirmed),
                RejectedCount = group.Count(payment => payment.Status == PaymentStatus.Rejected),
                PendingAmount = group
                    .Where(payment => payment.Status == PaymentStatus.Pending)
                    .Sum(payment => (decimal?)payment.Amount) ?? 0m,
            })
            .SingleOrDefaultAsync(cancellationToken);

        var query = scoped.AsNoTracking();
        if (filter.Status is { } status)
        {
            query = query.Where(payment => payment.Status == status);
        }

        if (filter.PeriodRange() is { } period)
        {
            var invoiceIdsInPeriod = context.Invoices
                .Where(invoice => invoice.PeriodStart <= period.End && invoice.PeriodEnd >= period.Start)
                .Select(invoice => invoice.Id);
            query = query.Where(payment => invoiceIdsInPeriod.Contains(payment.InvoiceId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var term = filter.Keyword!;
            var paymentId = ParsePrefixedNumber(term, "YC");
            var invoiceId = ParsePrefixedNumber(term, "HD");
            var matchingInvoices =
                from invoice in context.Invoices
                join contract in context.Contracts on invoice.ContractId equals contract.ContractId
                join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                join property in context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                where contract.ContractNumber.Contains(term)
                    || room.Code.Contains(term)
                    || property.Name.Contains(term)
                    || context.ContractMembers.Any(member => member.ContractId == contract.ContractId
                        && context.Tenants.IgnoreQueryFilters()
                            .Any(tenant => tenant.Id == member.TenantId && tenant.FullName.Contains(term)))
                select invoice.Id;

            query = (paymentId, invoiceId) switch
            {
                ({ } pid, { } iid) => query.Where(payment =>
                    payment.Id == pid || payment.InvoiceId == iid || matchingInvoices.Contains(payment.InvoiceId)),
                ({ } pid, null) => query.Where(payment =>
                    payment.Id == pid || matchingInvoices.Contains(payment.InvoiceId)),
                (null, { } iid) => query.Where(payment =>
                    payment.InvoiceId == iid || matchingInvoices.Contains(payment.InvoiceId)),
                _ => query.Where(payment => matchingInvoices.Contains(payment.InvoiceId)),
            };
        }

        if (filter.MinAmount is { } minAmount)
        {
            query = query.Where(payment => payment.Amount >= minAmount);
        }

        if (filter.MaxAmount is { } maxAmount)
        {
            query = query.Where(payment => payment.Amount <= maxAmount);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var skip = (long)(filter.Page - 1) * filter.PageSize;

        var payments = await query
            .Include(payment => payment.Proofs)
            .OrderByDescending(payment => payment.RequestedAt)
            .ThenByDescending(payment => payment.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var items = await BuildItemsAsync(payments, cancellationToken);

        return new PaymentReviewListResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize,
            Summary = new PaymentReviewSummaryDto
            {
                PendingCount = summaryRow?.PendingCount ?? 0,
                ConfirmedCount = summaryRow?.ConfirmedCount ?? 0,
                RejectedCount = summaryRow?.RejectedCount ?? 0,
                PendingAmount = summaryRow?.PendingAmount ?? 0m,
            },
        };
    }

    public async Task<PaymentReviewItemDto> GetRequestAsync(
        int paymentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        EnsureManager(caller);

        var payment = await ScopePayments(caller)
            .AsNoTracking()
            .Include(item => item.Proofs)
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            throw new KeyNotFoundException("Không tìm thấy yêu cầu thanh toán hoặc bạn không có quyền xem.");
        }

        var items = await BuildItemsAsync([payment], cancellationToken);
        return items[0];
    }

    private async Task<IReadOnlyList<PaymentReviewItemDto>> BuildItemsAsync(
        IReadOnlyList<Payment> payments,
        CancellationToken cancellationToken)
    {
        if (payments.Count == 0)
        {
            return [];
        }

        var invoiceIds = payments.Select(payment => payment.InvoiceId).Distinct().ToArray();
        var invoices = await context.Invoices
            .AsNoTracking()
            .Where(invoice => invoiceIds.Contains(invoice.Id))
            .Select(invoice => new
            {
                invoice.Id,
                invoice.ContractId,
                invoice.PeriodStart,
                invoice.PeriodEnd,
                invoice.Status,
                invoice.TotalAmount,
            })
            .ToDictionaryAsync(invoice => invoice.Id, cancellationToken);

        var contractIds = invoices.Values.Select(invoice => invoice.ContractId).Distinct().ToArray();
        var contracts = await context.Contracts
            .AsNoTracking()
            .Where(contract => contractIds.Contains(contract.ContractId))
            .Select(contract => new { contract.ContractId, contract.ContractNumber, contract.RoomId })
            .ToDictionaryAsync(contract => contract.ContractId, cancellationToken);

        var roomIds = contracts.Values.Select(contract => contract.RoomId).Distinct().ToArray();
        var rooms = await context.Rooms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(room => roomIds.Contains(room.Id))
            .Select(room => new { room.Id, room.Code, room.PropertyId })
            .ToDictionaryAsync(room => room.Id, cancellationToken);

        var propertyIds = rooms.Values.Select(room => room.PropertyId).Distinct().ToArray();
        var properties = await context.Properties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(property => propertyIds.Contains(property.Id))
            .Select(property => new { property.Id, property.Name })
            .ToDictionaryAsync(property => property.Id, cancellationToken);

        var tenantNames = await LoadTenantNamesAsync(contractIds, cancellationToken);

        return payments.Select(payment =>
        {
            var invoice = invoices[payment.InvoiceId];
            var contract = contracts[invoice.ContractId];
            var room = rooms[contract.RoomId];
            var property = properties[room.PropertyId];

            return new PaymentReviewItemDto
            {
                Payment = PaymentMapping.ToDto(payment),
                InvoiceId = invoice.Id,
                ContractNumber = contract.ContractNumber,
                RoomCode = room.Code,
                PropertyName = property.Name,
                TenantNames = tenantNames.GetValueOrDefault(contract.ContractId, []),
                PeriodStart = invoice.PeriodStart,
                PeriodEnd = invoice.PeriodEnd,
                InvoiceStatus = invoice.Status,
                InvoiceTotal = invoice.TotalAmount,
            };
        }).ToArray();
    }

    private IQueryable<Payment> ScopePayments(PropertyCaller caller)
    {
        var invoiceIds = ScopeInvoices(caller).Select(invoice => invoice.Id);
        return context.Payments.Where(payment => invoiceIds.Contains(payment.InvoiceId));
    }

    /// <summary>Phạm vi hóa đơn giống P6: Chủ trọ theo nhà sở hữu, Quản lý theo nhà được giao.</summary>
    private IQueryable<Invoice> ScopeInvoices(PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return from invoice in context.Invoices
                   join contract in context.Contracts on invoice.ContractId equals contract.ContractId
                   join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   join property in context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                   where property.OwnerId == caller.UserId
                   select invoice;
        }

        if (caller.IsQuanLy)
        {
            var propertyIds = context.PropertyManagers
                .Where(assignment => assignment.ManagerUserId == caller.UserId && assignment.RevokedAt == null)
                .Select(assignment => assignment.PropertyId);

            return from invoice in context.Invoices
                   join contract in context.Contracts on invoice.ContractId equals contract.ContractId
                   join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   where propertyIds.Contains(room.PropertyId)
                   select invoice;
        }

        throw new UnauthorizedAccessException(ForbiddenMessage);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<string>>> LoadTenantNamesAsync(
        Guid[] contractIds,
        CancellationToken cancellationToken)
    {
        if (contractIds.Length == 0)
        {
            return [];
        }

        var rows = await (from member in context.ContractMembers.AsNoTracking()
                          join tenant in context.Tenants.IgnoreQueryFilters().AsNoTracking()
                              on member.TenantId equals tenant.Id
                          where contractIds.Contains(member.ContractId)
                          orderby tenant.FullName, tenant.Id
                          select new { member.ContractId, tenant.FullName })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.ContractId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(row => row.FullName).ToArray());
    }

    /// <summary>Đọc mã dạng "YC-12"/"HD-12" hoặc "12"; trả null khi từ khóa không phải mã số.</summary>
    private static int? ParsePrefixedNumber(string term, string prefix)
    {
        var normalized = term.Trim();
        if (normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[(prefix.Length + 1)..];
        }

        return int.TryParse(normalized, out var value) ? value : null;
    }

    private static void EnsureManager(PropertyCaller caller)
    {
        if (!caller.IsManager)
        {
            throw new UnauthorizedAccessException(ForbiddenMessage);
        }
    }
}
