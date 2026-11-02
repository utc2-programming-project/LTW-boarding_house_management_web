using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Rooms.Entities;

namespace Web.Features.Invoices.Services;

public sealed class InvoiceQueryService(ApplicationDbContext context) : IInvoiceQueryService
{
    private const string ForbiddenMessage =
        "Vai trò này không được xem hóa đơn ngoài phạm vi được giao.";

    public async Task<InvoiceListResult> GetInvoicesAsync(
        InvoiceFilter filter,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(caller);
        filter.Normalize();

        var query = ApplyFilter(ScopeInvoices(caller), filter);

        var summaryRow = await query
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalCount = group.Count(),
                DraftCount = group.Count(invoice => invoice.Status == InvoiceStatus.Draft),
                IssuedCount = group.Count(invoice => invoice.Status == InvoiceStatus.Issued),
                PaidCount = group.Count(invoice => invoice.Status == InvoiceStatus.Paid),
                OutstandingAmount = group.Sum(invoice =>
                    invoice.Status == InvoiceStatus.Paid ? 0m : invoice.TotalAmount),
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalCount = summaryRow?.TotalCount ?? 0;
        var skip = (long)(filter.Page - 1) * filter.PageSize;

        var rows = await query
            .AsNoTracking()
            .OrderByDescending(invoice => invoice.PeriodStart)
            .ThenByDescending(invoice => invoice.Id)
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .Select(invoice => new InvoiceRow
            {
                Invoice = invoice,
                ContractNumber = context.Contracts
                    .Where(contract => contract.ContractId == invoice.ContractId)
                    .Select(contract => contract.ContractNumber)
                    .First(),
                RoomCode = (from contract in context.Contracts
                            join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                            where contract.ContractId == invoice.ContractId
                            select room.Code).First(),
                PropertyName = (from contract in context.Contracts
                                join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                                join property in context.Properties.IgnoreQueryFilters()
                                    on room.PropertyId equals property.Id
                                where contract.ContractId == invoice.ContractId
                                select property.Name).First(),
            })
            .ToListAsync(cancellationToken);

        var tenantNames = await LoadTenantNamesAsync(
            rows.Select(row => row.Invoice.ContractId).Distinct().ToArray(),
            cancellationToken);

        var items = rows.Select(row =>
        {
            var invoice = row.Invoice;
            var paidAmount = invoice.Status == InvoiceStatus.Paid ? invoice.TotalAmount : 0m;
            return new InvoiceListItemDto
            {
                Id = invoice.Id,
                ContractId = invoice.ContractId,
                ContractNumber = row.ContractNumber,
                RoomCode = row.RoomCode,
                PropertyName = row.PropertyName,
                TenantNames = tenantNames.GetValueOrDefault(invoice.ContractId, []),
                PeriodStart = invoice.PeriodStart,
                PeriodEnd = invoice.PeriodEnd,
                Status = invoice.Status,
                TotalAmount = invoice.TotalAmount,
                PaidAmount = paidAmount,
                OutstandingAmount = Math.Max(0m, invoice.TotalAmount - paidAmount),
            };
        }).ToArray();

        return new InvoiceListResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize,
            Summary = new InvoiceSummaryDto
            {
                DraftCount = summaryRow?.DraftCount ?? 0,
                IssuedCount = summaryRow?.IssuedCount ?? 0,
                PaidCount = summaryRow?.PaidCount ?? 0,
                OutstandingAmount = summaryRow?.OutstandingAmount ?? 0m,
            },
        };
    }

    public async Task<InvoiceDto> GetInvoiceAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var invoice = await ScopeInvoices(caller)
            .AsNoTracking()
            .Include(item => item.Items)
            .SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);

        // Không phân biệt "không tồn tại" và "ngoài phạm vi" để không lộ ID hóa đơn của người khác.
        if (invoice is null)
        {
            throw new KeyNotFoundException("Không tìm thấy hóa đơn hoặc bạn không có quyền xem.");
        }

        var contract = await context.Contracts
            .AsNoTracking()
            .Where(item => item.ContractId == invoice.ContractId)
            .Select(item => new { item.ContractNumber, item.RoomId })
            .SingleAsync(cancellationToken);

        var room = await context.Rooms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Id == contract.RoomId)
            .Select(item => new { item.Code, item.PropertyId })
            .SingleAsync(cancellationToken);

        var propertyName = await context.Properties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(item => item.Id == room.PropertyId)
            .Select(item => item.Name)
            .SingleAsync(cancellationToken);

        var tenantNames = await LoadTenantNamesAsync([invoice.ContractId], cancellationToken);
        var paidAmount = invoice.Status == InvoiceStatus.Paid ? invoice.TotalAmount : 0m;

        return new InvoiceDto
        {
            Id = invoice.Id,
            ContractId = invoice.ContractId,
            ContractNumber = contract.ContractNumber,
            RoomCode = room.Code,
            PropertyName = propertyName,
            TenantNames = tenantNames.GetValueOrDefault(invoice.ContractId, []),
            CreatedAt = invoice.CreatedAt,
            IssuedAt = invoice.IssuedAt,
            PeriodStart = invoice.PeriodStart,
            PeriodEnd = invoice.PeriodEnd,
            Status = invoice.Status,
            Items = invoice.Items
                .OrderBy(item => item.DisplayOrder)
                .ThenBy(item => item.Id)
                .Select(item => new InvoiceItemDto
                {
                    ItemType = item.ItemType,
                    Description = item.Description,
                    Unit = item.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount,
                })
                .ToArray(),
            TotalAmount = invoice.TotalAmount,
            PaidAmount = paidAmount,
            OutstandingAmount = Math.Max(0m, invoice.TotalAmount - paidAmount),
            LatestPayment = null,
        };
    }

    public async Task<IReadOnlyList<InvoiceContractOptionDto>> GetContractOptionsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var query = ScopeContracts(caller);
        return await query
            .AsNoTracking()
            .OrderBy(item => item.ContractNumber)
            .Select(item => new InvoiceContractOptionDto(
                item.ContractId,
                item.ContractNumber,
                (from contract in context.Contracts
                 join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                 where contract.ContractId == item.ContractId
                 select room.Code).First(),
                (from contract in context.Contracts
                 join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                 join property in context.Properties.IgnoreQueryFilters()
                     on room.PropertyId equals property.Id
                 where contract.ContractId == item.ContractId
                 select property.Name).First()))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Invoice> ScopeInvoices(PropertyCaller caller)
    {
        var contractIds = ScopeContracts(caller).Select(contract => contract.ContractId);
        return context.Invoices.Where(invoice => contractIds.Contains(invoice.ContractId));
    }

    /// <summary>
    /// Hợp đồng người gọi được xem, dùng chung cho danh sách hóa đơn và bộ lọc.
    /// Bỏ query filter xóa mềm của phòng/nhà để hóa đơn lịch sử vẫn đọc được.
    /// </summary>
    private IQueryable<Contract> ScopeContracts(PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return from contract in context.Contracts
                   join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   join property in context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                   where property.OwnerId == caller.UserId
                   select contract;
        }

        if (caller.IsQuanLy)
        {
            var propertyIds = context.PropertyManagers
                .Where(assignment => assignment.ManagerUserId == caller.UserId && assignment.RevokedAt == null)
                .Select(assignment => assignment.PropertyId);

            return from contract in context.Contracts
                   join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   where propertyIds.Contains(room.PropertyId)
                   select contract;
        }

        if (caller.IsKhachThue)
        {
            var contractIds = from member in context.ContractMembers
                              join tenant in context.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
                              where tenant.UserId == caller.UserId
                              select member.ContractId;

            return context.Contracts.Where(contract => contractIds.Contains(contract.ContractId));
        }

        throw new UnauthorizedAccessException(ForbiddenMessage);
    }

    private static IQueryable<Invoice> ApplyFilter(IQueryable<Invoice> query, InvoiceFilter filter)
    {
        if (filter.PeriodRange() is { } period)
        {
            query = query.Where(invoice =>
                invoice.PeriodStart <= period.End && invoice.PeriodEnd >= period.Start);
        }

        if (filter.ContractId is { } contractId)
        {
            query = query.Where(invoice => invoice.ContractId == contractId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(invoice => invoice.Status == status);
        }

        return query;
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

    private sealed class InvoiceRow
    {
        public required Invoice Invoice { get; init; }

        public required string ContractNumber { get; init; }

        public required string RoomCode { get; init; }

        public required string PropertyName { get; init; }
    }
}
