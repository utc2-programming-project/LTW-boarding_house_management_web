using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Features.Properties;
using Web.Infrastructure.Time;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Lập/phát hành hóa đơn từ nguồn chi phí đã chốt.
/// Service chỉ tin dữ liệu nguồn sau khi kiểm hợp đồng, phạm vi và từng dòng;
/// mọi phép tính tiền dùng chung <see cref="InvoiceCalculationRules"/>.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private readonly ApplicationDbContext _context;
    private readonly IInvoiceQueryService _invoiceQuery;
    private readonly TimeProvider _timeProvider;

    public InvoiceService(
        ApplicationDbContext context,
        IInvoiceQueryService invoiceQuery,
        TimeProvider? timeProvider = null)
    {
        _context = context;
        _invoiceQuery = invoiceQuery;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<InvoiceDto> CreateDraftAsync(
        CreateInvoiceRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        var items = InvoiceDraftRules.NormalizeItems(request.Items);
        var contract = await LoadManageableContractAsync(
            request.ContractId,
            caller,
            cancellationToken);
        ValidateContractAndPeriod(contract, request.PeriodStart, request.PeriodEnd);

        var total = InvoiceCalculationRules.CalculateTotal(items);

        var existing = await FindByPeriodAsync(
            request.ContractId,
            request.PeriodStart,
            cancellationToken);
        if (existing is not null)
        {
            if (Matches(existing, request.PeriodEnd, items, total))
            {
                return await _invoiceQuery.GetInvoiceAsync(existing.Id, caller, cancellationToken);
            }

            throw new InvoiceOperationException(
                InvoiceErrorMessages.DuplicateInvoice,
                InvoiceErrorCodes.DuplicateInvoice);
        }

        var now = _timeProvider.UtcNow();
        var invoice = new Invoice
        {
            ContractId = request.ContractId,
            PeriodStart = request.PeriodStart,
            PeriodEnd = request.PeriodEnd,
            Status = InvoiceStatus.Draft,
            TotalAmount = total,
            CreatedAt = now,
            Items = invoiceItems(items),
        };

        _context.Invoices.Add(invoice);

        try
        {
            // Một SaveChanges cho cả hóa đơn và dòng: lỗi dòng nào cũng không để lại
            // hóa đơn thiếu dòng. Unique index (ContractId, PeriodStart) là chốt chặn cuối.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Gỡ từng dòng trước khi gỡ hóa đơn: đặt hóa đơn sang Detached khi các dòng
            // còn tracked sẽ khiến EF cố xóa FK bắt buộc và ném lỗi che mất lỗi trùng kỳ.
            foreach (var item in invoice.Items.ToList())
            {
                _context.Entry(item).State = EntityState.Detached;
            }

            _context.Entry(invoice).State = EntityState.Detached;

            var duplicate = await FindByPeriodAsync(
                request.ContractId,
                request.PeriodStart,
                cancellationToken);
            if (duplicate is not null && Matches(duplicate, request.PeriodEnd, items, total))
            {
                return await _invoiceQuery.GetInvoiceAsync(duplicate.Id, caller, cancellationToken);
            }

            throw new InvoiceOperationException(
                InvoiceErrorMessages.DuplicateInvoice,
                InvoiceErrorCodes.DuplicateInvoice,
                exception);
        }

        return await _invoiceQuery.GetInvoiceAsync(invoice.Id, caller, cancellationToken);

        List<InvoiceItem> invoiceItems(IReadOnlyList<InvoiceItemInputDto> source)
        {
            var result = new List<InvoiceItem>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                var item = InvoiceCalculationRules.ToItem(source[index]);
                result.Add(new InvoiceItem
                {
                    ItemType = item.ItemType,
                    Description = item.Description,
                    Unit = item.Unit,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount,
                    DisplayOrder = index + 1,
                });
            }

            return result;
        }
    }

    public async Task<InvoiceDto> IssueAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var contractIds = ManageableContracts(caller).Select(contract => contract.ContractId);
        var invoice = await _context.Invoices
            .Include(item => item.Items)
            .SingleOrDefaultAsync(
                item => item.Id == invoiceId && contractIds.Contains(item.ContractId),
                cancellationToken);

        // Không phân biệt không tồn tại với ngoài phạm vi để không lộ ID.
        if (invoice is null)
        {
            throw new InvoiceNotFoundException(
                InvoiceErrorMessages.InvoiceNotFound,
                InvoiceErrorCodes.InvoiceNotFound);
        }

        // Gửi lặp yêu cầu phát hành không đổi dữ liệu và không tạo hóa đơn mới.
        if (invoice.Status != InvoiceStatus.Draft)
        {
            return await _invoiceQuery.GetInvoiceAsync(invoice.Id, caller, cancellationToken);
        }

        // Kiểm lại nguồn hợp đồng ngay trước khi phát hành: hóa đơn nháp có thể được
        // tạo từ trước, nhưng hợp đồng/kỳ vẫn phải còn hợp lệ ở thời điểm phát hành.
        var contract = await _context.Contracts
            .AsNoTracking()
            .SingleAsync(item => item.ContractId == invoice.ContractId, cancellationToken);
        ValidateContractAndPeriod(contract, invoice.PeriodStart, invoice.PeriodEnd);

        var total = InvoiceCalculationRules.TotalOf(invoice.Items
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
            }));

        if (invoice.Items.Count == 0 || total != invoice.TotalAmount)
        {
            throw new InvoiceOperationException(
                InvoiceErrorMessages.TotalMismatch,
                InvoiceErrorCodes.TotalMismatch);
        }

        var now = _timeProvider.UtcNow();
        invoice.Status = InvoiceStatus.Issued;
        invoice.IssuedAt = now;
        invoice.UpdatedAt = now;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Hai yêu cầu phát hành đồng thời: bản còn lại đã ghi trước.
            await _context.Entry(invoice).ReloadAsync(cancellationToken);
            if (invoice.Status != InvoiceStatus.Draft)
            {
                return await _invoiceQuery.GetInvoiceAsync(invoice.Id, caller, cancellationToken);
            }

            throw new InvoiceOperationException(
                InvoiceErrorMessages.InvalidStatus,
                InvoiceErrorCodes.InvalidStatus);
        }

        return await _invoiceQuery.GetInvoiceAsync(invoice.Id, caller, cancellationToken);
    }

    public async Task<InvoiceDto> CreateAndIssueAsync(
        CreateInvoiceRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        // Lập và phát hành là một nghiệp vụ: nếu bước phát hành lỗi thì rollback cả
        // hóa đơn nháp, không để lại bản ghi dở dang.
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var draft = await CreateDraftAsync(request, caller, cancellationToken);
            var issued = await IssueAsync(draft.Id, caller, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return issued;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<Contract> LoadManageableContractAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var contract = await ManageableContracts(caller)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ContractId == contractId, cancellationToken);

        if (contract is null)
        {
            throw new InvoiceNotFoundException(
                InvoiceErrorMessages.ContractNotFound,
                InvoiceErrorCodes.ContractNotFound);
        }

        return contract;
    }

    private IQueryable<Contract> ManageableContracts(PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return from contract in _context.Contracts
                   join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                   where property.OwnerId == caller.UserId
                   select contract;
        }

        if (caller.IsQuanLy)
        {
            var propertyIds = _context.PropertyManagers
                .Where(assignment => assignment.ManagerUserId == caller.UserId && assignment.RevokedAt == null)
                .Select(assignment => assignment.PropertyId);

            return from contract in _context.Contracts
                   join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   where propertyIds.Contains(room.PropertyId)
                   select contract;
        }

        throw new InvoiceAuthorizationException(
            InvoiceErrorMessages.Forbidden,
            InvoiceErrorCodes.Forbidden);
    }

    private static void ValidateContractAndPeriod(
        Contract contract,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        if (contract.Status != ContractStatus.Approved)
        {
            throw new InvoiceOperationException(
                InvoiceErrorMessages.ContractNotApproved,
                InvoiceErrorCodes.ContractNotApproved);
        }

        if (periodEnd < periodStart)
        {
            throw new InvoiceValidationException(
                InvoiceErrorMessages.InvalidPeriod,
                InvoiceErrorCodes.InvalidPeriod,
                nameof(periodEnd));
        }

        var contractStart = DateOnly.FromDateTime(contract.StartDate);
        var contractEnd = DateOnly.FromDateTime(contract.EndDate);
        if (periodStart < contractStart || periodEnd > contractEnd)
        {
            throw new InvoiceValidationException(
                InvoiceErrorMessages.PeriodOutsideContract,
                InvoiceErrorCodes.InvalidPeriod,
                nameof(periodStart));
        }
    }

    private Task<Invoice?> FindByPeriodAsync(
        Guid contractId,
        DateOnly periodStart,
        CancellationToken cancellationToken) =>
        _context.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Items)
            .SingleOrDefaultAsync(
                invoice => invoice.ContractId == contractId && invoice.PeriodStart == periodStart,
                cancellationToken);

    private static bool Matches(
        Invoice invoice,
        DateOnly periodEnd,
        IReadOnlyList<InvoiceItemInputDto> items,
        decimal total)
    {
        if (invoice.PeriodEnd != periodEnd || invoice.TotalAmount != total)
        {
            return false;
        }

        var existingItems = invoice.Items
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Id)
            .ToArray();
        if (existingItems.Length != items.Count)
        {
            return false;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var expected = InvoiceCalculationRules.ToItem(items[index]);
            var actual = existingItems[index];
            // Quantity/UnitPrice của bảng hóa đơn là decimal(18,2); Amount mới là số tiền
            // snapshot đã chốt. So sánh theo đúng độ chính xác đã lưu để lần gửi lặp khớp.
            if (actual.ItemType != expected.ItemType
                || actual.Description != expected.Description
                || actual.Unit != expected.Unit
                || actual.Quantity != ToStoredDecimal(expected.Quantity)
                || actual.UnitPrice != ToStoredDecimal(expected.UnitPrice)
                || actual.Amount != expected.Amount)
            {
                return false;
            }
        }

        return true;
    }

    private static decimal ToStoredDecimal(decimal value) =>
        decimal.Round(value, 2, MidpointRounding.AwayFromZero);
}
