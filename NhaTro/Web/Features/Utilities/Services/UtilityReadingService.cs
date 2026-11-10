using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Common.Utils;
using Web.Data;
using Web.Data.Tariffs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Features.Properties;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Enums;
using Web.Features.Utilities.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Utilities.Services;

/// <summary>
/// VHT-51 — ghi chỉ số/sử dụng/tính chi phí kỳ thật.
/// Toàn bộ số liệu lấy từ hợp đồng, kỳ trước và biểu giá đã khóa (VHT-100);
/// điện/nước/dịch vụ được chốt thành snapshot bất biến, sau đó mới chuyển cho P6 lập hóa đơn.
/// </summary>
public sealed class UtilityReadingService(
    ApplicationDbContext context,
    IUtilityTariffService tariffService,
    TimeProvider? timeProvider = null) : IUtilityReadingService
{
    /// <summary>Biểu giá điện sinh hoạt EVN đã khóa (VHT-100).</summary>
    public const string ElectricScopeCode = "VN-EVN-HOUSEHOLD";

    /// <summary>Biểu giá nước sinh hoạt Bến Thành đã khóa (VHT-100).</summary>
    public const string WaterScopeCode = "HCMC-BENTHANH";

    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    public async Task<UtilityReadingPeriodDto> OpenPeriodAsync(
        Guid contractId,
        DateOnly periodStart,
        DateOnly periodEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var authorizedContract = await LoadManageableContractAsync(contractId, caller, cancellationToken);
        ValidateContractAndPeriod(authorizedContract, periodStart, periodEnd);

        // UPDLOCK/HOLDLOCK trên cùng hàng hợp đồng tuần tự hóa mọi lần mở kỳ của hợp đồng đó,
        // kể cả hai kỳ có ngày bắt đầu khác nhau nhưng chồng nhau. Khóa trong bộ nhớ không đủ
        // vì ứng dụng có thể chạy nhiều process.
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var contract = await LockContractAsync(contractId, cancellationToken);
        ValidateContractAndPeriod(contract, periodStart, periodEnd);

        var existing = await context.UtilityReadingPeriods
            .SingleOrDefaultAsync(
                period => period.ContractId == contractId && period.PeriodStart == periodStart,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.PeriodEnd != periodEnd)
            {
                throw new UtilityReadingException(
                    UtilityReadingErrorMessages.DuplicatePeriod,
                    nameof(UtilityReadingErrorCode.DuplicatePeriod));
            }

                await transaction.CommitAsync(cancellationToken);
                return await ToDtoAsync(existing, cancellationToken);
        }

        // Không mở kỳ chen trước kỳ đã có: chuỗi chỉ số đầu/cuối phải tiến theo thời gian.
        var hasLaterPeriod = await context.UtilityReadingPeriods
            .AnyAsync(
                period => period.ContractId == contractId && period.PeriodStart > periodStart,
                cancellationToken);
        if (hasLaterPeriod)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.PeriodHasLaterPeriod,
                nameof(UtilityReadingErrorCode.PeriodHasLaterPeriod));
        }

        var overlapping = await context.UtilityReadingPeriods
            .AnyAsync(
                period => period.ContractId == contractId
                    && period.PeriodStart <= periodEnd
                    && period.PeriodEnd >= periodStart,
                cancellationToken);
        if (overlapping)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.OverlappingPeriod,
                nameof(UtilityReadingErrorCode.OverlappingPeriod));
        }

        // Chỉ số đầu kỳ = chỉ số cuối kỳ liền trước đã lưu (không lấy từ dữ liệu mẫu).
        var previous = await FindPreviousPeriodAsync(contractId, periodStart, cancellationToken);

        // Kỳ liền trước chưa ghi chỉ số thì chưa có mốc đầu kỳ: mặc định 0 sẽ làm sai
        // dây chuyền chỉ số, nên chặn để người dùng ghi chỉ số kỳ trước trước.
        if (previous is not null && !previous.HasReadings)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.PreviousPeriodHasNoReadings,
                nameof(UtilityReadingErrorCode.PreviousPeriodHasNoReadings));
        }

        var residentCount = await context.ContractMembers
            .CountAsync(member => member.ContractId == contractId, cancellationToken);
        if (residentCount <= 0)
        {
            residentCount = 1;
        }

        var now = _clock.UtcNow();
        var electricOpening = previous?.ElectricEnd ?? 0m;
        var waterOpening = previous?.WaterEnd ?? 0m;
        var created = new UtilityReadingPeriod
        {
            ContractId = contractId,
            RoomId = contract.RoomId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            ElectricStart = electricOpening,
            ElectricEnd = electricOpening,
            WaterStart = waterOpening,
            WaterEnd = waterOpening,
            ResidentCount = residentCount,
            Status = UtilityReadingPeriodStatus.Open,
            HasReadings = false,
            CreatedAt = now,
            UpdatedAt = now,
        };

        context.UtilityReadingPeriods.Add(created);

        try
        {
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // Hai luồng mở cùng kỳ: unique (ContractId, PeriodStart) là chốt chặn cuối.
            context.Entry(created).State = EntityState.Detached;
            var duplicate = await context.UtilityReadingPeriods
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    period => period.ContractId == contractId && period.PeriodStart == periodStart,
                    cancellationToken);
            if (duplicate is not null && duplicate.PeriodEnd == periodEnd)
            {
                await transaction.RollbackAsync(cancellationToken);
                return await ToDtoAsync(duplicate, cancellationToken);
            }

            throw new UtilityReadingException(
                UtilityReadingErrorMessages.DuplicatePeriod,
                nameof(UtilityReadingErrorCode.DuplicatePeriod),
                exception);
        }
        catch (DbUpdateException)
        {
            // Lỗi khác (FK, check constraint, deadlock...) không phải trùng kỳ: giữ nguyên lỗi gốc.
            context.Entry(created).State = EntityState.Detached;
            throw;
        }

        return await ToDtoAsync(created, cancellationToken);
    }

    public async Task<UtilityReadingPeriodDto> SaveReadingsAsync(
        int periodId,
        decimal electricEnd,
        decimal waterEnd,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var period = await LoadManageablePeriodAsync(periodId, caller, cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await LockContractAsync(period.ContractId, cancellationToken);
        await context.Entry(period).ReloadAsync(cancellationToken);

        // D10: kỳ đã phát hành hóa đơn thì khóa vĩnh viễn. Kiểm cả InvoiceId vì tiền lịch sử
        // được đọc từ hóa đơn đã chốt, không phụ thuộc trạng thái được cập nhật cùng lúc hay không.
        if (period.Status != UtilityReadingPeriodStatus.Open || period.InvoiceId is not null)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.PeriodLocked,
                nameof(UtilityReadingErrorCode.PeriodLocked));
        }

        // VHT-51 chỉ ghi chỉ số lần đầu; sửa chỉ số kỳ liền trước (kèm tính lại dây chuyền)
        // thuộc VHT-102 nên trả mã riêng để bàn giao, không âm thầm đổi tiền đã chốt.
        if (period.HasReadings)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.PeriodAlreadyHasReadings,
                nameof(UtilityReadingErrorCode.PeriodAlreadyHasReadings));
        }

        // Hai kỳ có thể được mở chen nhau (ví dụ 10 và 11 mở gần như cùng lúc): kỳ 11 lúc mở
        // chưa thấy chỉ số kỳ 10 nên đầu kỳ đang là 0. Trước khi ghi, lấy lại mốc cuối kỳ liền
        // trước đã lưu để dây chuyền chỉ số luôn đúng; nếu kỳ trước chưa đọc thì chặn như lúc mở.
        var targetElectricStart = period.ElectricStart;
        var targetWaterStart = period.WaterStart;
        var previous = await FindPreviousPeriodAsync(
            period.ContractId,
            period.PeriodStart,
            cancellationToken);
        if (previous is not null)
        {
            if (!previous.HasReadings)
            {
                throw new UtilityReadingException(
                    UtilityReadingErrorMessages.PreviousPeriodHasNoReadings,
                    nameof(UtilityReadingErrorCode.PreviousPeriodHasNoReadings));
            }

            targetElectricStart = previous.ElectricEnd;
            targetWaterStart = previous.WaterEnd;
        }

        var issues = UtilityReadingRules.ValidateReadings(
            targetElectricStart,
            electricEnd,
            targetWaterStart,
            waterEnd);
        if (issues.Count > 0)
        {
            throw FromIssue(issues[0]);
        }

        period.ElectricStart = targetElectricStart;
        period.WaterStart = targetWaterStart;
        var now = _clock.UtcNow();
        period.ElectricEnd = UtilityTariffRules.NormalizeQuantity(electricEnd);
        period.WaterEnd = UtilityTariffRules.NormalizeQuantity(waterEnd);
        period.HasReadings = true;
        period.UpdatedAt = now;

        var electricUsage = UtilityTariffRules.NormalizeQuantity(
            UtilityReadingRules.CalculateUsage(period.ElectricStart, period.ElectricEnd));
        var waterUsage = UtilityTariffRules.NormalizeQuantity(
            UtilityReadingRules.CalculateUsage(period.WaterStart, period.WaterEnd));

        try
        {
            await context.SaveChangesAsync(cancellationToken);

            if (electricUsage > 0m)
            {
                await tariffService.SnapshotForPeriodAsync(
                    period.Id,
                    TariffType.Electric,
                    ElectricScopeCode,
                    period.PeriodStart,
                    period.PeriodEnd,
                    electricUsage,
                    period.ResidentCount,
                    cancellationToken);
            }

            if (waterUsage > 0m)
            {
                await tariffService.SnapshotForPeriodAsync(
                    period.Id,
                    TariffType.Water,
                    WaterScopeCode,
                    period.PeriodStart,
                    period.PeriodEnd,
                    waterUsage,
                    period.ResidentCount,
                    cancellationToken);
            }

            var serviceSnapshots = await LoadServiceSnapshotsForCurrentCatalogAsync(
                period, includeInactiveCatalogs: false, cancellationToken);
            context.UtilityServicePeriodSnapshots.AddRange(serviceSnapshots);
            period.HasServiceSnapshots = true;
            await context.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            // Rollback không hoàn nguyên trạng thái entity trong bộ nhớ: nếu giữ lại, lần thử tiếp
            // theo trong cùng scope sẽ thấy HasReadings = true dù DB chưa lưu.
            context.ChangeTracker.Clear();
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.ConcurrencyConflict,
                nameof(UtilityReadingErrorCode.ConcurrencyConflict),
                exception);
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            // Cùng lý do: bỏ các thay đổi dở dang để context không bị "nhiễm" sau thất bại.
            context.ChangeTracker.Clear();
            throw;
        }

        return await ToDtoAsync(period, cancellationToken);
    }

    private static async Task RollbackQuietlyAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // Giữ nguyên exception gốc của nghiệp vụ nếu rollback cũng lỗi.
        }
    }

    public async Task<UtilityReadingPeriodDto?> GetPeriodAsync(
        int periodId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var period = await FindManageablePeriodAsync(periodId, caller, cancellationToken);
        return period is null ? null : await ToDtoAsync(period, cancellationToken);
    }

    public async Task<UtilityReadingPeriodDto?> GetPeriodByContractAsync(
        Guid contractId,
        DateOnly periodStart,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var inScope = await ManageableContracts(caller)
            .AnyAsync(contract => contract.ContractId == contractId, cancellationToken);
        if (!inScope)
        {
            return null;
        }

        var period = await context.UtilityReadingPeriods
            .SingleOrDefaultAsync(
                item => item.ContractId == contractId && item.PeriodStart == periodStart,
                cancellationToken);

        return period is null ? null : await ToDtoAsync(period, cancellationToken);
    }

    public async Task<int> BackfillServiceSnapshotsAsync(
        int periodId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var period = await LoadManageablePeriodAsync(periodId, caller, cancellationToken);
        if (period.InvoiceId is not null)
        {
            throw new UtilityReadingException(UtilityReadingErrorMessages.PeriodLocked, nameof(UtilityReadingErrorCode.PeriodLocked));
        }

        if (!period.HasReadings)
        {
            throw new UtilityReadingException(UtilityReadingErrorMessages.PeriodHasNoReadings, nameof(UtilityReadingErrorCode.PeriodHasNoReadings));
        }

        if (period.HasServiceSnapshots)
        {
            return 0;
        }

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockContractAsync(period.ContractId, cancellationToken);
            await context.Entry(period).ReloadAsync(cancellationToken);
            if (period.InvoiceId is not null)
            {
                throw new UtilityReadingException(UtilityReadingErrorMessages.PeriodLocked, nameof(UtilityReadingErrorCode.PeriodLocked));
            }

            if (!period.HasReadings)
            {
                throw new UtilityReadingException(UtilityReadingErrorMessages.PeriodHasNoReadings, nameof(UtilityReadingErrorCode.PeriodHasNoReadings));
            }

            if (period.HasServiceSnapshots)
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }
            // Price history is immutable. Include a catalog disabled after the legacy period so
            // backfill does not silently discard a historical service line.
            var snapshots = await LoadServiceSnapshotsForCurrentCatalogAsync(
                period, includeInactiveCatalogs: true, cancellationToken);
            context.UtilityServicePeriodSnapshots.AddRange(snapshots);
            period.HasServiceSnapshots = true;
            period.UpdatedAt = _clock.UtcNow();
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return snapshots.Count;
        }
        catch
        {
            await RollbackQuietlyAsync(transaction, cancellationToken);
            context.ChangeTracker.Clear();
            throw;
        }
    }

    public async Task<CreateInvoiceRequest> BuildInvoiceRequestAsync(
        int periodId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var period = await LoadManageablePeriodAsync(periodId, caller, cancellationToken);

        // Chưa ghi chỉ số thì chưa có nguồn điện/nước đã chốt, không được chuyển cho P6.
        // Kỳ đã phát hành (có InvoiceId) luôn có chỉ số nên chỉ cần chặn kỳ còn mở.
        if (period.InvoiceId is null && !period.HasReadings)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.PeriodHasNoReadings,
                nameof(UtilityReadingErrorCode.PeriodHasNoReadings));
        }

        // Các kỳ được tạo trước migration không có snapshot dịch vụ. Không được dựng nguồn
        // hóa đơn bằng danh mục hiện hành vì trạng thái bật/tắt khi kỳ được chốt đã mất.
        // Chỉ kỳ đã phát hành mới đọc từ InvoiceItems nên không cần snapshot P5 này.
        if (period.InvoiceId is null && !period.HasServiceSnapshots)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.ServiceSnapshotsMissing,
                nameof(UtilityReadingErrorCode.ServiceSnapshotsMissing));
        }

        return await BuildInvoiceRequestAsync(period, cancellationToken);
    }

    private async Task<CreateInvoiceRequest> BuildInvoiceRequestAsync(
        UtilityReadingPeriod period,
        CancellationToken cancellationToken)
    {
        var contract = await context.Contracts
            .AsNoTracking()
            .SingleAsync(item => item.ContractId == period.ContractId, cancellationToken);

        // Kỳ đã phát hành: trả lại đúng nguồn đã chốt trong hóa đơn, không đọc lại giá hiện tại.
        if (period.InvoiceId is { } invoiceId)
        {
            await LoadMatchingInvoiceAsync(period, invoiceId, cancellationToken);
            var storedItems = await context.InvoiceItems
                .AsNoTracking()
                .Where(item => item.InvoiceId == invoiceId)
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);

            return new CreateInvoiceRequest
            {
                ContractId = period.ContractId,
                PeriodStart = period.PeriodStart,
                PeriodEnd = period.PeriodEnd,
                Items = storedItems.Select(ToInvoiceInput).ToList(),
            };
        }

        var snapshots = await LoadSnapshotsAsync(period.Id, cancellationToken);
        var items = await ComposeSettledItemsAsync(period, contract, snapshots, cancellationToken);
        var normalized = InvoiceDraftRules.NormalizeItems(items);

        return new CreateInvoiceRequest
        {
            ContractId = period.ContractId,
            PeriodStart = period.PeriodStart,
            PeriodEnd = period.PeriodEnd,
            Items = normalized,
        };
    }

    /// <summary>
    /// Ghép nguồn chi phí đã chốt: tiền thuê theo hợp đồng, điện/nước theo snapshot kỳ,
    /// dịch vụ theo snapshot khi đã ghi chỉ số (dữ liệu legacy trước migration mới đọc cách cũ).
    /// </summary>
    private async Task<List<InvoiceItemInputDto>> ComposeSettledItemsAsync(
        UtilityReadingPeriod period,
        Contract contract,
        IReadOnlyList<UtilityBillingPeriodSnapshot> snapshots,
        CancellationToken cancellationToken)
    {
        var items = new List<InvoiceItemInputDto>(6)
        {
            new()
            {
                ItemType = InvoiceItemType.Rent,
                Description = $"Tiền thuê {PeriodLabel(period)}",
                Unit = "tháng",
                Quantity = 1m,
                UnitPrice = contract.SnapshotPrice,
                Amount = contract.SnapshotPrice,
            },
        };

        AddUtilityItem(items, snapshots, TariffType.Electric, "Điện", period);
        AddUtilityItem(items, snapshots, TariffType.Water, "Nước", period);
        items.AddRange(await LoadServiceItemsAsync(period, cancellationToken));

        return items;
    }

    private static void AddUtilityItem(
        List<InvoiceItemInputDto> items,
        IReadOnlyList<UtilityBillingPeriodSnapshot> snapshots,
        TariffType type,
        string label,
        UtilityReadingPeriod period)
    {
        var snapshot = snapshots.SingleOrDefault(item => item.Type == type);
        if (snapshot is null || snapshot.Quantity <= 0m)
        {
            return;
        }

        // Điện/nước tính theo bậc nên giữ nguyên tổng đã snapshot; đơn giá chỉ là giá bình quân để hiển thị.
        var averagePrice = MoneyFormatter.RoundVnd(snapshot.Total / snapshot.Quantity);
        items.Add(new InvoiceItemInputDto
        {
            ItemType = type == TariffType.Electric ? InvoiceItemType.Electric : InvoiceItemType.Water,
            Description = $"{label} {PeriodLabel(period)}",
            Unit = snapshot.Unit,
            Quantity = snapshot.Quantity,
            UnitPrice = averagePrice,
            Amount = snapshot.Total,
        });
    }

    private async Task<IReadOnlyList<InvoiceItemInputDto>> LoadServiceItemsAsync(
        UtilityReadingPeriod period,
        CancellationToken cancellationToken)
    {
        if (period.HasServiceSnapshots)
        {
            return await context.UtilityServicePeriodSnapshots
                .AsNoTracking()
                .Where(snapshot => snapshot.UtilityReadingPeriodId == period.Id)
                .OrderBy(snapshot => snapshot.Description)
                .ThenBy(snapshot => snapshot.Id)
                .Select(snapshot => new InvoiceItemInputDto
                {
                    ItemType = InvoiceItemType.Service,
                    Description = snapshot.Description,
                    Unit = snapshot.Unit,
                    Quantity = snapshot.Quantity,
                    UnitPrice = snapshot.UnitPrice,
                    Amount = snapshot.Amount,
                })
                .ToListAsync(cancellationToken);
        }

        // A legacy period has no trustworthy service snapshot. Its DTO is explicitly marked
        // incomplete and must not expose a reconstructed current-catalog amount as history.
        return [];
    }

    private async Task<List<UtilityServicePeriodSnapshot>> LoadServiceSnapshotsForCurrentCatalogAsync(
        UtilityReadingPeriod period,
        bool includeInactiveCatalogs,
        CancellationToken cancellationToken)
    {
        var currentItems = await LoadCurrentServiceItemsWithSourceAsync(
            period, includeInactiveCatalogs, cancellationToken);
        return currentItems.Select(item => new UtilityServicePeriodSnapshot
        {
            UtilityReadingPeriodId = period.Id,
            SourceServiceCatalogId = item.ServiceCatalogId,
            SourceServicePriceHistoryId = item.ServicePriceHistoryId,
            Description = item.Description,
            Unit = item.Unit,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            Amount = item.Amount,
        }).ToList();
    }

    private async Task<List<ServiceItemSource>> LoadCurrentServiceItemsWithSourceAsync(
        UtilityReadingPeriod period,
        bool includeInactiveCatalogs,
        CancellationToken cancellationToken)
    {
        // Phòng có thể bị xóa mềm sau khi mở kỳ; kỳ đã chụp RoomId nên vẫn phải tra được tòa
        // để không làm rơi các dòng dịch vụ của hóa đơn lịch sử.
        var propertyId = await context.Rooms
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(room => room.Id == period.RoomId)
            .Select(room => (int?)room.PropertyId)
            .SingleOrDefaultAsync(cancellationToken);
        if (propertyId is null)
        {
            return [];
        }

        // Snapshot uses the catalog version effective on the period start; later catalog edits
        // must not mutate the values already captured for this period.
        var versions = await context.ServicePriceHistories
            .AsNoTracking()
            .Where(version => (version.ServiceCatalog.PropertyId == propertyId
                    || version.ServiceCatalog.PropertyId == null)
                && (includeInactiveCatalogs || version.ServiceCatalog.IsActive)
                && !version.ServiceCatalog.IsReadOnly
                && version.EffectiveDate <= period.PeriodStart)
            .ToListAsync(cancellationToken);

        var effective = versions
            .Where(version => version.EffectiveTo is null || version.EffectiveTo >= period.PeriodStart)
            .GroupBy(version => version.ServiceCatalogId)
            .Select(group => group
                .OrderByDescending(version => version.EffectiveDate)
                .ThenByDescending(version => version.Id)
                .First())
            .OrderBy(version => version.Name)
            .ToList();

        var items = new List<ServiceItemSource>(effective.Count);
        foreach (var version in effective)
        {
            var unitKind = ServiceUnitNormalizer.Classify(version.Unit);
            if (unitKind == ServiceUnitKind.Metered)
            {
                // Điện/nước đã chốt theo biểu giá khóa, không cộng trùng qua danh mục dịch vụ.
                continue;
            }

            // ResidentCount is captured at opening; non-person units are charged once.
            var quantity = unitKind == ServiceUnitKind.PerPerson ? period.ResidentCount : 1m;
            var amount = MoneyFormatter.RoundVnd(version.UnitPrice * quantity);
            items.Add(new ServiceItemSource(
                version.ServiceCatalogId,
                version.Id,
                version.Name,
                version.Unit,
                quantity,
                version.UnitPrice,
                amount));
        }

        return items;
    }

    private async Task<IReadOnlyList<UtilityBillingPeriodSnapshot>> LoadSnapshotsAsync(
        int periodId,
        CancellationToken cancellationToken) =>
        await context.UtilityBillingPeriodSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.CostLines)
            .Where(snapshot => snapshot.BillingPeriodId == periodId)
            .OrderBy(snapshot => snapshot.Type)
            .ToListAsync(cancellationToken);

    /// <summary>Kỳ liền trước theo thời gian (kỳ có ngày kết thúc sớm hơn ngày bắt đầu kỳ này).</summary>
    private Task<UtilityReadingPeriod?> FindPreviousPeriodAsync(
        Guid contractId,
        DateOnly periodStart,
        CancellationToken cancellationToken) =>
        context.UtilityReadingPeriods
            .AsNoTracking()
            .Where(period => period.ContractId == contractId && period.PeriodEnd < periodStart)
            .OrderByDescending(period => period.PeriodEnd)
            .ThenByDescending(period => period.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // EF cannot express UPDLOCK/HOLDLOCK. This raw SQL locks only the target contract to serialize
    // competing period/read operations; it does not broaden the authorization query.
    private Task<Contract> LockContractAsync(Guid contractId, CancellationToken cancellationToken) =>
        context.Contracts.FromSqlInterpolated(
                $"SELECT * FROM [Contracts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContractId] = {contractId}")
            .SingleAsync(cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is SqlException { Number: 2601 or 2627 };

    private async Task<UtilityReadingPeriodDto> ToDtoAsync(
        UtilityReadingPeriod period,
        CancellationToken cancellationToken)
    {
        var snapshots = await LoadSnapshotsAsync(period.Id, cancellationToken);

        var electric = snapshots.SingleOrDefault(snapshot => snapshot.Type == TariffType.Electric);
        var water = snapshots.SingleOrDefault(snapshot => snapshot.Type == TariffType.Water);

        decimal rentAmount;
        decimal electricCost;
        decimal waterCost;
        decimal serviceCost;
        decimal totalAmount;

        if (period.InvoiceId is { } invoiceId)
        {
            await LoadMatchingInvoiceAsync(period, invoiceId, cancellationToken);
            // D10: kỳ đã phát hành thì tiền hiển thị phải lấy đúng dòng hóa đơn đã chốt,
            // không đọc lại danh mục dịch vụ hiện tại (tránh âm thầm đổi tiền lịch sử).
            var invoiceItems = await context.InvoiceItems
                .AsNoTracking()
                .Where(item => item.InvoiceId == invoiceId)
                .ToListAsync(cancellationToken);

            rentAmount = SumOf(invoiceItems, InvoiceItemType.Rent);
            electricCost = SumOf(invoiceItems, InvoiceItemType.Electric);
            waterCost = SumOf(invoiceItems, InvoiceItemType.Water);
            serviceCost = SumOf(invoiceItems, InvoiceItemType.Service);
            totalAmount = invoiceItems.Sum(item => item.Amount);
        }
        else
        {
            var contract = await context.Contracts
                .AsNoTracking()
                .SingleAsync(item => item.ContractId == period.ContractId, cancellationToken);
            var items = await ComposeSettledItemsAsync(period, contract, snapshots, cancellationToken);
            rentAmount = SumOf(items, InvoiceItemType.Rent);
            electricCost = SumOf(items, InvoiceItemType.Electric);
            waterCost = SumOf(items, InvoiceItemType.Water);
            serviceCost = SumOf(items, InvoiceItemType.Service);
            totalAmount = InvoiceCalculationRules.CalculateTotal(items);
        }

        return new UtilityReadingPeriodDto(
            period.Id,
            period.ContractId,
            period.RoomId,
            period.PeriodStart,
            period.PeriodEnd,
            period.ElectricStart,
            period.ElectricEnd,
            period.WaterStart,
            period.WaterEnd,
            UtilityReadingRules.CalculateUsage(period.ElectricStart, period.ElectricEnd),
            UtilityReadingRules.CalculateUsage(period.WaterStart, period.WaterEnd),
            period.ResidentCount,
            period.Status,
            period.InvoiceId,
            period.HasReadings,
            rentAmount,
            electricCost,
            waterCost,
            serviceCost,
            totalAmount,
            electric?.Id,
            water?.Id,
            period.InvoiceId is null && !period.HasServiceSnapshots);
    }

    private sealed record ServiceItemSource(
        int ServiceCatalogId,
        int ServicePriceHistoryId,
        string Description,
        string Unit,
        decimal Quantity,
        decimal UnitPrice,
        decimal Amount);

    private async Task<Invoice> LoadMatchingInvoiceAsync(
        UtilityReadingPeriod period,
        int invoiceId,
        CancellationToken cancellationToken)
    {
        var invoice = await context.Invoices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null || invoice.ContractId != period.ContractId || invoice.PeriodStart != period.PeriodStart || invoice.PeriodEnd != period.PeriodEnd)
        {
            throw new UtilityReadingException(UtilityReadingErrorMessages.InvoicePeriodMismatch, nameof(UtilityReadingErrorCode.InvoicePeriodMismatch));
        }

        return invoice;
    }

    private async Task<UtilityReadingPeriod> LoadManageablePeriodAsync(
        int periodId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var period = await FindManageablePeriodAsync(periodId, caller, cancellationToken);
        if (period is null)
        {
            throw new UtilityReadingNotFoundException(
                UtilityReadingErrorMessages.PeriodNotFound,
                nameof(UtilityReadingErrorCode.PeriodNotFound));
        }

        return period;
    }

    private async Task<UtilityReadingPeriod?> FindManageablePeriodAsync(
        int periodId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var contractIds = ManageableContracts(caller).Select(contract => contract.ContractId);
        return await context.UtilityReadingPeriods
            .SingleOrDefaultAsync(
                period => period.Id == periodId && contractIds.Contains(period.ContractId),
                cancellationToken);
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
            throw new UtilityReadingNotFoundException(
                UtilityReadingErrorMessages.ContractNotFound,
                nameof(UtilityReadingErrorCode.ContractNotFound));
        }

        return contract;
    }

    private IQueryable<Contract> ManageableContracts(PropertyCaller caller)
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

        throw new UtilityReadingAuthorizationException(
            UtilityReadingErrorMessages.Forbidden,
            nameof(UtilityReadingErrorCode.Forbidden));
    }

    private static void ValidateContractAndPeriod(
        Contract contract,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        if (contract.Status != ContractStatus.Approved)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.ContractNotApproved,
                nameof(UtilityReadingErrorCode.ContractNotApproved));
        }

        var issues = UtilityReadingRules.ValidatePeriod(periodStart, periodEnd);
        if (issues.Count > 0)
        {
            throw FromIssue(issues[0]);
        }

        var contractStart = DateOnly.FromDateTime(contract.StartDate);
        var contractEnd = DateOnly.FromDateTime(contract.EndDate);
        if (periodStart < contractStart || periodEnd > contractEnd)
        {
            throw new UtilityReadingException(
                UtilityReadingErrorMessages.PeriodOutsideContract,
                nameof(UtilityReadingErrorCode.PeriodOutsideContract));
        }
    }

    private static UtilityReadingException FromIssue(UtilityReadingIssue issue)
    {
        var message = issue.Error switch
        {
            UtilityReadingErrorCode.InvalidPeriod => UtilityReadingErrorMessages.InvalidPeriod,
            UtilityReadingErrorCode.ReadingCannotBeNegative => UtilityReadingErrorMessages.ReadingCannotBeNegative,
            UtilityReadingErrorCode.ReadingLessThanPrevious => UtilityReadingErrorMessages.ReadingLessThanPrevious,
            _ => UtilityReadingErrorMessages.InvalidPeriod,
        };

        return new UtilityReadingException(message, issue.Error.ToString());
    }

    private static string PeriodLabel(UtilityReadingPeriod period) =>
        $"{DateFormatter.Format(period.PeriodStart)} - {DateFormatter.Format(period.PeriodEnd)}";

    private static decimal SumOf(IReadOnlyList<InvoiceItem> items, InvoiceItemType type) =>
        items.Where(item => item.ItemType == type).Sum(item => item.Amount);

    private static decimal SumOf(IReadOnlyList<InvoiceItemInputDto> items, InvoiceItemType type) =>
        items
            .Where(item => item.ItemType == type)
            .Sum(InvoiceCalculationRules.CalculateItemAmount);

    private static InvoiceItemInputDto ToInvoiceInput(InvoiceItem item) =>
        new()
        {
            ItemType = item.ItemType,
            Description = item.Description,
            Unit = item.Unit,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice,
            // Dòng giảm giá lưu số âm trong hóa đơn; nguồn đầu vào của P6 nhập số dương.
            Amount = item.ItemType == InvoiceItemType.Discount ? Math.Abs(item.Amount) : item.Amount,
        };

}
