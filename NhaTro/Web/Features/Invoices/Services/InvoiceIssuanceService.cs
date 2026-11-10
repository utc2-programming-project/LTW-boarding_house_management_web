using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Features.Properties;
using Web.Features.Utilities;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;

namespace Web.Features.Invoices.Services;

/// <summary>
/// VHT-137 (3B) — ghép dòng hóa đơn từ nguồn thật:
/// tiền thuê lấy <see cref="Contract.SnapshotPrice"/>, điện/nước lấy chỉ số VHT-51
/// (ưu tiên snapshot biểu giá đã lưu, chưa có thì tính từ biểu giá hiệu lực),
/// dịch vụ lấy đơn giá đang hiệu lực của nhà trọ hoặc danh mục dùng chung.
/// Việc lưu snapshot/kiểm tổng/chống trùng kỳ vẫn do <see cref="IInvoiceService"/> đảm nhận.
/// </summary>
public sealed class InvoiceIssuanceService : IInvoiceIssuanceService
{
    private static readonly string[] MeteredUnits = ["kwh", "m³", "m3"];
    private static readonly string[] PerPersonUnits = ["người", "nguoi", "person"];

    private readonly ApplicationDbContext _context;
    private readonly IInvoiceService _invoices;
    private readonly IUtilityTariffService _tariffs;

    public InvoiceIssuanceService(
        ApplicationDbContext context,
        IInvoiceService invoices,
        IUtilityTariffService tariffs)
    {
        _context = context;
        _invoices = invoices;
        _tariffs = tariffs;
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
        var source = await ComposeAsync(contractId, periodStart, periodEnd, caller, cancellationToken);
        return await _invoices.CreateDraftAsync(
            new CreateInvoiceRequest
            {
                ContractId = contractId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Items = source.Items,
            },
            caller,
            cancellationToken);
    }

    public async Task<InvoiceDto> CreateAndIssueAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        var source = await ComposeAsync(contractId, periodStart, periodEnd, caller, cancellationToken);
        return await _invoices.CreateAndIssueAsync(
            new CreateInvoiceRequest
            {
                ContractId = contractId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Items = source.Items,
            },
            caller,
            cancellationToken);
    }

    private async Task<InvoicePeriodSourceDto> ComposeAsync(
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

        var place = await (from room in _context.Rooms.IgnoreQueryFilters().AsNoTracking()
                           join property in _context.Properties.IgnoreQueryFilters().AsNoTracking()
                               on room.PropertyId equals property.Id
                           where room.Id == contract.RoomId
                           select new { room.Code, room.PropertyId, PropertyName = property.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (place is null)
        {
            throw new InvoiceNotFoundException(
                InvoiceErrorMessages.ContractNotFound,
                InvoiceErrorCodes.ContractNotFound);
        }

        var reading = await FindReadingAsync(contract.RoomId, periodStart, periodEnd, cancellationToken);
        var residentCount = await _context.ContractMembers
            .AsNoTracking()
            .CountAsync(member => member.ContractId == contract.ContractId, cancellationToken);
        residentCount = Math.Max(1, residentCount);

        var items = new List<InvoiceItemInputDto>(8);
        AddRent(items, contract.SnapshotPrice);

        if (reading is not null)
        {
            var electric = await BuildUtilityItemAsync(
                reading, TariffType.Electric, residentCount, cancellationToken);
            if (electric is not null) items.Add(electric);

            var water = await BuildUtilityItemAsync(
                reading, TariffType.Water, residentCount, cancellationToken);
            if (water is not null) items.Add(water);
        }

        items.AddRange(await BuildServiceItemsAsync(
            place.PropertyId, periodStart, periodEnd, residentCount, cancellationToken));

        if (items.Count == 0)
        {
            throw new InvoiceValidationException(
                InvoiceErrorMessages.ItemsRequired,
                InvoiceErrorCodes.ItemsRequired);
        }

        return new InvoicePeriodSourceDto(
            contract.ContractId,
            contract.ContractNumber,
            place.Code,
            place.PropertyName,
            periodStart,
            periodEnd,
            items,
            InvoiceCalculationRules.CalculateTotal(items),
            reading?.Id);
    }

    private static void AddRent(List<InvoiceItemInputDto> items, decimal monthlyRent)
    {
        if (monthlyRent <= 0m) return;

        items.Add(new InvoiceItemInputDto
        {
            ItemType = InvoiceItemType.Rent,
            Description = "Tiền thuê",
            Unit = "tháng",
            Quantity = 1m,
            UnitPrice = monthlyRent,
            Amount = monthlyRent,
        });
    }

    /// <summary>
    /// Chỉ nhận đúng kỳ chỉ số đã chốt: cùng ngày bắt đầu mà lệch ngày kết thúc là dữ liệu
    /// không khớp, không được tự ghép chi phí của kỳ khác vào hóa đơn.
    /// </summary>
    private async Task<UtilityReading?> FindReadingAsync(
        int roomId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        var reading = await _context.UtilityReadings
            .AsNoTracking()
            .Where(item => item.RoomId == roomId && item.PeriodStart == periodStart)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (reading is not null && reading.PeriodEnd != periodEnd)
        {
            throw new InvoiceValidationException(
                InvoiceErrorMessages.UtilityPeriodMismatch,
                InvoiceErrorCodes.UtilityPeriodMismatch);
        }

        return reading;
    }

    private async Task<InvoiceItemInputDto?> BuildUtilityItemAsync(
        UtilityReading reading,
        TariffType type,
        int residentCount,
        CancellationToken cancellationToken)
    {
        var usage = type == TariffType.Electric
            ? Math.Max(0m, reading.ElectricEnd - reading.ElectricStart)
            : Math.Max(0m, reading.WaterEnd - reading.WaterStart);
        if (usage <= 0m) return null;

        var total = await ResolveUtilityTotalAsync(
            reading.Id, type, reading.PeriodStart, reading.PeriodEnd, usage, residentCount, cancellationToken);
        if (total <= 0m) return null;

        return new InvoiceItemInputDto
        {
            ItemType = type == TariffType.Electric ? InvoiceItemType.Electric : InvoiceItemType.Water,
            Description = type == TariffType.Electric
                ? $"Tiền điện kỳ {reading.PeriodStart:dd/MM/yyyy} - {reading.PeriodEnd:dd/MM/yyyy}"
                : $"Tiền nước kỳ {reading.PeriodStart:dd/MM/yyyy} - {reading.PeriodEnd:dd/MM/yyyy}",
            Unit = type == TariffType.Electric ? "kWh" : "m³",
            Quantity = usage,
            UnitPrice = MoneyFormatter.RoundVnd(total / usage),
            Amount = total,
        };
    }

    /// <summary>
    /// Ưu tiên số tiền đã chốt trong snapshot của kỳ chỉ số; chưa có snapshot thì tính từ
    /// biểu giá VHT-51 đang hiệu lực. Thiếu biểu giá là lỗi chặn phát hành, không suy diễn 0 đồng.
    /// </summary>
    private async Task<decimal> ResolveUtilityTotalAsync(
        int readingId,
        TariffType type,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal usage,
        int residentCount,
        CancellationToken cancellationToken)
    {
        var snapshotTotal = await _context.UtilityBillingPeriodSnapshots
            .AsNoTracking()
            .Where(snapshot => snapshot.BillingPeriodId == readingId && snapshot.Type == type)
            .Select(snapshot => (decimal?)snapshot.Total)
            .SingleOrDefaultAsync(cancellationToken);
        if (snapshotTotal is { } stored)
        {
            return stored;
        }

        try
        {
            var calculated = await _tariffs.CalculateForPeriodAsync(
                type,
                type == TariffType.Electric ? UtilityTariffScopes.Electric : UtilityTariffScopes.Water,
                periodStart,
                periodEnd,
                usage,
                residentCount,
                cancellationToken);
            return calculated.Total;
        }
        catch (InvalidOperationException exception)
        {
            throw new InvoiceOperationException(
                InvoiceErrorMessages.TariffNotConfigured,
                InvoiceErrorCodes.TariffNotConfigured,
                exception);
        }
    }

    private async Task<IReadOnlyList<InvoiceItemInputDto>> BuildServiceItemsAsync(
        int propertyId,
        DateOnly periodStart,
        DateOnly periodEnd,
        int residentCount,
        CancellationToken cancellationToken)
    {
        var versions = await _context.ServicePriceHistories
            .AsNoTracking()
            .Include(version => version.ServiceCatalog)
            .Where(version => (version.ServiceCatalog.PropertyId == propertyId
                    || version.ServiceCatalog.PropertyId == null)
                && version.ServiceCatalog.IsActive
                && !version.ServiceCatalog.IsReadOnly
                && !version.ServiceCatalog.IsDeleted
                && version.EffectiveDate <= periodEnd)
            .ToListAsync(cancellationToken);

        var effective = versions
            .Where(version => version.EffectiveTo is null || version.EffectiveTo >= periodStart)
            .GroupBy(version => version.ServiceCatalogId)
            .Select(group => group
                .OrderByDescending(version => version.EffectiveDate)
                .ThenByDescending(version => version.Id)
                .First())
            .OrderBy(version => version.ServiceCatalogId);

        var items = new List<InvoiceItemInputDto>();
        foreach (var version in effective)
        {
            // Điện/nước tính theo chỉ số, không bán lại như dịch vụ cố định.
            if (IsMeteredUnit(version.Unit)) continue;

            var quantity = IsPerPersonUnit(version.Unit) ? residentCount : 1m;
            var amount = MoneyFormatter.RoundVnd(version.UnitPrice * quantity);
            if (amount <= 0m) continue;

            items.Add(new InvoiceItemInputDto
            {
                ItemType = InvoiceItemType.Service,
                Description = version.Name,
                Unit = version.Unit,
                Quantity = quantity,
                UnitPrice = version.UnitPrice,
                Amount = amount,
            });
        }

        return items;
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

    private static bool IsMeteredUnit(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return MeteredUnits.Contains(normalized);
    }

    private static bool IsPerPersonUnit(string unit)
    {
        var normalized = unit.Trim().ToLowerInvariant();
        return PerPersonUnits.Contains(normalized);
    }
}
