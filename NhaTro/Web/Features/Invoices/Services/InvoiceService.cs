using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Features.Properties;
using Web.Features.Rooms;
using Web.Features.Settings;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Infrastructure.Time;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Lập/phát hành hóa đơn từ nguồn chi phí đã chốt.
/// Service chỉ tin dữ liệu nguồn sau khi kiểm hợp đồng, phạm vi và từng dòng;
/// mọi phép tính tiền dùng chung <see cref="InvoiceCalculationRules"/>.
/// Lập hóa đơn (SW-10) và phát hành (SW-11) là hai thao tác ⚙️ của Quản lý: chế độ công tắc
/// quyết định ghi ngay, vào hàng đợi duyệt hay bị chặn — Chủ trọ luôn ghi ngay (D4).
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly IInvoiceQueryService _invoiceQuery;
    private readonly TimeProvider _timeProvider;
    private readonly IManagerTogglePolicy? _togglePolicy;
    private readonly IManagerApprovalService? _approvals;

    public InvoiceService(
        ApplicationDbContext context,
        IInvoiceQueryService invoiceQuery,
        TimeProvider? timeProvider = null,
        IManagerTogglePolicy? togglePolicy = null,
        IManagerApprovalService? approvals = null)
    {
        _context = context;
        _invoiceQuery = invoiceQuery;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _togglePolicy = togglePolicy;
        _approvals = approvals;
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

        // Công tắc phải được kiểm TRƯỚC mọi lần trả về "idempotent": Quản lý ở chế độ
        // Không thể không được nhận success chỉ vì hóa đơn của kỳ đó đã tồn tại.
        var decision = await EvaluateToggleAsync(ManagerAction.SW10, caller, cancellationToken);
        if (decision == RoomChangeDecision.Denied)
        {
            EnsureToggleAllowed(decision);
        }

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

        if (decision == RoomChangeDecision.RequiresApproval)
        {
            await CreateDraftApprovalRequestAsync(contract, items, request, caller, cancellationToken);
            throw new InvoiceOperationException(
                InvoiceErrorMessages.ApprovalRequired,
                InvoiceErrorCodes.ApprovalRequired);
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
            Items = ToEntities(items),
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
    }

    /// <summary>
    /// Dòng hóa đơn lưu dưới dạng snapshot đã chốt (thành tiền do rule tính, không lấy số client gửi).
    /// Applier SW-10 dựng lại dòng bằng chính hàm này để bản được duyệt giống bản Quản lý đã xin.
    /// </summary>
    internal static List<InvoiceItem> ToEntities(IReadOnlyList<InvoiceItemInputDto> source)
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

        // Công tắc phải được kiểm TRƯỚC nhánh "gửi lặp": Quản lý ở chế độ Không thể không được
        // nhận success chỉ vì hóa đơn đã phát hành trước đó.
        var decision = await EvaluateToggleAsync(ManagerAction.SW11, caller, cancellationToken);
        if (decision == RoomChangeDecision.Denied)
        {
            EnsureToggleAllowed(decision);
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

        if (decision == RoomChangeDecision.RequiresApproval)
        {
            await CreateIssueApprovalRequestAsync(invoice, contract, caller, cancellationToken);
            throw new InvoiceOperationException(
                InvoiceErrorMessages.ApprovalRequired,
                InvoiceErrorCodes.ApprovalRequired);
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

        // Kiểm công tắc TRƯỚC khi mở transaction: một yêu cầu chờ duyệt tạo bên trong
        // transaction này sẽ bị rollback kèm khi bước sau ném lỗi, thành ra "đã gửi duyệt" mà DB không có gì.
        await EnsureDirectExecutionAsync(caller, cancellationToken);

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

    /// <summary>
    /// Chủ trọ là cấp cao nhất nên thao tác của họ có hiệu lực ngay (D4). Quản lý phải đi qua
    /// công tắc; thiếu policy thì coi như "cần duyệt" — fail closed, không mặc nhiên cho ghi sổ.
    /// </summary>
    private async Task<RoomChangeDecision> EvaluateToggleAsync(
        ManagerAction action,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (!caller.IsQuanLy)
        {
            return RoomChangeDecision.Allowed;
        }

        return _togglePolicy is null
            ? RoomChangeDecision.RequiresApproval
            : await _togglePolicy.EvaluateAsync(action, caller.Role, cancellationToken);
    }

    private static void EnsureToggleAllowed(RoomChangeDecision decision)
    {
        if (decision != RoomChangeDecision.Allowed)
        {
            throw new InvoiceOperationException(
                InvoiceErrorMessages.ApprovalDenied,
                InvoiceErrorCodes.ApprovalDenied);
        }
    }

    /// <summary>
    /// Bản gộp "lập rồi phát hành" là một transaction nên không dừng giữa chừng để chờ duyệt được:
    /// Quản lý ở chế độ Duyệt phải gửi từng bước, mỗi bước một yêu cầu cho Chủ trọ quyết định.
    /// </summary>
    private async Task EnsureDirectExecutionAsync(PropertyCaller caller, CancellationToken cancellationToken)
    {
        foreach (var action in new[] { ManagerAction.SW10, ManagerAction.SW11 })
        {
            var decision = await EvaluateToggleAsync(action, caller, cancellationToken);
            if (decision == RoomChangeDecision.RequiresApproval)
            {
                throw new InvoiceOperationException(
                    InvoiceErrorMessages.ApprovalComposite,
                    InvoiceErrorCodes.ApprovalRequired);
            }

            EnsureToggleAllowed(decision);
        }
    }

    private async Task CreateDraftApprovalRequestAsync(
        Contract contract,
        IReadOnlyList<InvoiceItemInputDto> items,
        CreateInvoiceRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var payload = new InvoiceDraftApprovalPayload(
            contract.ContractId,
            request.PeriodStart,
            request.PeriodEnd,
            items);

        await CreateApprovalRequestAsync(
            ManagerAction.SW10,
            ApprovalTargetType.Contract,
            contract.ContractId.ToString("D"),
            await ResolvePropertyIdAsync(contract.RoomId, cancellationToken),
            JsonSerializer.Serialize(payload, PayloadJsonOptions),
            expectedToken: null,
            caller,
            cancellationToken);
    }

    private async Task CreateIssueApprovalRequestAsync(
        Invoice invoice,
        Contract contract,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        await CreateApprovalRequestAsync(
            ManagerAction.SW11,
            ApprovalTargetType.Invoice,
            invoice.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            await ResolvePropertyIdAsync(contract.RoomId, cancellationToken),
            JsonSerializer.Serialize(
                new InvoiceIssueApprovalPayload(invoice.Id), PayloadJsonOptions),
            invoice.RowVersion,
            caller,
            cancellationToken);
    }

    private async Task CreateApprovalRequestAsync(
        ManagerAction action,
        ApprovalTargetType targetType,
        string targetKey,
        int propertyId,
        string payloadJson,
        byte[]? expectedToken,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (_approvals is null)
        {
            throw new InvoiceOperationException(
                InvoiceErrorMessages.ApprovalFailed,
                InvoiceErrorCodes.ApprovalFailed);
        }

        var approval = await _approvals.CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = ManagerToggleCatalog.ToCode(action),
                TargetType = targetType,
                TargetKey = targetKey,
                PropertyId = propertyId,
                PayloadJson = payloadJson,
                ExpectedToken = expectedToken,
            },
            new ApprovalCaller(caller.UserId, caller.Role),
            cancellationToken);

        if (approval.Succeeded && approval.Request is not null)
        {
            return;
        }

        throw new InvoiceOperationException(
            approval.ErrorMessage ?? InvoiceErrorMessages.ApprovalFailed,
            InvoiceErrorCodes.ApprovalFailed);
    }

    private async Task<int> ResolvePropertyIdAsync(int roomId, CancellationToken cancellationToken)
    {
        var propertyId = await _context.Rooms
            .IgnoreQueryFilters()
            .Where(room => room.Id == roomId)
            .Select(room => (int?)room.PropertyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (propertyId is null)
        {
            throw new InvoiceNotFoundException(
                InvoiceErrorMessages.ContractNotFound,
                InvoiceErrorCodes.ContractNotFound);
        }

        return propertyId.Value;
    }

    /// <summary>internal: applier SW-10/SW-11 tái dùng đúng chốt kiểm này khi Chủ trọ duyệt.</summary>
    internal static void ValidateContractAndPeriod(
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
            // Quantity/UnitPrice are persisted at four decimal places so a P5 service snapshot
            // retains its source precision. Compare at the database precision for idempotency.
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
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);
}
