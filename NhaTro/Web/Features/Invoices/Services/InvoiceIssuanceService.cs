using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Rules;
using Web.Features.Properties;
using Web.Features.Utilities;
using Web.Features.Utilities.Services;
using UtilityReadingException = Web.Features.Utilities.UtilityReadingException;
using UtilityReadingAuthorizationException = Web.Features.Utilities.UtilityReadingAuthorizationException;
using UtilityReadingNotFoundException = Web.Features.Utilities.UtilityReadingNotFoundException;

namespace Web.Features.Invoices.Services;

/// <summary>
/// VHT-137 (3B) — ghép dòng hóa đơn từ nguồn thật. Nguồn chi phí là kỳ chỉ số đã chốt của VHT-51
/// (thuê + điện + nước + dịch vụ snapshot); dịch vụ vào hóa đơn đúng giá đã chụp lúc ghi chỉ số.
/// Việc lưu snapshot/kiểm tổng/chống trùng kỳ vẫn do <see cref="IInvoiceService"/> đảm nhận.
/// </summary>
public sealed class InvoiceIssuanceService : IInvoiceIssuanceService
{
    private readonly ApplicationDbContext _context;
    private readonly IInvoiceService _invoices;
    private readonly IUtilityReadingService _readings;

    public InvoiceIssuanceService(
        ApplicationDbContext context,
        IInvoiceService invoices,
        IUtilityReadingService readings)
    {
        _context = context;
        _invoices = invoices;
        _readings = readings;
    }

    public async Task<InvoicePeriodSourceDto> PreviewPeriodAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default) =>
        await ComposeAsync(contractId, periodStart, periodEnd, caller, cancellationToken);

    public async Task<InvoiceDto> CreateDraftAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        var request = await BuildRequestAsync(contractId, periodStart, periodEnd, caller, cancellationToken);
        return await _invoices.CreateDraftAsync(request, caller, cancellationToken);
    }

    public async Task<InvoiceDto> CreateAndIssueAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        var request = await BuildRequestAsync(contractId, periodStart, periodEnd, caller, cancellationToken);
        return await _invoices.CreateAndIssueAsync(request, caller, cancellationToken);
    }

    private async Task<InvoicePeriodSourceDto> ComposeAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var request = await BuildRequestAsync(contractId, periodStart, periodEnd, caller, cancellationToken);

        var contract = await _context.Contracts
            .AsNoTracking()
            .SingleAsync(item => item.ContractId == contractId, cancellationToken);

        var place = await (from room in _context.Rooms.IgnoreQueryFilters().AsNoTracking()
                           join property in _context.Properties.IgnoreQueryFilters().AsNoTracking()
                               on room.PropertyId equals property.Id
                           where room.Id == contract.RoomId
                           select new { room.Code, PropertyName = property.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (place is null)
        {
            throw new InvoiceNotFoundException(
                InvoiceErrorMessages.ContractNotFound,
                InvoiceErrorCodes.ContractNotFound);
        }

        var period = await _readings.GetPeriodByContractAsync(contractId, periodStart, caller, cancellationToken);

        return new InvoicePeriodSourceDto(
            contract.ContractId,
            contract.ContractNumber,
            place.Code,
            place.PropertyName,
            periodStart,
            periodEnd,
            request.Items,
            InvoiceCalculationRules.CalculateTotal(request.Items),
            period?.Id);
    }

    /// <summary>
    /// Chỉ nhận kỳ chỉ số đã chốt đúng ngày kết thúc. Kỳ lệch ngày, kỳ chưa ghi chỉ số hoặc
    /// chưa chụp dịch vụ đều bị chặn bằng mã lỗi riêng, không suy diễn chi phí.
    /// </summary>
    private async Task<CreateInvoiceRequest> BuildRequestAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var contract = await ManageableContracts(caller)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ContractId == contractId, cancellationToken);
        if (contract is null)
        {
            throw new InvoiceNotFoundException(
                InvoiceErrorMessages.ContractNotFound,
                InvoiceErrorCodes.ContractNotFound);
        }

        InvoiceService.ValidateContractAndPeriod(contract, periodStart, periodEnd);

        try
        {
            var period = await _readings.GetPeriodByContractAsync(contractId, periodStart, caller, cancellationToken)
                ?? throw new InvoiceValidationException(
                    InvoiceErrorMessages.UtilityPeriodMismatch,
                    InvoiceErrorCodes.UtilityPeriodMismatch);

            if (period.PeriodEnd != periodEnd)
            {
                throw new InvoiceValidationException(
                    InvoiceErrorMessages.UtilityPeriodMismatch,
                    InvoiceErrorCodes.UtilityPeriodMismatch);
            }

            var built = await _readings.BuildInvoiceRequestAsync(period.Id, caller, cancellationToken);
            return new CreateInvoiceRequest
            {
                ContractId = contractId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Items = built.Items,
            };
        }
        catch (UtilityReadingAuthorizationException)
        {
            throw new InvoiceAuthorizationException(InvoiceErrorMessages.Forbidden, InvoiceErrorCodes.Forbidden);
        }
        catch (UtilityReadingNotFoundException exception)
        {
            throw exception.Code == nameof(UtilityReadingErrorCode.ContractNotFound)
                ? new InvoiceNotFoundException(InvoiceErrorMessages.ContractNotFound, InvoiceErrorCodes.ContractNotFound)
                : new InvoiceValidationException(InvoiceErrorMessages.UtilityPeriodMismatch, InvoiceErrorCodes.UtilityPeriodMismatch);
        }
        catch (UtilityReadingException exception)
        {
            throw Translate(exception);
        }
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

    private static Exception Translate(UtilityReadingException exception) => exception.Code switch
    {
        nameof(UtilityReadingErrorCode.ContractNotApproved) =>
            new InvoiceValidationException(exception.Message, InvoiceErrorCodes.ContractNotApproved),
        nameof(UtilityReadingErrorCode.InvalidPeriod) or nameof(UtilityReadingErrorCode.PeriodOutsideContract) =>
            new InvoiceValidationException(exception.Message, InvoiceErrorCodes.InvalidPeriod),
        nameof(UtilityReadingErrorCode.InvoicePeriodMismatch)
            or nameof(UtilityReadingErrorCode.PeriodHasNoReadings)
            or nameof(UtilityReadingErrorCode.ServiceSnapshotsMissing) =>
            new InvoiceValidationException(exception.Message, InvoiceErrorCodes.UtilityPeriodMismatch),
        nameof(UtilityReadingErrorCode.PeriodLocked) =>
            new InvoiceOperationException(exception.Message, InvoiceErrorCodes.InvalidStatus, exception),
        _ => new InvoiceOperationException(exception.Message, exception.Code, exception),
    };
}
