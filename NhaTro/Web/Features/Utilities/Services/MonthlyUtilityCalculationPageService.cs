using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Data.Tariffs;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;

namespace Web.Features.Utilities.Services;

/// <summary>
/// VHT-58 — dựng dữ liệu cho màn hình chỉ số/chi phí P5 từ SQL thật.
/// Đọc kỳ đã lưu trong <see cref="UtilityReading"/>, snapshot biểu giá điện/nước,
/// đơn giá dịch vụ đang hiệu lực và hóa đơn P6 để hiển thị đúng trạng thái kỳ.
/// Mọi thao tác ghi vẫn đi qua <see cref="UtilityReadingService"/> để giữ nguyên rule D10/SW-09.
/// </summary>
public sealed class MonthlyUtilityCalculationPageService(
    ApplicationDbContext db,
    UtilityReadingService readingService,
    IUtilityTariffService tariffService,
    P2CallerProvider callerProvider,
    Web.Features.Invoices.Services.IInvoiceService invoiceService,
    Microsoft.Extensions.Localization.IStringLocalizer<UtilityResources> localizer,
    ILogger<MonthlyUtilityCalculationPageService> logger)
{
    public const string ElectricScopeCode = "VN-EVN-HOUSEHOLD";
    public const string WaterScopeCode = "HCMC-BENTHANH";

    private static readonly string[] MeteredUnits = ["kwh", "m³", "m3"];
    private static readonly string[] PerPersonUnits = ["người", "nguoi", "person"];

    // Blazor có thể chạy nhiều event handler song song trên cùng một circuit; DbContext
    // là scoped nên không được phép có hai truy vấn chồng lấn. Xếp hàng mọi thao tác
    // đọc/ghi của trang để tránh lỗi "A second operation was started...".
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public async Task<MonthlyUtilityViewData> LoadAsync(
        int? propertyId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            return await LoadCoreAsync(propertyId, periodStart, periodEnd, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<MonthlyUtilityViewData> LoadCoreAsync(
        int? propertyId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        var caller = await callerProvider.GetCallerAsync();

        if (caller.IsAdmin)
        {
            return MonthlyUtilityViewData.Empty(periodStart, periodEnd);
        }

        var contracts = await LoadContractsAsync(caller, propertyId, periodStart, periodEnd, cancellationToken);
        var properties = await LoadPropertiesAsync(caller, cancellationToken);

        if (contracts.Count == 0)
        {
            return new MonthlyUtilityViewData([], properties, propertyId, periodStart, periodEnd);
        }

        var roomIds = contracts.Select(item => item.RoomId).Distinct().ToList();
        var contractIds = contracts.Select(item => item.ContractId).Distinct().ToList();

        var readings = await db.UtilityReadings
            .AsNoTracking()
            .Include(reading => reading.Audits)
            .Where(reading => roomIds.Contains(reading.RoomId))
            .ToListAsync(cancellationToken);

        // Bộ lọc có thể trải nhiều kỳ: lấy bản ghi chỉ số mới nhất trong khoảng lọc của
        // từng phòng thay vì đòi khớp đúng ngày bắt đầu (khi đó bảng sẽ trống).
        var exactReadings = readings
            .Where(reading => reading.PeriodStart >= periodStart && reading.PeriodStart <= periodEnd)
            .GroupBy(reading => reading.RoomId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(reading => reading.PeriodStart)
                    .ThenByDescending(reading => reading.Id)
                    .First());

        var readingIds = exactReadings.Values.Select(reading => reading.Id).ToList();
        var snapshots = await LoadSnapshotsAsync(readingIds, cancellationToken);
        var invoices = await LoadInvoicesAsync(contractIds, periodStart, periodEnd, cancellationToken);
        var invoicedPeriods = await LoadInvoicedPeriodsAsync(contractIds, cancellationToken);

        var tenantNames = await LoadTenantNamesAsync(contractIds, cancellationToken);
        var residentCounts = await LoadResidentCountsAsync(contractIds, cancellationToken);
        var servicesByProperty = await LoadServicePricesAsync(
            contracts.Select(item => item.PropertyId).Distinct().ToList(),
            periodStart,
            periodEnd,
            cancellationToken);

        // Khách thuê đăng ký dịch vụ theo hợp đồng/tháng: hợp đồng nào đã từng có đăng ký
        // được duyệt trong tháng thì chỉ tính đúng các dịch vụ còn hiệu lực. Nhờ vậy hủy
        // dịch vụ cuối cùng không làm toàn bộ dịch vụ bị tính lại (fallback chỉ áp cho
        // hợp đồng chưa từng đăng ký gì trong tháng).
        var rangeMonthStart = new DateOnly(periodStart.Year, periodStart.Month, 1);
        var rangeMonthEnd = new DateOnly(periodEnd.Year, periodEnd.Month, 1);
        var registrationRows = await db.ContractServiceRegistrations
            .AsNoTracking()
            .Where(item => contractIds.Contains(item.ContractId)
                && item.MonthStart >= rangeMonthStart
                && item.MonthStart <= rangeMonthEnd)
            .ToListAsync(cancellationToken);
        var registrationsByContractMonth = registrationRows
            .GroupBy(item => (item.ContractId, item.MonthStart))
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.ServiceCatalogId).ToHashSet());
        // Tháng đã từng có đăng ký được duyệt (kể cả đăng ký đã duyệt rồi bị hủy) vẫn được
        // tính là "đã đăng ký": nếu không, hủy dịch vụ cuối cùng sẽ làm fallback bật lại toàn bộ
        // dịch vụ của tòa.
        var registeredRequestMonths = (await db.ServiceRegistrationRequests
                .AsNoTracking()
                .Where(item => contractIds.Contains(item.ContractId)
                    && (item.Status == ServiceRegistrationStatus.Approved
                        || (item.Status == ServiceRegistrationStatus.Cancelled
                            && item.CancelStatus == ServiceRegistrationCancelStatus.Approved))
                    && item.StartDate >= rangeMonthStart
                    && item.StartDate <= periodEnd)
                .Select(item => new { item.ContractId, item.StartDate })
                .ToListAsync(cancellationToken))
            .Select(item => (item.ContractId, new DateOnly(item.StartDate.Year, item.StartDate.Month, 1)))
            .ToHashSet();

        var rows = new List<MonthlyUtilityRoomCostDto>(contracts.Count);
        foreach (var contract in contracts.OrderBy(item => item.RoomCode, StringComparer.OrdinalIgnoreCase))
        {
            exactReadings.TryGetValue(contract.RoomId, out var reading);

            // Kỳ hiển thị phải là kỳ thật của bản ghi chỉ số, không phải khoảng ngày
            // người dùng đang lọc (bộ lọc có thể rộng hơn một kỳ). Số liệu đầu kỳ lấy
            // theo kỳ thật đó để không lệch khi lọc nhiều tháng.
            var rowPeriodStart = reading?.PeriodStart ?? periodStart;
            var rowPeriodEnd = reading?.PeriodEnd ?? periodEnd;
            var invoice = invoices.GetValueOrDefault((contract.ContractId, rowPeriodStart));
            var previous = FindPreviousReading(readings, contract.RoomId, rowPeriodStart);
            var latest = FindLatestReading(readings, contract.RoomId);

            var isInvoiced = invoice is not null
                && invoice.Status is InvoiceStatus.Issued or InvoiceStatus.Paid;
            // Đã lập hóa đơn (kể cả nháp) thì khóa sửa chỉ số: nếu cho sửa, snapshot tính lại
            // sẽ lệch khỏi số tiền đã chốt trong hóa đơn (total vẫn lấy theo hóa đơn). Muốn sửa
            // phải hủy hóa đơn nháp bên P6 trước.
            var isAdjustable = caller.IsManager
                && reading is not null
                && latest?.Id == reading.Id
                && !isInvoiced
                && invoice is null;
            // "Sửa kỳ liền trước": chỉ khi kỳ trước vẫn là bản ghi mới nhất của phòng
            // (kỳ hiện tại chưa chốt) và chính kỳ liền trước chưa bị hóa đơn đã phát hành
            // phủ lên. Hóa đơn nháp kỳ hiện tại không chặn sửa kỳ trước vì hai kỳ độc lập.
            var previousInvoiced = previous is not null
                && invoicedPeriods.TryGetValue(contract.ContractId, out var periods)
                && periods.Any(period =>
                    period.Start <= previous.PeriodEnd && previous.PeriodStart <= period.End);
            var canCorrectPrevious = caller.IsManager
                && previous is not null
                && latest?.Id == previous.Id
                && !isInvoiced
                && !previousInvoiced;

            var electricStart = reading?.ElectricStart ?? previous?.ElectricEnd ?? 0m;
            var electricEnd = reading?.ElectricEnd ?? electricStart;
            var waterStart = reading?.WaterStart ?? previous?.WaterEnd ?? 0m;
            var waterEnd = reading?.WaterEnd ?? waterStart;
            var electricUsage = Math.Max(0m, electricEnd - electricStart);
            var waterUsage = Math.Max(0m, waterEnd - waterStart);
            var residentCount = residentCounts.GetValueOrDefault(contract.ContractId, 1);

            var electric = await ResolveUtilityCostAsync(
                reading,
                snapshots,
                TariffType.Electric,
                rowPeriodStart,
                rowPeriodEnd,
                electricUsage,
                residentCount,
                cancellationToken);
            var water = await ResolveUtilityCostAsync(
                reading,
                snapshots,
                TariffType.Water,
                rowPeriodStart,
                rowPeriodEnd,
                waterUsage,
                residentCount,
                cancellationToken);

            var propertyServices = servicesByProperty.GetValueOrDefault(contract.PropertyId, []);
            var rowMonthStart = new DateOnly(rowPeriodStart.Year, rowPeriodStart.Month, 1);
            var hasRegisteredServices = registrationsByContractMonth.TryGetValue(
                (contract.ContractId, rowMonthStart),
                out var registeredIds);
            if (hasRegisteredServices
                || registeredRequestMonths.Contains((contract.ContractId, rowMonthStart)))
            {
                propertyServices = propertyServices
                    .Where(version => registeredIds is not null && registeredIds.Contains(version.ServiceCatalogId))
                    .ToList();
            }

            var serviceLines = BuildServiceLines(propertyServices, residentCount);

            var rent = contract.SnapshotPrice;
            var electricCost = electric.Total;
            var waterCost = water.Total;
            var serviceCost = serviceLines.Sum(line => line.Amount);
            var total = rent + electricCost + waterCost + serviceCost;

            if (invoice is not null)
            {
                rent = SumInvoice(invoice, InvoiceItemType.Rent, rent);
                electricCost = SumInvoice(invoice, InvoiceItemType.Electric, electricCost);
                waterCost = SumInvoice(invoice, InvoiceItemType.Water, waterCost);
                serviceCost = SumInvoice(invoice, InvoiceItemType.Service, serviceCost);
                total = invoice.TotalAmount;
            }

            var state = invoice is null
                ? reading is null ? MonthlyUtilityPeriodState.Open : MonthlyUtilityPeriodState.Closed
                : isInvoiced ? MonthlyUtilityPeriodState.Invoiced : MonthlyUtilityPeriodState.Closed;

            rows.Add(new MonthlyUtilityRoomCostDto(
                contract.RoomId,
                contract.ContractId,
                contract.ContractNumber,
                contract.RoomCode,
                contract.PropertyId,
                contract.PropertyName,
                tenantNames.GetValueOrDefault(contract.ContractId, string.Empty),
                reading?.Id,
                previous?.Id,
                rowPeriodStart,
                rowPeriodEnd,
                electricStart,
                electricEnd,
                waterStart,
                waterEnd,
                electricUsage,
                waterUsage,
                reading is not null,
                rent,
                electricCost,
                waterCost,
                serviceCost,
                total,
                electric.UnitPrice,
                water.UnitPrice,
                electric.Tiers,
                water.Tiers,
                serviceLines,
                state,
                invoice?.Id,
                invoice?.Status,
                isAdjustable,
                canCorrectPrevious,
                caller.IsManager,
                previous is null
                    ? null
                    : new MonthlyUtilityPreviousReadingDto(
                        previous.Id,
                        previous.PeriodStart,
                        previous.PeriodEnd,
                        previous.ElectricStart,
                        previous.ElectricEnd,
                        previous.WaterStart,
                        previous.WaterEnd),
                BuildCorrections(reading)));
        }

        return new MonthlyUtilityViewData(rows, properties, propertyId, periodStart, periodEnd);
    }

    /// <summary>
    /// Lưu chỉ số lần đầu hoặc điều chỉnh kỳ gần nhất. Sau khi ghi, làm mới snapshot
    /// điện/nước của chính kỳ đó để chi phí đọc lại luôn khớp biểu giá đã áp dụng.
    /// </summary>
    public async Task<MonthlyUtilitySaveResult> SaveAsync(
        MonthlyUtilitySaveRequest request,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            return await SaveCoreAsync(request, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<MonthlyUtilitySaveResult> SaveCoreAsync(
        MonthlyUtilitySaveRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            return new MonthlyUtilitySaveResult(false, ErrorCode: UtilityReadingErrorCodes.Forbidden);
        }

        if (request.PeriodEnd < request.PeriodStart)
        {
            return new MonthlyUtilitySaveResult(false, ErrorCode: UtilityReadingErrorCodes.InvalidPeriod);
        }

        if (request.ElectricEnd < 0m || request.WaterEnd < 0m)
        {
            return new MonthlyUtilitySaveResult(false, ErrorCode: UtilityReadingErrorCodes.InvalidReading);
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            int readingId;
            if (request.ReadingId is { } existingReadingId)
            {
                await readingService.AdjustAsync(
                    existingReadingId,
                    new AdjustUtilityReadingRequest(request.ElectricEnd, request.WaterEnd, request.Reason ?? string.Empty),
                    caller,
                    cancellationToken);
                readingId = existingReadingId;
            }
            else
            {
                var duplicate = await db.UtilityReadings
                    .AsNoTracking()
                    .AnyAsync(
                        reading => reading.RoomId == request.RoomId
                            && reading.PeriodStart == request.PeriodStart,
                        cancellationToken);
                if (duplicate)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new MonthlyUtilitySaveResult(false, ErrorCode: "DuplicatePeriod");
                }

                var saved = await readingService.RecordAsync(
                    new RecordUtilityReadingRequest(
                        request.RoomId,
                        request.PeriodStart,
                        request.PeriodEnd,
                        request.ElectricEnd,
                        request.WaterEnd),
                    caller,
                    cancellationToken);
                readingId = saved.Id;
            }

            await RefreshSnapshotsAsync(readingId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new MonthlyUtilitySaveResult(true, readingId);
        }
        catch (UtilityReadingException exception)
        {
            if (exception.ErrorCode == UtilityReadingErrorCodes.ApprovalRequired)
            {
                return new MonthlyUtilitySaveResult(
                    false,
                    ErrorCode: exception.ErrorCode,
                    PendingApproval: true);
            }

            return new MonthlyUtilitySaveResult(false, ErrorCode: exception.ErrorCode);
        }
        catch (UtilityReadingAuthorizationException exception)
        {
            return new MonthlyUtilitySaveResult(false, ErrorCode: exception.ErrorCode);
        }
        catch (UtilityReadingNotFoundException exception)
        {
            return new MonthlyUtilitySaveResult(false, ErrorCode: exception.ErrorCode);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "P5 monthly utility save conflicted for room {RoomId}", request.RoomId);
            return new MonthlyUtilitySaveResult(false, ErrorCode: UtilityReadingErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException exception)
        {
            logger.LogWarning(exception, "P5 monthly utility save failed for room {RoomId}", request.RoomId);
            return new MonthlyUtilitySaveResult(false, ErrorCode: "DuplicatePeriod");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "P5 monthly utility save failed for room {RoomId}", request.RoomId);
            return new MonthlyUtilitySaveResult(false, ErrorCode: "SaveFailed");
        }
    }

    /// <summary>Điều chỉnh một kỳ đã có (dùng cho nút "Sửa kỳ liền trước").</summary>
    public async Task<MonthlyUtilitySaveResult> CorrectAsync(
        int readingId,
        decimal electricEnd,
        decimal waterEnd,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            return await CorrectCoreAsync(readingId, electricEnd, waterEnd, reason, cancellationToken);
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task<MonthlyUtilitySaveResult> CorrectCoreAsync(
        int readingId,
        decimal electricEnd,
        decimal waterEnd,
        string reason,
        CancellationToken cancellationToken)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            return new MonthlyUtilitySaveResult(false, ErrorCode: UtilityReadingErrorCodes.Forbidden);
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await readingService.AdjustAsync(
                readingId,
                new AdjustUtilityReadingRequest(electricEnd, waterEnd, reason),
                caller,
                cancellationToken);
            await RefreshSnapshotsAsync(readingId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new MonthlyUtilitySaveResult(true, readingId);
        }
        catch (UtilityReadingException exception)
        {
            return new MonthlyUtilitySaveResult(
                false,
                ErrorCode: exception.ErrorCode,
                PendingApproval: exception.ErrorCode == UtilityReadingErrorCodes.ApprovalRequired);
        }
        catch (UtilityReadingAuthorizationException exception)
        {
            return new MonthlyUtilitySaveResult(false, ErrorCode: exception.ErrorCode);
        }
        catch (UtilityReadingNotFoundException exception)
        {
            return new MonthlyUtilitySaveResult(false, ErrorCode: exception.ErrorCode);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(exception, "P5 previous-period correction conflicted for reading {ReadingId}", readingId);
            return new MonthlyUtilitySaveResult(false, ErrorCode: UtilityReadingErrorCodes.ConcurrencyConflict);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "P5 previous-period correction failed for reading {ReadingId}", readingId);
            return new MonthlyUtilitySaveResult(false, ErrorCode: "SaveFailed");
        }
    }

    /// <summary>
    /// Lập hóa đơn nháp thật (P6) từ số liệu P5 đã chốt của một phòng/kỳ.
    /// Chỉ Chủ trọ/Quản lý được gọi; khách thuê bị từ chối trong InvoiceService.
    /// Trả về Id hóa đơn để UI điều hướng tới trang chi tiết hóa đơn.
    /// </summary>
    public async Task<MonthlyUtilityInvoiceResult> CreateInvoiceAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        if (!caller.IsManager)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: UtilityReadingErrorCodes.Forbidden);
        }

        var view = await LoadAsync(null, periodStart, periodEnd, cancellationToken);
        var room = view.Rooms.FirstOrDefault(item => item.ContractId == contractId);
        if (room is null)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: "RoomNotFound");
        }

        if (room.InvoiceId is not null)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: "DuplicateInvoice");
        }

        if (!room.HasReadings)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: "MissingReadings");
        }

        var items = new List<Web.Features.Invoices.DTOs.InvoiceItemInputDto>();
        if (room.RentAmount > 0m)
        {
            items.Add(new Web.Features.Invoices.DTOs.InvoiceItemInputDto
            {
                ItemType = InvoiceItemType.Rent,
                Description = localizer["P5CostRentLabel"],
                Unit = "tháng",
                Quantity = 1m,
                UnitPrice = room.RentAmount,
                Amount = room.RentAmount,
            });
        }

        if (room.ElectricUsage > 0m && room.ElectricCost > 0m)
        {
            items.Add(new Web.Features.Invoices.DTOs.InvoiceItemInputDto
            {
                ItemType = InvoiceItemType.Electric,
                Description = $"{localizer["P5CostElectricSection"]} ({FormatRoomPeriod(room)})",
                Unit = "kWh",
                Quantity = room.ElectricUsage,
                UnitPrice = room.ElectricUnitPrice,
                Amount = room.ElectricCost,
            });
        }

        if (room.WaterUsage > 0m && room.WaterCost > 0m)
        {
            items.Add(new Web.Features.Invoices.DTOs.InvoiceItemInputDto
            {
                ItemType = InvoiceItemType.Water,
                Description = $"{localizer["P5CostWaterSection"]} ({FormatRoomPeriod(room)})",
                Unit = "m³",
                Quantity = room.WaterUsage,
                UnitPrice = room.WaterUnitPrice,
                Amount = room.WaterCost,
            });
        }

        foreach (var line in room.ServiceLines.Where(line => line.Amount > 0m))
        {
            items.Add(new Web.Features.Invoices.DTOs.InvoiceItemInputDto
            {
                ItemType = InvoiceItemType.Service,
                Description = line.Name,
                Unit = line.Unit,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                Amount = line.Amount,
            });
        }

        if (items.Count == 0)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: "InvalidItem");
        }

        try
        {
            var invoice = await invoiceService.CreateDraftAsync(
                new Web.Features.Invoices.DTOs.CreateInvoiceRequest
                {
                    ContractId = room.ContractId,
                    PeriodStart = room.PeriodStart,
                    PeriodEnd = room.PeriodEnd,
                    Items = items,
                },
                caller,
                cancellationToken);
            return new MonthlyUtilityInvoiceResult(true, invoice.Id);
        }
        catch (Web.Features.Invoices.InvoiceOperationException exception)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: exception.ErrorCode);
        }
        catch (Web.Features.Invoices.InvoiceValidationException exception)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: exception.ErrorCode);
        }
        catch (Web.Features.Invoices.InvoiceNotFoundException exception)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: exception.ErrorCode);
        }
        catch (Web.Features.Invoices.InvoiceAuthorizationException)
        {
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: UtilityReadingErrorCodes.Forbidden);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "P5 create invoice failed for contract {ContractId}", contractId);
            return new MonthlyUtilityInvoiceResult(false, ErrorCode: "SaveFailed");
        }
    }

    private static string FormatRoomPeriod(MonthlyUtilityRoomCostDto room) =>
        $"{room.PeriodStart:dd/MM/yyyy} - {room.PeriodEnd:dd/MM/yyyy}";

    private async Task RefreshSnapshotsAsync(int readingId, CancellationToken cancellationToken)
    {
        var reading = await db.UtilityReadings
            .AsNoTracking()
            .SingleAsync(item => item.Id == readingId, cancellationToken);

        var contract = await db.Contracts
            .AsNoTracking()
            .Where(item => item.RoomId == reading.RoomId
                && item.Status == ContractStatus.Approved
                && item.StartDate <= reading.PeriodEnd.ToDateTime(TimeOnly.MaxValue)
                && item.EndDate >= reading.PeriodStart.ToDateTime(TimeOnly.MinValue))
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        var residentCount = contract is null
            ? 1
            : Math.Max(1, await db.ContractMembers.CountAsync(
                member => member.ContractId == contract.ContractId,
                cancellationToken));

        var oldSnapshots = await db.UtilityBillingPeriodSnapshots
            .Include(snapshot => snapshot.CostLines)
            .Where(snapshot => snapshot.BillingPeriodId == readingId)
            .ToListAsync(cancellationToken);

        if (oldSnapshots.Count > 0)
        {
            db.UtilityBillingCostLineSnapshots.RemoveRange(oldSnapshots.SelectMany(snapshot => snapshot.CostLines));
            db.UtilityBillingPeriodSnapshots.RemoveRange(oldSnapshots);
            await db.SaveChangesAsync(cancellationToken);
        }

        var electricUsage = Math.Max(0m, reading.ElectricEnd - reading.ElectricStart);
        var waterUsage = Math.Max(0m, reading.WaterEnd - reading.WaterStart);

        if (electricUsage > 0m)
        {
            await tariffService.SnapshotForPeriodAsync(
                readingId,
                TariffType.Electric,
                ElectricScopeCode,
                reading.PeriodStart,
                reading.PeriodEnd,
                electricUsage,
                residentCount,
                cancellationToken);
        }

        if (waterUsage > 0m)
        {
            await tariffService.SnapshotForPeriodAsync(
                readingId,
                TariffType.Water,
                WaterScopeCode,
                reading.PeriodStart,
                reading.PeriodEnd,
                waterUsage,
                residentCount,
                cancellationToken);
        }
    }

    private async Task<IReadOnlyList<ContractRow>> LoadContractsAsync(
        PropertyCaller caller,
        int? propertyId,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        var start = periodStart.ToDateTime(TimeOnly.MinValue);
        var end = periodEnd.ToDateTime(TimeOnly.MaxValue);

        IQueryable<ContractRow> query;
        if (caller.IsKhachThue)
        {
            query = from contract in db.Contracts.AsNoTracking()
                    join member in db.ContractMembers.AsNoTracking()
                        on contract.ContractId equals member.ContractId
                    join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking()
                        on member.TenantId equals tenant.Id
                    join room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                        on contract.RoomId equals room.Id
                    join property in db.Properties.IgnoreQueryFilters().AsNoTracking()
                        on room.PropertyId equals property.Id
                    where tenant.UserId == caller.UserId
                        && contract.Status == ContractStatus.Approved
                        && contract.StartDate <= end
                        && contract.EndDate >= start
                        && !room.IsDeleted
                        && !property.IsDeleted
                        && (propertyId == null || room.PropertyId == propertyId)
                    select new ContractRow(
                        contract.ContractId,
                        contract.RoomId,
                        contract.ContractNumber,
                        room.Code,
                        property.Id,
                        property.Name,
                        contract.StartDate,
                        contract.EndDate,
                        contract.SnapshotPrice);
        }
        else
        {
            var propertyIds = PropertyScope.Apply(db.Properties, db.PropertyManagers, caller)
                .Select(property => property.Id);

            query = from contract in db.Contracts.AsNoTracking()
                    join room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                        on contract.RoomId equals room.Id
                    join property in db.Properties.IgnoreQueryFilters().AsNoTracking()
                        on room.PropertyId equals property.Id
                    where contract.Status == ContractStatus.Approved
                        && contract.StartDate <= end
                        && contract.EndDate >= start
                        && propertyIds.Contains(room.PropertyId)
                        && !room.IsDeleted
                        && !property.IsDeleted
                        && (propertyId == null || room.PropertyId == propertyId)
                    select new ContractRow(
                        contract.ContractId,
                        contract.RoomId,
                        contract.ContractNumber,
                        room.Code,
                        property.Id,
                        property.Name,
                        contract.StartDate,
                        contract.EndDate,
                        contract.SnapshotPrice);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return rows
            .GroupBy(row => row.ContractId)
            .Select(group => group.OrderByDescending(row => row.StartDate).First())
            .ToList();
    }

    private async Task<IReadOnlyList<MonthlyUtilityPropertyOptionDto>> LoadPropertiesAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        IQueryable<int> propertyIds;
        if (caller.IsKhachThue)
        {
            propertyIds = from member in db.ContractMembers.AsNoTracking()
                          join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking()
                              on member.TenantId equals tenant.Id
                          join contract in db.Contracts.AsNoTracking()
                              on member.ContractId equals contract.ContractId
                          join room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                              on contract.RoomId equals room.Id
                          where tenant.UserId == caller.UserId
                              && contract.Status == ContractStatus.Approved
                          select room.PropertyId;
        }
        else
        {
            propertyIds = PropertyScope.Apply(db.Properties, db.PropertyManagers, caller)
                .Select(property => property.Id);
        }

        return await db.Properties
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(property => propertyIds.Contains(property.Id) && !property.IsDeleted)
            .OrderBy(property => property.Name)
            .Select(property => new MonthlyUtilityPropertyOptionDto(property.Id, property.Name))
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, string>> LoadTenantNamesAsync(
        IReadOnlyList<Guid> contractIds,
        CancellationToken cancellationToken) =>
        (await db.ContractMembers
            .AsNoTracking()
            .Where(member => contractIds.Contains(member.ContractId))
            .Join(
                db.Tenants.IgnoreQueryFilters().AsNoTracking(),
                member => member.TenantId,
                tenant => tenant.Id,
                (member, tenant) => new { member.ContractId, tenant.FullName })
            .ToListAsync(cancellationToken))
        .GroupBy(item => item.ContractId)
        .ToDictionary(
            group => group.Key,
            group => string.Join(", ", group.Select(item => item.FullName).Distinct()));

    private async Task<Dictionary<Guid, int>> LoadResidentCountsAsync(
        IReadOnlyList<Guid> contractIds,
        CancellationToken cancellationToken) =>
        await db.ContractMembers
            .AsNoTracking()
            .Where(member => contractIds.Contains(member.ContractId))
            .GroupBy(member => member.ContractId)
            .Select(group => new { ContractId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ContractId, item => Math.Max(1, item.Count), cancellationToken);

    private async Task<Dictionary<(Guid ContractId, DateOnly PeriodStart), InvoiceRow>> LoadInvoicesAsync(
        IReadOnlyList<Guid> contractIds,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken) =>
        (await db.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Items)
            .Where(invoice => contractIds.Contains(invoice.ContractId)
                && invoice.PeriodStart >= periodStart
                && invoice.PeriodStart <= periodEnd
                // Chỉ hóa đơn theo tháng/P5 mới quyết trạng thái kỳ; hóa đơn dịch vụ
                // theo yêu cầu là hóa đơn riêng, không thay tổng tiền của kỳ.
                && invoice.RegistrationId == null)
            .ToListAsync(cancellationToken))
        .GroupBy(invoice => (invoice.ContractId, invoice.PeriodStart))
        .ToDictionary(
            group => group.Key,
            group => new InvoiceRow(group.OrderByDescending(invoice => invoice.Id).First()));

    private async Task<Dictionary<Guid, IReadOnlyList<InvoicedPeriodRow>>> LoadInvoicedPeriodsAsync(
        IReadOnlyList<Guid> contractIds,
        CancellationToken cancellationToken) =>
        (await db.Invoices
            .AsNoTracking()
            .Where(invoice => contractIds.Contains(invoice.ContractId)
                && invoice.RegistrationId == null
                && (invoice.Status == InvoiceStatus.Issued || invoice.Status == InvoiceStatus.Paid))
            .Select(invoice => new InvoicedPeriodRow(
                invoice.ContractId,
                invoice.PeriodStart,
                invoice.PeriodEnd))
            .ToListAsync(cancellationToken))
        .GroupBy(row => row.ContractId)
        .ToDictionary(group => group.Key, group => (IReadOnlyList<InvoicedPeriodRow>)group.ToList());

    private async Task<Dictionary<(int ReadingId, TariffType Type), UtilityBillingPeriodSnapshot>> LoadSnapshotsAsync(
        IReadOnlyList<int> readingIds,
        CancellationToken cancellationToken)
    {
        if (readingIds.Count == 0)
        {
            return [];
        }

        var snapshots = await db.UtilityBillingPeriodSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.CostLines)
            .Where(snapshot => readingIds.Contains(snapshot.BillingPeriodId))
            .ToListAsync(cancellationToken);

        return snapshots.ToDictionary(snapshot => (snapshot.BillingPeriodId, snapshot.Type));
    }

    private async Task<Dictionary<int, IReadOnlyList<ServiceVersionRow>>> LoadServicePricesAsync(
        IReadOnlyList<int> propertyIds,
        DateOnly periodStart,
        DateOnly periodEnd,
        CancellationToken cancellationToken)
    {
        if (propertyIds.Count == 0)
        {
            return [];
        }

        var scopedPropertyIds = propertyIds.Select(id => (int?)id).ToList();

        var versions = await db.ServicePriceHistories
            .AsNoTracking()
            .Include(version => version.ServiceCatalog)
            .Where(version => (scopedPropertyIds.Contains(version.ServiceCatalog.PropertyId)
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
            .ToList();

        var result = new Dictionary<int, IReadOnlyList<ServiceVersionRow>>();
        foreach (var propertyId in propertyIds)
        {
            result[propertyId] = effective
                .Where(version => version.ServiceCatalog.PropertyId == propertyId
                    || version.ServiceCatalog.PropertyId == null)
                .Select(version => new ServiceVersionRow(
                    version.ServiceCatalogId,
                    version.Name,
                    version.Unit,
                    version.UnitPrice))
                .ToList();
        }

        return result;
    }

    private async Task<UtilityCostResult> ResolveUtilityCostAsync(
        UtilityReading? reading,
        IReadOnlyDictionary<(int ReadingId, TariffType Type), UtilityBillingPeriodSnapshot> snapshots,
        TariffType type,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal usage,
        int residentCount,
        CancellationToken cancellationToken)
    {
        if (reading is not null
            && snapshots.TryGetValue((reading.Id, type), out var snapshot)
            && snapshot.CostLines.Count > 0)
        {
            var tiers = snapshot.CostLines
                .OrderBy(line => line.TierNumber)
                .Select(line => new MonthlyUtilityTierDto(
                    line.TierNumber,
                    line.From,
                    line.To,
                    line.Quantity,
                    line.UnitPrice,
                    line.Amount,
                    line.EffectiveDate))
                .ToList();
            var unitPrice = usage > 0m
                ? MoneyFormatter.RoundVnd(snapshot.Total / usage)
                : 0m;
            return new UtilityCostResult(snapshot.Total, unitPrice, tiers);
        }

        if (usage <= 0m)
        {
            return UtilityCostResult.Empty;
        }

        try
        {
            var tariff = await tariffService.CalculateForPeriodAsync(
                type,
                type == TariffType.Electric ? ElectricScopeCode : WaterScopeCode,
                periodStart,
                periodEnd,
                usage,
                residentCount,
                cancellationToken);

            return new UtilityCostResult(
                tariff.Total,
                usage > 0m ? MoneyFormatter.RoundVnd(tariff.Total / usage) : 0m,
                tariff.Tiers.Select(tier => new MonthlyUtilityTierDto(
                    tier.TierNumber,
                    tier.From,
                    tier.To,
                    tier.Quantity,
                    tier.Price,
                    tier.Amount,
                    tier.EffectiveFrom)).ToList());
        }
        catch (Exception exception)
        {
            // Không bao giờ suy diễn chi phí từ bảng giá hard-code: thiếu tariff nghĩa là
            // không đủ căn cứ tính tiền thật. Trả 0 kèm cảnh báo để vận hành bổ sung biểu giá VHT-51.
            logger.LogWarning(
                exception,
                "P5 could not load {TariffType} tariff for {PeriodStart}-{PeriodEnd}; cost shown as 0 until a real tariff is configured",
                type,
                periodStart,
                periodEnd);
            return UtilityCostResult.Empty;
        }
    }

    private static IReadOnlyList<MonthlyUtilityServiceLineDto> BuildServiceLines(
        IReadOnlyList<ServiceVersionRow> versions,
        int residentCount)
    {
        var lines = new List<MonthlyUtilityServiceLineDto>(versions.Count);
        foreach (var version in versions)
        {
            if (IsMeteredUnit(version.Unit))
            {
                continue;
            }

            var quantity = IsPerPersonUnit(version.Unit) ? residentCount : 1m;
            lines.Add(new MonthlyUtilityServiceLineDto(
                version.Name,
                version.Unit,
                quantity,
                version.UnitPrice,
                MoneyFormatter.RoundVnd(version.UnitPrice * quantity)));
        }

        return lines;
    }

    private static decimal SumInvoice(InvoiceRow invoice, InvoiceItemType type, decimal fallback) =>
        invoice.Items.Any(item => item.ItemType == type)
            ? invoice.Items.Where(item => item.ItemType == type).Sum(item => item.Amount)
            : fallback;

    private static IReadOnlyList<MonthlyUtilityCorrectionDto> BuildCorrections(UtilityReading? reading)
    {
        if (reading is null)
        {
            return [];
        }

        var corrections = new List<MonthlyUtilityCorrectionDto>(reading.Audits.Count * 2);
        foreach (var audit in reading.Audits.OrderByDescending(item => item.ChangedAt))
        {
            if (audit.OldElectricEnd != audit.NewElectricEnd)
            {
                corrections.Add(new MonthlyUtilityCorrectionDto(
                    "Electric",
                    audit.OldElectricEnd,
                    audit.NewElectricEnd,
                    audit.Reason,
                    audit.ChangedByUserId,
                    audit.ChangedAt));
            }

            if (audit.OldWaterEnd != audit.NewWaterEnd)
            {
                corrections.Add(new MonthlyUtilityCorrectionDto(
                    "Water",
                    audit.OldWaterEnd,
                    audit.NewWaterEnd,
                    audit.Reason,
                    audit.ChangedByUserId,
                    audit.ChangedAt));
            }
        }

        return corrections;
    }

    private static UtilityReading? FindPreviousReading(
        IReadOnlyList<UtilityReading> readings,
        int roomId,
        DateOnly periodStart) =>
        readings
            .Where(reading => reading.RoomId == roomId && reading.PeriodEnd < periodStart)
            .OrderByDescending(reading => reading.PeriodEnd)
            .ThenByDescending(reading => reading.Id)
            .FirstOrDefault();

    private static UtilityReading? FindLatestReading(
        IReadOnlyList<UtilityReading> readings,
        int roomId) =>
        readings
            .Where(reading => reading.RoomId == roomId)
            .OrderByDescending(reading => reading.PeriodStart)
            .ThenByDescending(reading => reading.Id)
            .FirstOrDefault();

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

    private sealed record ContractRow(
        Guid ContractId,
        int RoomId,
        string ContractNumber,
        string RoomCode,
        int PropertyId,
        string PropertyName,
        DateTime StartDate,
        DateTime EndDate,
        decimal SnapshotPrice);

    private sealed record ServiceVersionRow(int ServiceCatalogId, string Name, string Unit, decimal UnitPrice);

    private sealed record InvoicedPeriodRow(Guid ContractId, DateOnly Start, DateOnly End);

    private sealed record InvoiceRow(Web.Features.Invoices.Entities.Invoice Invoice)
    {
        public int Id => Invoice.Id;
        public InvoiceStatus Status => Invoice.Status;
        public decimal TotalAmount => Invoice.TotalAmount;
        public IReadOnlyList<Web.Features.Invoices.Entities.InvoiceItem> Items => Invoice.Items.ToList();
    }

    private sealed record UtilityCostResult(
        decimal Total,
        decimal UnitPrice,
        IReadOnlyList<MonthlyUtilityTierDto> Tiers)
    {
        public static UtilityCostResult Empty { get; } = new(0m, 0m, []);
    }
}

public enum MonthlyUtilityPeriodState
{
    Open,
    Closed,
    Invoiced,
}

public sealed record MonthlyUtilityPropertyOptionDto(int Id, string Name);

public sealed record MonthlyUtilityTierDto(
    int TierNumber,
    decimal From,
    decimal? To,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount,
    DateOnly? EffectiveDate);

public sealed record MonthlyUtilityServiceLineDto(
    string Name,
    string Unit,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record MonthlyUtilityCorrectionDto(
    string Field,
    decimal OldValue,
    decimal NewValue,
    string Reason,
    string ChangedByUserId,
    DateTimeOffset ChangedAt);

public sealed record MonthlyUtilityPreviousReadingDto(
    int Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal ElectricStart,
    decimal ElectricEnd,
    decimal WaterStart,
    decimal WaterEnd);

public sealed record MonthlyUtilityRoomCostDto(
    int RoomId,
    Guid ContractId,
    string ContractNumber,
    string RoomCode,
    int PropertyId,
    string PropertyName,
    string TenantNames,
    int? ReadingId,
    int? PreviousReadingId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal ElectricStart,
    decimal ElectricEnd,
    decimal WaterStart,
    decimal WaterEnd,
    decimal ElectricUsage,
    decimal WaterUsage,
    bool HasReadings,
    decimal RentAmount,
    decimal ElectricCost,
    decimal WaterCost,
    decimal ServiceCost,
    decimal TotalAmount,
    decimal ElectricUnitPrice,
    decimal WaterUnitPrice,
    IReadOnlyList<MonthlyUtilityTierDto> ElectricTiers,
    IReadOnlyList<MonthlyUtilityTierDto> WaterTiers,
    IReadOnlyList<MonthlyUtilityServiceLineDto> ServiceLines,
    MonthlyUtilityPeriodState State,
    int? InvoiceId,
    InvoiceStatus? InvoiceStatus,
    bool CanAdjust,
    bool CanCorrectPrevious,
    bool CanManage,
    MonthlyUtilityPreviousReadingDto? PreviousReading,
    IReadOnlyList<MonthlyUtilityCorrectionDto> Corrections)
{
    public bool IsInvoiced => State == MonthlyUtilityPeriodState.Invoiced;
}

public sealed record MonthlyUtilityViewData(
    IReadOnlyList<MonthlyUtilityRoomCostDto> Rooms,
    IReadOnlyList<MonthlyUtilityPropertyOptionDto> Properties,
    int? PropertyId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd)
{
    public static MonthlyUtilityViewData Empty(DateOnly periodStart, DateOnly periodEnd) =>
        new([], [], null, periodStart, periodEnd);
}

public sealed record MonthlyUtilitySaveRequest(
    int RoomId,
    Guid ContractId,
    int? ReadingId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal ElectricEnd,
    decimal WaterEnd,
    string? Reason);

public sealed record MonthlyUtilitySaveResult(
    bool Success,
    int? ReadingId = null,
    string? ErrorCode = null,
    bool PendingApproval = false);

public sealed record MonthlyUtilityInvoiceResult(
    bool Success,
    int? InvoiceId = null,
    string? ErrorCode = null);
