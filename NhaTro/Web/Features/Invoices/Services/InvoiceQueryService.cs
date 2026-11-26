using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Accounts.Owners;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Mapping;
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

        // Thẻ KPI trên trang vừa là nút lọc: số trên thẻ phải giữ nguyên khi bấm sang trạng thái
        // khác, nên phần tổng hợp bỏ qua đúng hai bộ lọc trạng thái/thanh toán do các thẻ điều khiển.
        var summaryRow = await ApplyFilter(ScopeInvoices(caller), new InvoiceFilter
            {
                Keyword = filter.Keyword,
                Year = filter.Year,
                Month = filter.Month,
                ContractId = filter.ContractId,
                MinAmount = filter.MinAmount,
                MaxAmount = filter.MaxAmount,
            })
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                DraftCount = group.Count(invoice => invoice.Status == InvoiceStatus.Draft),
                IssuedCount = group.Count(invoice => invoice.Status == InvoiceStatus.Issued),
                PaidCount = group.Count(invoice => invoice.Status == InvoiceStatus.Paid),
                OutstandingAmount = group.Sum(invoice =>
                    invoice.Status == InvoiceStatus.Issued ? invoice.TotalAmount : 0m),
            })
            .SingleOrDefaultAsync(cancellationToken);

        // Tổng số dòng phải theo đúng bộ lọc của danh sách, tách khỏi summary vì summary bỏ lọc trạng thái.
        var totalCount = await query.AsNoTracking().CountAsync(cancellationToken);
        var skip = (long)(filter.Page - 1) * filter.PageSize;

        var rows = await (
                from invoice in query
                join contract in context.Contracts.AsNoTracking() on invoice.ContractId equals contract.ContractId
                join room in context.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
                join property in context.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
                orderby invoice.PeriodStart descending, invoice.Id descending
                select new InvoiceRow
                {
                    Invoice = invoice,
                    ContractNumber = contract.ContractNumber,
                    RoomCode = room.Code,
                    PropertyName = property.Name,
                })
            .Skip((int)Math.Min(skip, int.MaxValue))
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        var tenantNames = await LoadTenantNamesAsync(
            rows.Select(row => row.Invoice.ContractId).Distinct().ToArray(),
            cancellationToken);

        // Chỉ Payment Confirmed mới được cộng vào tiền đã thu; Pending/Rejected không tính.
        var invoiceIds = rows.Select(row => row.Invoice.Id).ToArray();
        var paidByInvoice = await context.Payments
            .AsNoTracking()
            .Where(payment => invoiceIds.Contains(payment.InvoiceId)
                && payment.Status == PaymentStatus.Confirmed)
            .GroupBy(payment => payment.InvoiceId)
            .Select(group => new { InvoiceId = group.Key, Amount = group.Sum(payment => payment.Amount) })
            .ToDictionaryAsync(row => row.InvoiceId, row => row.Amount, cancellationToken);

        var items = rows.Select(row =>
        {
            var invoice = row.Invoice;
            var paidAmount = paidByInvoice.GetValueOrDefault(invoice.Id, 0m);
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
                DueDate = invoice.DueDate,
                Status = invoice.Status,
                TotalAmount = invoice.TotalAmount,
                PaidAmount = paidAmount,
                OutstandingAmount = invoice.Status == InvoiceStatus.Issued
                    ? Math.Max(0m, invoice.TotalAmount - paidAmount)
                    : 0m,
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

        // Một lượt join thay vì 3 truy vấn đơn dòng nối tiếp (hợp đồng → phòng → tòa).
        var header = await context.Contracts
            .AsNoTracking()
            .Where(item => item.ContractId == invoice.ContractId)
            .Join(
                context.Rooms.IgnoreQueryFilters(),
                contract => contract.RoomId,
                room => room.Id,
                (contract, room) => new { contract.ContractNumber, RoomCode = room.Code, room.PropertyId })
            .Join(
                context.Properties.IgnoreQueryFilters(),
                row => row.PropertyId,
                property => property.Id,
                (row, property) => new { row.ContractNumber, row.RoomCode, PropertyName = property.Name })
            .SingleAsync(cancellationToken);

        var tenantNames = await LoadTenantNamesAsync([invoice.ContractId], cancellationToken);

        // Tiền đã thu và yêu cầu mới nhất đều đọc từ bảng Payments, không suy từ trạng thái hóa đơn.
        var payments = await context.Payments
            .AsNoTracking()
            .Include(payment => payment.Proofs)
            .Where(payment => payment.InvoiceId == invoice.Id)
            .OrderByDescending(payment => payment.RequestedAt)
            .ThenByDescending(payment => payment.Id)
            .ToListAsync(cancellationToken);

        var paidAmount = payments
            .Where(payment => payment.Status == PaymentStatus.Confirmed)
            .Sum(payment => payment.Amount);
        var latestPayment = payments.Count > 0 ? payments[0] : null;

        // Lịch sử chỉnh sửa hoá đơn (mới nhất trước) để khách thuê thấy lý do Chủ trọ đã sửa.
        var revisions = await context.InvoiceRevisions
            .AsNoTracking()
            .Where(revision => revision.InvoiceId == invoice.Id)
            .OrderByDescending(revision => revision.ChangedAt)
            .ThenByDescending(revision => revision.Id)
            .Select(revision => new InvoiceRevisionDto
            {
                Id = revision.Id,
                Reason = revision.Reason,
                PreviousTotalAmount = revision.PreviousTotalAmount,
                NewTotalAmount = revision.NewTotalAmount,
                ChangedAt = revision.ChangedAt,
            })
            .ToListAsync(cancellationToken);

        // Đã có yêu cầu thanh toán (đang chờ xác nhận) hoặc đã thanh toán thì Chủ trọ không sửa được.
        var hasPaymentRequest = payments.Any(payment =>
            payment.Status is PaymentStatus.Pending or PaymentStatus.Confirmed);

        // Hoá đơn đã sửa nhưng bản mới chưa gửi cho khách: LastSentRevisionId lệch với revision mới nhất.
        var latestRevisionId = revisions.Count > 0 ? revisions.Max(revision => revision.Id) : (int?)null;
        var hasUnsentChanges = latestRevisionId is { } latest && invoice.LastSentRevisionId != latest;

        return new InvoiceDto
        {
            Id = invoice.Id,
            ContractId = invoice.ContractId,
            ContractNumber = header.ContractNumber,
            RoomCode = header.RoomCode,
            PropertyName = header.PropertyName,
            TenantNames = tenantNames.GetValueOrDefault(invoice.ContractId, []),
            CreatedAt = invoice.CreatedAt,
            IssuedAt = invoice.IssuedAt,
            PeriodStart = invoice.PeriodStart,
            PeriodEnd = invoice.PeriodEnd,
            DueDate = invoice.DueDate,
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
            OutstandingAmount = invoice.Status == InvoiceStatus.Issued
                ? Math.Max(0m, invoice.TotalAmount - paidAmount)
                : 0m,
            LatestPayment = latestPayment is null ? null : PaymentMapping.ToDto(latestPayment),
            Revisions = revisions,
            HasPaymentRequest = hasPaymentRequest,
            HasUnsentChanges = hasUnsentChanges,
        };
    }

    /// <summary>
    /// Gợi ý hợp đồng cho bộ lọc hóa đơn: tối đa <paramref name="take"/> dòng, tìm theo
    /// mã hợp đồng / mã phòng / tên tòa ở SQL thay vì tải cả danh sách về lọc.
    /// </summary>
    public async Task<IReadOnlyList<InvoiceContractOptionDto>> GetContractOptionsAsync(
        PropertyCaller caller,
        string? keyword,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var limit = Math.Clamp(take, 1, 100);
        var query = ContractOptionRows(caller);
        if (keyword?.Trim() is { Length: > 0 } term)
        {
            query = query.Where(row =>
                row.ContractNumber.Contains(term)
                || row.RoomCode.Contains(term)
                || row.PropertyName.Contains(term));
        }

        var rows = await query
            .OrderBy(row => row.ContractNumber)
            .ThenBy(row => row.ContractId)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new InvoiceContractOptionDto(row.ContractId, row.ContractNumber, row.RoomCode, row.PropertyName))
            .ToList();
    }

    /// <summary>Nạp đúng các hợp đồng đang được chọn (deep-link) để không rơi khỏi dropdown.</summary>
    public async Task<IReadOnlyList<InvoiceContractOptionDto>> GetContractOptionsByIdsAsync(
        PropertyCaller caller,
        IReadOnlyCollection<Guid> contractIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (contractIds.Count == 0)
        {
            return [];
        }

        var rows = await ContractOptionRows(caller)
            .Where(row => contractIds.Contains(row.ContractId))
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new InvoiceContractOptionDto(row.ContractId, row.ContractNumber, row.RoomCode, row.PropertyName))
            .ToList();
    }

    /// <summary>
    /// Bộ lọc hóa đơn chỉ liệt kê hợp đồng đã từng có hóa đơn; hợp đồng Pending/Rejected
    /// chưa thể phát sinh hóa đơn nên không thuộc dữ liệu chọn.
    /// </summary>
    private IQueryable<ContractOptionRow> ContractOptionRows(PropertyCaller caller) =>
        (from contract in ScopeContracts(caller)
         join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
         join property in context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
         where context.Invoices.Any(invoice => invoice.ContractId == contract.ContractId)
         select new ContractOptionRow
         {
             ContractId = contract.ContractId,
             ContractNumber = contract.ContractNumber,
             RoomCode = room.Code,
             PropertyName = property.Name,
         }).AsNoTracking();

    private sealed record ContractOptionRow
    {
        public Guid ContractId { get; init; }
        public string ContractNumber { get; init; } = string.Empty;
        public string RoomCode { get; init; } = string.Empty;
        public string PropertyName { get; init; } = string.Empty;
    }

    public async Task<IReadOnlyList<string>> GetContractTenantUserIdsAsync(
        Guid contractId,
        CancellationToken cancellationToken = default) =>
        await (from member in context.ContractMembers.AsNoTracking()
               join tenant in context.Tenants.IgnoreQueryFilters().AsNoTracking()
                   on member.TenantId equals tenant.Id
               where member.ContractId == contractId
               select tenant.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    private IQueryable<Invoice> ScopeInvoices(PropertyCaller caller)
    {
        var contractIds = ScopeContracts(caller).Select(contract => contract.ContractId);
        // Lọc tường minh hoá đơn đã xoá mềm: không phụ thuộc query filter của EF
        // (bộ lọc có thể bị bỏ qua khi entity tham gia quan hệ bắt buộc).
        var query = context.Invoices
            .AsNoTracking()
            .Where(invoice => !invoice.IsDeleted && contractIds.Contains(invoice.ContractId));
        // Hóa đơn Nháp là bản làm việc nội bộ: khách thuê không được thấy,
        // kể cả khi mở trực tiếp /my/invoices/{id}.
        return caller.IsKhachThue
            ? query.Where(invoice => invoice.Status != InvoiceStatus.Draft)
            : query;
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
            var blockedOwners = OwnerLockQuery.BlockedOwnerIds(context);
            var propertyIds =
                from assignment in context.PropertyManagers
                join property in context.Properties.IgnoreQueryFilters() on assignment.PropertyId equals property.Id
                where assignment.ManagerUserId == caller.UserId && assignment.RevokedAt == null
                    && !blockedOwners.Contains(property.OwnerId)
                select assignment.PropertyId;

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

    private IQueryable<Invoice> ApplyFilter(IQueryable<Invoice> query, InvoiceFilter filter)
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

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var term = filter.Keyword!;
            var invoiceId = ParsePrefixedNumber(term, "HD");
            var matchingContracts =
                from contract in context.Contracts
                join room in context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                join property in context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                where contract.ContractNumber.Contains(term)
                    || room.Code.Contains(term)
                    || property.Name.Contains(term)
                    || context.ContractMembers.Any(member => member.ContractId == contract.ContractId
                        && context.Tenants.IgnoreQueryFilters()
                            .Any(tenant => tenant.Id == member.TenantId && tenant.FullName.Contains(term)))
                select contract.ContractId;

            query = invoiceId is { } id
                ? query.Where(invoice => matchingContracts.Contains(invoice.ContractId) || invoice.Id == id)
                : query.Where(invoice => matchingContracts.Contains(invoice.ContractId));
        }

        if (filter.MinAmount is { } minAmount)
        {
            query = query.Where(invoice => invoice.TotalAmount >= minAmount);
        }

        if (filter.MaxAmount is { } maxAmount)
        {
            query = query.Where(invoice => invoice.TotalAmount <= maxAmount);
        }

        if (filter.PaymentState is { } paymentState)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var payments = context.Payments.AsNoTracking();
            query = paymentState switch
            {
                InvoicePaymentState.Unpaid => query.Where(invoice =>
                    !context.Payments.Any(payment =>
                        payment.InvoiceId == invoice.Id && payment.Status == PaymentStatus.Confirmed)),
                InvoicePaymentState.Partial => query.Where(invoice =>
                    (context.Payments
                        .Where(payment => payment.InvoiceId == invoice.Id && payment.Status == PaymentStatus.Confirmed)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m) > 0m
                    && (context.Payments
                        .Where(payment => payment.InvoiceId == invoice.Id && payment.Status == PaymentStatus.Confirmed)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m) < invoice.TotalAmount),
                InvoicePaymentState.Paid => query.Where(invoice =>
                    invoice.Status == InvoiceStatus.Paid
                    || (context.Payments
                        .Where(payment => payment.InvoiceId == invoice.Id && payment.Status == PaymentStatus.Confirmed)
                        .Sum(payment => (decimal?)payment.Amount) ?? 0m) >= invoice.TotalAmount),
                InvoicePaymentState.PendingConfirmation => query.Where(invoice =>
                    invoice.Status == InvoiceStatus.Issued
                    && payments.Any(payment => payment.InvoiceId == invoice.Id
                        && payment.Status == PaymentStatus.Pending)),
                InvoicePaymentState.PaymentRejected => query.Where(invoice =>
                    invoice.Status == InvoiceStatus.Issued
                    && payments.Any(payment => payment.InvoiceId == invoice.Id
                        && payment.Status == PaymentStatus.Rejected)
                    && !payments.Any(payment => payment.InvoiceId == invoice.Id
                        && payment.Status == PaymentStatus.Pending)),
                InvoicePaymentState.Expired => query.Where(invoice =>
                    invoice.Status == InvoiceStatus.Issued
                    && invoice.DueDate != null && invoice.DueDate < today
                    && !payments.Any(payment => payment.InvoiceId == invoice.Id
                        && (payment.Status == PaymentStatus.Pending || payment.Status == PaymentStatus.Confirmed))),
                _ => query,
            };
        }

        return query;
    }

    /// <summary>Đọc mã dạng "HD-12" hoặc "12"; trả null khi từ khóa không phải mã số.</summary>
    private static int? ParsePrefixedNumber(string term, string prefix)
    {
        var normalized = term.Trim();
        if (normalized.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[(prefix.Length + 1)..];
        }

        return int.TryParse(normalized, out var value) ? value : null;
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
