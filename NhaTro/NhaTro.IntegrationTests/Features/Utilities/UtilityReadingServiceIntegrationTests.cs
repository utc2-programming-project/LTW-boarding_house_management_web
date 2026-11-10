using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Rules;
using Web.Features.Properties;
using Web.Features.Utilities;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Enums;
using Web.Features.Utilities.Services;

namespace NhaTro.IntegrationTests.Features.Utilities;

/// <summary>
/// VHT-51 (đợt 3A) — ghi chỉ số/sử dụng/tính chi phí kỳ thật trên SQL Server thật:
/// lấy chỉ số kỳ trước, chống trùng/chồng lấn kỳ, chốt chi phí điện/nước bằng snapshot,
/// khóa kỳ đã phát hành hóa đơn (D10), cấp nguồn chi phí cho P6 và phạm vi theo vai trò.
/// </summary>
public sealed class UtilityReadingServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int Year = 2026;
    private static readonly DateOnly OctoberStart = new(Year, 10, 1);
    private static readonly DateOnly OctoberEnd = new(Year, 10, 31);
    private static readonly DateOnly NovemberStart = new(Year, 11, 1);
    private static readonly DateOnly NovemberEnd = new(Year, 11, 30);

    [SqlServerFact]
    public async Task OpenPeriodAsync_TakesOpeningFromPreviousPeriod_AndBlocksDuplicateOrOverlap()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        var october = await service.OpenPeriodAsync(
            data.ContractId, OctoberStart, OctoberEnd, owner);
        Assert.Equal(0m, october.ElectricStart);
        Assert.Equal(0m, october.WaterStart);
        Assert.False(october.HasReadings);
        Assert.Equal(UtilityReadingPeriodStatus.Open, october.Status);

        var reopened = await service.OpenPeriodAsync(
            data.ContractId, OctoberStart, OctoberEnd, owner);
        Assert.Equal(october.Id, reopened.Id);

        var duplicate = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.OpenPeriodAsync(data.ContractId, OctoberStart, new DateOnly(Year, 10, 30), owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.DuplicatePeriod), duplicate.Code);

        // Không chen kỳ chồng lấn (tính cả hai đầu) vào chuỗi chỉ số.
        var overlapping = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.OpenPeriodAsync(
                data.ContractId,
                new DateOnly(Year, 10, 15),
                new DateOnly(Year, 10, 20),
                owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.OverlappingPeriod), overlapping.Code);

        await service.SaveReadingsAsync(october.Id, 160m, 16m, owner);

        var november = await service.OpenPeriodAsync(
            data.ContractId, NovemberStart, NovemberEnd, owner);
        Assert.Equal(160m, november.ElectricStart);
        Assert.Equal(16m, november.WaterStart);

        // Kỳ trước đã có kỳ sau: không mở chen ngược thời gian.
        var earlier = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.OpenPeriodAsync(
                data.ContractId,
                new DateOnly(Year, 9, 1),
                new DateOnly(Year, 9, 30),
                owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodHasLaterPeriod), earlier.Code);

        Assert.Equal(
            2,
            await db.UtilityReadingPeriods.CountAsync(period => period.ContractId == data.ContractId));
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_LocksElectricAndWaterCostsIntoSnapshots()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);

        var saved = await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        Assert.True(saved.HasReadings);
        Assert.Equal(UtilityReadingPeriodStatus.Open, saved.Status);
        Assert.Equal(160m, saved.ElectricUsage);
        Assert.Equal(16m, saved.WaterUsage);
        Assert.Equal(3_000_000m, saved.RentAmount);
        // 50 kWh × 1.984 + 50 × 2.050 + 60 × 2.380 = 344.500.
        Assert.Equal(344_500m, saved.ElectricCost);
        // 4 m³ × 6.700 + 2 × 12.900 + 10 × 14.400 = 196.600.
        Assert.Equal(196_600m, saved.WaterCost);
        Assert.Equal(0m, saved.ServiceCost);
        Assert.Equal(3_541_100m, saved.TotalAmount);
        Assert.NotNull(saved.ElectricSnapshotId);
        Assert.NotNull(saved.WaterSnapshotId);

        var snapshots = await db.UtilityBillingPeriodSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.CostLines)
            .Where(snapshot => snapshot.BillingPeriodId == period.Id)
            .ToListAsync();

        Assert.Equal(2, snapshots.Count);

        var electric = snapshots.Single(snapshot => snapshot.Type == TariffType.Electric);
        Assert.Equal(UtilityReadingService.ElectricScopeCode, electric.ScopeCode);
        Assert.Equal(160m, electric.Quantity);
        Assert.Equal(344_500m, electric.Total);
        // Snapshot giữ đủ các bậc của biểu giá để đối chiếu, kể cả bậc chưa dùng.
        Assert.Equal(6, electric.CostLines.Count);

        var water = snapshots.Single(snapshot => snapshot.Type == TariffType.Water);
        Assert.Equal(UtilityReadingService.WaterScopeCode, water.ScopeCode);
        Assert.Equal(16m, water.Quantity);
        Assert.Equal(196_600m, water.Total);
        Assert.Equal(1, water.ResidentCount);
        Assert.Equal(3, water.CostLines.Count);
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_SnapshotsServiceLines_AndLaterCatalogToggleCannotChangeSettledMoney()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        await SeedServiceCatalogsAsync(db, data.PropertyAId);
        var owner = Owner(data);
        var service = NewService(db);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);

        var saved = await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);
        Assert.Equal(130_000m, saved.ServiceCost);
        Assert.True(await db.UtilityReadingPeriods
            .AnyAsync(item => item.Id == period.Id && item.HasServiceSnapshots));
        Assert.Equal(2, await db.UtilityServicePeriodSnapshots
            .CountAsync(item => item.UtilityReadingPeriodId == period.Id));

        // Chủ trọ tắt danh mục cho các kỳ mới. Kỳ đã chốt chỉ số phải đọc snapshot của nó.
        await db.ServiceCatalogs.ExecuteUpdateAsync(setters => setters
            .SetProperty(catalog => catalog.IsActive, false));
        db.ChangeTracker.Clear();

        var reread = await service.GetPeriodAsync(period.Id, owner);
        Assert.NotNull(reread);
        Assert.Equal(130_000m, reread.ServiceCost);
        Assert.Equal(3_671_100m, reread.TotalAmount);

        var invoiceSource = await service.BuildInvoiceRequestAsync(period.Id, owner);
        Assert.Equal(130_000m, invoiceSource.Items
            .Where(item => item.ItemType == InvoiceItemType.Service)
            .Sum(item => item.Amount));
        Assert.Equal(3_671_100m, InvoiceCalculationRules.CalculateTotal(invoiceSource.Items));
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_PreservesFractionalServiceUnitPriceInSnapshot()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        await AddCatalogAsync(
            db, data.PropertyAId, "Phí theo tỷ lệ", "tháng", "fixed-monthly", 12.3456m, isReadOnly: false);
        var owner = Owner(data);
        var service = NewService(db);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);

        await service.SaveReadingsAsync(period.Id, 0m, 0m, owner);

        var snapshot = await db.UtilityServicePeriodSnapshots
            .SingleAsync(item => item.UtilityReadingPeriodId == period.Id);
        Assert.Equal(12.3456m, snapshot.UnitPrice);
        Assert.Equal(12m, snapshot.Amount);

        var invoiceSource = await service.BuildInvoiceRequestAsync(period.Id, owner);
        var item = Assert.Single(invoiceSource.Items, item => item.ItemType == InvoiceItemType.Service);
        Assert.Equal(12.3456m, item.UnitPrice);
        Assert.Equal(12m, item.Amount);
    }

    [SqlServerFact]
    public async Task BuildInvoiceRequestAsync_RejectsLegacyPeriodWithoutServiceSnapshots()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var owner = Owner(data);
        var service = NewService(db);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await service.SaveReadingsAsync(period.Id, 0m, 0m, owner);

        // Mô phỏng một kỳ đã ghi chỉ số từ trước migration mới: không được lấy danh mục hiện
        // tại để đoán lại chi phí dịch vụ và phát hành hóa đơn với số liệu sai.
        await db.UtilityReadingPeriods
            .Where(item => item.Id == period.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.HasServiceSnapshots, false));
        db.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.BuildInvoiceRequestAsync(period.Id, owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.ServiceSnapshotsMissing), exception.Code);
    }

    [SqlServerFact]
    public async Task OpenPeriodAsync_ConcurrentOverlaps_OnlyOnePeriodIsCreated()
    {
        var data = await SeedApprovedContractAsync();
        var owner = Owner(data);
        using var start = new Barrier(2);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        async Task<object> TryOpenAsync(DateOnly startDate, DateOnly endDate)
        {
            await using var db = fixture.CreateContext();
            var service = NewService(db);
            await Task.Run(() => start.SignalAndWait(timeout.Token), timeout.Token);
            try
            {
                return await service.OpenPeriodAsync(data.ContractId, startDate, endDate, owner);
            }
            catch (UtilityReadingException exception)
            {
                return exception;
            }
        }

        var all = Task.WhenAll(
            TryOpenAsync(OctoberStart, OctoberEnd),
            TryOpenAsync(new DateOnly(Year, 10, 15), new DateOnly(Year, 11, 15)));
        var completed = await Task.WhenAny(all, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        Assert.Same(all, completed);
        var results = await all;

        Assert.IsType<UtilityReadingPeriodDto>(
            Assert.Single(results, result => result is UtilityReadingPeriodDto));
        var rejected = Assert.IsType<UtilityReadingException>(
            Assert.Single(results, result => result is UtilityReadingException));
        // Luồng nào lấy khóa hợp đồng trước quyết định mã lỗi: luồng sau thấy kỳ chồng lấn,
        // hoặc luồng trước thấy đã có kỳ bắt đầu muộn hơn.
        Assert.Contains(rejected.Code, new[]
        {
            nameof(UtilityReadingErrorCode.OverlappingPeriod),
            nameof(UtilityReadingErrorCode.PeriodHasLaterPeriod),
        });

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.UtilityReadingPeriods
            .CountAsync(period => period.ContractId == data.ContractId));
    }

    [SqlServerFact]
    public async Task BackfillServiceSnapshotsAsync_RejectsNoReadingsAndInvoicedPeriod()
    {
        var data = await SeedApprovedContractAsync(); await using var db = fixture.CreateContext();
        var service = NewService(db); var owner = Owner(data);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        var noReadings = await Assert.ThrowsAsync<UtilityReadingException>(() => service.BackfillServiceSnapshotsAsync(period.Id, owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodHasNoReadings), noReadings.Code);
        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);
        var invoice = new Invoice { ContractId = data.ContractId, PeriodStart = OctoberStart, PeriodEnd = OctoberEnd, Status = InvoiceStatus.Issued, TotalAmount = 1m, CreatedAt = DateTime.UtcNow };
        db.Invoices.Add(invoice); await db.SaveChangesAsync();
        await db.UtilityReadingPeriods.Where(x => x.Id == period.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.InvoiceId, invoice.Id)); db.ChangeTracker.Clear();
        var locked = await Assert.ThrowsAsync<UtilityReadingException>(() => service.BackfillServiceSnapshotsAsync(period.Id, owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodLocked), locked.Code);
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_ConcurrentSamePeriod_OnlyOneEntryWins()
    {
        var data = await SeedApprovedContractAsync();
        var owner = Owner(data);
        int periodId;
        await using (var setup = fixture.CreateContext())
        {
            periodId = (await NewService(setup).OpenPeriodAsync(
                data.ContractId, OctoberStart, OctoberEnd, owner)).Id;
        }

        using var start = new Barrier(2);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        async Task<object> TrySaveAsync(decimal electricEnd)
        {
            await using var db = fixture.CreateContext();
            var service = NewService(db);
            await Task.Run(() => start.SignalAndWait(timeout.Token), timeout.Token);
            try
            {
                return await service.SaveReadingsAsync(periodId, electricEnd, 16m, owner, timeout.Token);
            }
            catch (UtilityReadingException exception)
            {
                return exception;
            }
        }

        var all = Task.WhenAll(TrySaveAsync(160m), TrySaveAsync(170m));
        var completed = await Task.WhenAny(all, Task.Delay(Timeout.InfiniteTimeSpan, timeout.Token));
        Assert.Same(all, completed);
        var results = await all;

        Assert.IsType<UtilityReadingPeriodDto>(
            Assert.Single(results, result => result is UtilityReadingPeriodDto));
        var rejected = Assert.IsType<UtilityReadingException>(
            Assert.Single(results, result => result is UtilityReadingException));
        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodAlreadyHasReadings), rejected.Code);
    }

    [SqlServerFact]
    public async Task BackfillServiceSnapshotsAsync_BackfillsLegacyPeriodAndIsIdempotent()
    {
        var data = await SeedApprovedContractAsync(); await using var db = fixture.CreateContext();
        await SeedServiceCatalogsAsync(db, data.PropertyAId); var service = NewService(db); var owner = Owner(data);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);
        await db.UtilityServicePeriodSnapshots.Where(x => x.UtilityReadingPeriodId == period.Id).ExecuteDeleteAsync();
        await db.UtilityReadingPeriods.Where(x => x.Id == period.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.HasServiceSnapshots, false)); db.ChangeTracker.Clear();
        Assert.Equal(2, await service.BackfillServiceSnapshotsAsync(period.Id, owner));
        Assert.Equal(2, await db.UtilityServicePeriodSnapshots.CountAsync(x => x.UtilityReadingPeriodId == period.Id));
        var dto = await service.GetPeriodAsync(period.Id, owner); Assert.NotNull(dto); Assert.False(dto.ServiceSnapshotsMissing); Assert.Equal(130_000m, dto.ServiceCost);
        Assert.Equal(130_000m, (await service.BuildInvoiceRequestAsync(period.Id, owner)).Items.Where(x => x.ItemType == InvoiceItemType.Service).Sum(x => x.Amount));
        Assert.Equal(0, await service.BackfillServiceSnapshotsAsync(period.Id, owner));
    }

    [SqlServerFact]
    public async Task BuildInvoiceRequestAsync_RejectsInvoiceFromDifferentPeriod()
    {
        var data = await SeedApprovedContractAsync(); await using var db = fixture.CreateContext(); var service = NewService(db); var owner = Owner(data);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner); await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);
        var invoice = new Invoice { ContractId = data.ContractId, PeriodStart = new DateOnly(Year, 9, 1), PeriodEnd = new DateOnly(Year, 9, 30), Status = InvoiceStatus.Issued, TotalAmount = 1m, CreatedAt = DateTime.UtcNow };
        db.Invoices.Add(invoice); await db.SaveChangesAsync(); await db.UtilityReadingPeriods.Where(x => x.Id == period.Id).ExecuteUpdateAsync(s => s.SetProperty(x => x.InvoiceId, invoice.Id)); db.ChangeTracker.Clear();
        var dto = await Assert.ThrowsAsync<UtilityReadingException>(() => service.GetPeriodAsync(period.Id, owner)); Assert.Equal(nameof(UtilityReadingErrorCode.InvoicePeriodMismatch), dto.Code);
        var source = await Assert.ThrowsAsync<UtilityReadingException>(() => service.BuildInvoiceRequestAsync(period.Id, owner)); Assert.Equal(nameof(UtilityReadingErrorCode.InvoicePeriodMismatch), source.Code);
    }

    [SqlServerFact]
    public async Task BuildInvoiceRequestAsync_UnitLength25_FlowsThroughToInvoice()
    {
        var data = await SeedApprovedContractAsync(); await using var db = fixture.CreateContext(); var service = NewService(db); var owner = Owner(data);
        var unit = new string('a', 25); await AddCatalogAsync(db, data.PropertyAId, "Phi dai", unit, "fixed", 50_000m, false);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner); await service.SaveReadingsAsync(period.Id, 0m, 0m, owner);
        var item = Assert.Single((await service.BuildInvoiceRequestAsync(period.Id, owner)).Items, x => x.ItemType == InvoiceItemType.Service);
        Assert.Equal(unit, item.Unit); Assert.Equal(50_000m, item.Amount);
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_AfterValidationFailure_CanRetryInSameContext()
    {
        var data = await SeedApprovedContractAsync(); await using var db = fixture.CreateContext(); var service = NewService(db); var owner = Owner(data);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await Assert.ThrowsAsync<UtilityReadingException>(() => service.SaveReadingsAsync(period.Id, -1m, 16m, owner));
        var saved = await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);
        Assert.True(saved.HasReadings); Assert.Equal(160m, saved.ElectricEnd);
    }

    [SqlServerFact]
    public async Task BuildInvoiceRequestAsync_HandlesMeteredAndPerPersonUnitVariants()
    {
        var data = await SeedApprovedContractAsync(); await using var db = fixture.CreateContext(); var service = NewService(db); var owner = Owner(data);
        await AddCatalogAsync(db, data.PropertyAId, "Dien bien the", "kWh/tháng", "meter", 1m, false);
        await AddCatalogAsync(db, data.PropertyAId, "Ve sinh bien the", "Người/tháng", "fixed", 30_000m, false);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner); await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);
        var items = (await service.BuildInvoiceRequestAsync(period.Id, owner)).Items;
        Assert.DoesNotContain(items, x => x.ItemType == InvoiceItemType.Service && x.Description == "Dien bien the");
        Assert.Equal(1m, Assert.Single(items, x => x.Description == "Ve sinh bien the").Quantity);
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_RejectsSecondEntry_SoCorrectionGoesToVht102()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);

        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        // VHT-51 chỉ ghi lần đầu; sửa chỉ số (kèm lý do + lịch sử) thuộc VHT-102.
        var second = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.SaveReadingsAsync(period.Id, 175m, 17m, owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodAlreadyHasReadings), second.Code);

        // Không có thay đổi nào bị ghi dở: chỉ số và snapshot vẫn là lần đầu.
        var stored = await db.UtilityReadingPeriods.AsNoTracking().SingleAsync(item => item.Id == period.Id);
        Assert.Equal(160m, stored.ElectricEnd);
        Assert.Equal(16m, stored.WaterEnd);

        var electric = await db.UtilityBillingPeriodSnapshots
            .AsNoTracking()
            .SingleAsync(snapshot => snapshot.BillingPeriodId == period.Id
                && snapshot.Type == TariffType.Electric);
        Assert.Equal(160m, electric.Quantity);
        Assert.Equal(344_500m, electric.Total);
    }

    [SqlServerFact]
    public async Task OpenPeriodAsync_RequiresPreviousReadingsBeforeOpeningNextPeriod()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        var october = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);

        // Kỳ trước chưa ghi chỉ số: mở kỳ sau sẽ lấy sai mốc đầu kỳ nên bị chặn.
        var blocked = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.OpenPeriodAsync(data.ContractId, NovemberStart, NovemberEnd, owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PreviousPeriodHasNoReadings), blocked.Code);

        var reread = await service.GetPeriodByContractAsync(data.ContractId, OctoberStart, owner);
        Assert.NotNull(reread);
        Assert.Equal(october.Id, reread.Id);
        await service.SaveReadingsAsync(october.Id, 120m, 12m, owner);

        var november = await service.OpenPeriodAsync(data.ContractId, NovemberStart, NovemberEnd, owner);
        Assert.Equal(120m, november.ElectricStart);
        Assert.Equal(12m, november.WaterStart);
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_RefreshesOpeningFromPreviousPeriod_WhenPeriodsWereOpenedOutOfOrder()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        var october = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        var roomId = await db.Contracts
            .Where(contract => contract.ContractId == data.ContractId)
            .Select(contract => contract.RoomId)
            .SingleAsync();

        // Mô phỏng hai kỳ được mở chen nhau: kỳ 11 ra đời khi kỳ 10 chưa có chỉ số nên đầu kỳ = 0.
        var november = new UtilityReadingPeriod
        {
            ContractId = data.ContractId,
            RoomId = roomId,
            PeriodStart = NovemberStart,
            PeriodEnd = NovemberEnd,
            ElectricStart = 0m,
            ElectricEnd = 0m,
            WaterStart = 0m,
            WaterEnd = 0m,
            ResidentCount = 1,
            Status = UtilityReadingPeriodStatus.Open,
            HasReadings = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.UtilityReadingPeriods.Add(november);
        await db.SaveChangesAsync();

        // Kỳ 10 chưa đọc thì kỳ 11 không được ghi, tránh dây chuyền chỉ số sai.
        var blocked = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.SaveReadingsAsync(november.Id, 200m, 20m, owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PreviousPeriodHasNoReadings), blocked.Code);

        await service.SaveReadingsAsync(october.Id, 160m, 16m, owner);

        // Kỳ 11 được kéo lại đúng mốc đầu kỳ từ kỳ 10 trước khi tính usage.
        var saved = await service.SaveReadingsAsync(november.Id, 200m, 20m, owner);
        Assert.Equal(160m, saved.ElectricStart);
        Assert.Equal(16m, saved.WaterStart);
        Assert.Equal(40m, saved.ElectricUsage);
        Assert.Equal(4m, saved.WaterUsage);
    }

    [SqlServerFact]
    public async Task BuildInvoiceRequestAsync_KeepsServiceLines_WhenRoomWasSoftDeleted()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        await SeedServiceCatalogsAsync(db, data.PropertyAId);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        // Phòng bị xóa mềm sau khi mở kỳ: kỳ đã chụp RoomId nên vẫn phải giữ dịch vụ của tòa.
        var room = await db.Rooms.IgnoreQueryFilters().SingleAsync(item => item.Id == period.RoomId);
        room.IsDeleted = true;
        room.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var stored = await service.GetPeriodAsync(period.Id, owner);
        Assert.NotNull(stored);
        Assert.Equal(130_000m, stored.ServiceCost);

        var request = await service.BuildInvoiceRequestAsync(period.Id, owner);
        Assert.Contains(request.Items, item => item.Description == "Vệ sinh");
        Assert.Contains(request.Items, item => item.Description == "Internet");
        Assert.Equal(3_671_100m, InvoiceCalculationRules.CalculateTotal(request.Items));
    }

    [SqlServerFact]
    public async Task BuildInvoiceRequestAsync_RejectsPeriodWithoutReadings()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);

        var exception = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.BuildInvoiceRequestAsync(period.Id, owner));

        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodHasNoReadings), exception.Code);
    }

    [SqlServerFact]
    public async Task BuildInvoiceRequestAsync_ComposesSettledSourceWithoutDuplicatingMeteredServices()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        await SeedServiceCatalogsAsync(db, data.PropertyAId);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        var request = await service.BuildInvoiceRequestAsync(period.Id, owner);

        Assert.Equal(data.ContractId, request.ContractId);
        Assert.Equal(OctoberStart, request.PeriodStart);
        Assert.Equal(OctoberEnd, request.PeriodEnd);
        // 3.000.000 thuê + 344.500 điện + 196.600 nước + 100.000 internet + 30.000 vệ sinh/người.
        Assert.Equal(3_671_100m, InvoiceCalculationRules.CalculateTotal(request.Items));
        Assert.Equal(5, request.Items.Count);
        Assert.DoesNotContain(
            request.Items,
            item => item.ItemType == InvoiceItemType.Service
                && item.Unit is not null
                && (item.Unit == "kWh" || item.Unit == "m³"));

        var electric = Assert.Single(request.Items, item => item.ItemType == InvoiceItemType.Electric);
        Assert.Equal(160m, electric.Quantity);
        Assert.Equal(344_500m, electric.Amount);
        // Tổng theo bậc đã chốt, đơn giá chỉ là bình quân để hiển thị.
        Assert.Equal(2_153m, electric.UnitPrice);

        var cleaning = Assert.Single(request.Items, item => item.Description == "Vệ sinh");
        Assert.Equal("người", cleaning.Unit);
        Assert.Equal(1m, cleaning.Quantity);
        Assert.Equal(30_000m, cleaning.Amount);
    }

    [SqlServerFact]
    public async Task BuildInvoiceRequestAsync_IncludesServiceCatalogApplyingToEveryProperty()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        // Danh mục PropertyId = null áp dụng cho mọi tòa (ServiceCatalog.PropertyId).
        await AddCatalogAsync(
            db, propertyId: null, "Rác", "tháng", "fixed-monthly", 20_000m, isReadOnly: false);
        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        var request = await service.BuildInvoiceRequestAsync(period.Id, owner);

        var item = Assert.Single(request.Items, entry => entry.Description == "Rác");
        Assert.Equal(20_000m, item.Amount);
        Assert.Equal(3_561_100m, InvoiceCalculationRules.CalculateTotal(request.Items));
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_RejectsInvoicedPeriod_AndReadsMoneyFromIssuedInvoice()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        // P6 (VHT-53) phát hành hóa đơn rồi gắn InvoiceId vào kỳ: mô phỏng bằng dữ liệu đã chốt.
        var invoice = new Invoice
        {
            ContractId = data.ContractId,
            PeriodStart = OctoberStart,
            PeriodEnd = OctoberEnd,
            Status = InvoiceStatus.Issued,
            TotalAmount = 3_541_100m,
            IssuedAt = DateTime.UtcNow,
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        db.InvoiceItems.AddRange(
            new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ItemType = InvoiceItemType.Rent,
                Description = "Tiền thuê",
                Unit = "tháng",
                Quantity = 1m,
                UnitPrice = 3_000_000m,
                Amount = 3_000_000m,
                DisplayOrder = 1,
            },
            new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ItemType = InvoiceItemType.Electric,
                Description = "Điện",
                Unit = "kWh",
                Quantity = 160m,
                UnitPrice = 2_153m,
                Amount = 344_500m,
                DisplayOrder = 2,
            },
            new InvoiceItem
            {
                InvoiceId = invoice.Id,
                ItemType = InvoiceItemType.Water,
                Description = "Nước",
                Unit = "m³",
                Quantity = 16m,
                UnitPrice = 12_288m,
                Amount = 196_600m,
                DisplayOrder = 3,
            });
        await db.SaveChangesAsync();

        var stored = await db.UtilityReadingPeriods.SingleAsync(item => item.Id == period.Id);
        stored.Status = UtilityReadingPeriodStatus.Invoiced;
        stored.InvoiceId = invoice.Id;
        await db.SaveChangesAsync();

        // Danh mục dịch vụ đổi giá sau khi phát hành: tiền của kỳ đã khóa phải giữ nguyên.
        await SeedServiceCatalogsAsync(db, data.PropertyAId, internetPrice: 999_999m);

        var locked = await service.GetPeriodAsync(period.Id, owner);
        Assert.NotNull(locked);
        Assert.Equal(UtilityReadingPeriodStatus.Invoiced, locked.Status);
        Assert.Equal(invoice.Id, locked.InvoiceId);
        Assert.Equal(3_000_000m, locked.RentAmount);
        Assert.Equal(344_500m, locked.ElectricCost);
        Assert.Equal(196_600m, locked.WaterCost);
        Assert.Equal(0m, locked.ServiceCost);
        Assert.Equal(3_541_100m, locked.TotalAmount);

        // Nguồn cho P6 vẫn là đúng các dòng hóa đơn đã chốt.
        var request = await service.BuildInvoiceRequestAsync(period.Id, owner);
        Assert.Equal(3, request.Items.Count);
        Assert.Equal(3_541_100m, InvoiceCalculationRules.CalculateTotal(request.Items));

        // D10: kỳ đã phát hành hóa đơn thì khóa vĩnh viễn.
        var lockedException = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.SaveReadingsAsync(period.Id, 200m, 20m, owner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodLocked), lockedException.Code);
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_TreatsLinkedInvoiceAsLockedEvenIfStatusIsStillOpen()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        // Kỳ đã gắn hóa đơn nhưng trạng thái chưa kịp đổi: tiền lịch sử đã nằm ở hóa đơn nên vẫn phải khóa.
        var invoice = new Invoice
        {
            ContractId = data.ContractId,
            PeriodStart = OctoberStart,
            PeriodEnd = OctoberEnd,
            Status = InvoiceStatus.Draft,
            TotalAmount = 0m,
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var stored = await db.UtilityReadingPeriods.SingleAsync(item => item.Id == period.Id);
        stored.InvoiceId = invoice.Id;
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<UtilityReadingException>(() =>
            service.SaveReadingsAsync(period.Id, 200m, 20m, owner));

        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodLocked), exception.Code);
    }

    [SqlServerFact]
    public async Task Database_RejectsInvoicedStatusWithoutInvoiceId()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);

        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);
        await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        // D10 ở tầng DB: không thể đánh dấu đã phát hành mà không có hóa đơn để đọc lại tiền.
        var stored = await db.UtilityReadingPeriods.SingleAsync(item => item.Id == period.Id);
        stored.Status = UtilityReadingPeriodStatus.Invoiced;

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [SqlServerFact]
    public async Task SaveReadingsAsync_AfterMidTransactionFailure_CanRetryInSameContext()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var owner = Owner(data);
        var failing = new UtilityReadingService(
            db,
            new WaterFailingTariffService(new UtilityTariffService(db)),
            TimeProvider.System);

        var period = await failing.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);

        // Điện đã snapshot xong, nước lỗi giữa transaction: toàn bộ phải rollback.
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failing.SaveReadingsAsync(period.Id, 160m, 16m, owner));

        // Context không được giữ entity dở dang, nếu không lần thử lại sẽ báo nhầm
        // PeriodAlreadyHasReadings hoặc đụng unique snapshot.
        var service = NewService(db);
        var saved = await service.SaveReadingsAsync(period.Id, 160m, 16m, owner);

        Assert.True(saved.HasReadings);
        Assert.Equal(344_500m, saved.ElectricCost);
        Assert.Equal(196_600m, saved.WaterCost);
        Assert.Equal(
            2,
            await db.UtilityBillingPeriodSnapshots.CountAsync(item => item.BillingPeriodId == period.Id));
    }

    [SqlServerFact]
    public async Task PeriodScope_FollowsContractOwnershipAndRoles()
    {
        var data = await SeedApprovedContractAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var owner = Owner(data);
        var otherOwner = new PropertyCaller(data.ChuTroBId, Roles.ChuTro);
        var tenant = new PropertyCaller(data.KhachThueId, Roles.KhachThue);
        var manager = new PropertyCaller(data.QuanLyId, Roles.QuanLy);

        var period = await service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, owner);

        var otherContract = await Assert.ThrowsAsync<UtilityReadingNotFoundException>(() =>
            service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, otherOwner));
        Assert.Equal(nameof(UtilityReadingErrorCode.ContractNotFound), otherContract.Code);

        var forbidden = await Assert.ThrowsAsync<UtilityReadingAuthorizationException>(() =>
            service.OpenPeriodAsync(data.ContractId, OctoberStart, OctoberEnd, tenant));
        Assert.Equal(nameof(UtilityReadingErrorCode.Forbidden), forbidden.Code);

        Assert.Null(await service.GetPeriodAsync(period.Id, otherOwner));

        var hidden = await Assert.ThrowsAsync<UtilityReadingNotFoundException>(() =>
            service.SaveReadingsAsync(period.Id, 10m, 1m, otherOwner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodNotFound), hidden.Code);

        var hiddenSource = await Assert.ThrowsAsync<UtilityReadingNotFoundException>(() =>
            service.BuildInvoiceRequestAsync(period.Id, otherOwner));
        Assert.Equal(nameof(UtilityReadingErrorCode.PeriodNotFound), hiddenSource.Code);

        // Quản lý được phân công tòa A ghi chỉ số bình thường.
        var managed = await service.GetPeriodAsync(period.Id, manager);
        Assert.NotNull(managed);
        var saved = await service.SaveReadingsAsync(period.Id, 10m, 1m, manager);
        Assert.True(saved.HasReadings);
        Assert.Equal(10m, saved.ElectricUsage);
        Assert.Equal(1m, saved.WaterUsage);
    }

    [SqlServerFact]
    public async Task OpenPeriodAsync_RejectsUnapprovedContractAndPeriodOutsideContract()
    {
        await fixture.ResetAsync();

        SeedData data;
        await using (var db = fixture.CreateContext())
        {
            data = await SeedContractAsync(db, ContractStatus.Approved);
        }

        await using (var db = fixture.CreateContext())
        {
            var service = NewService(db);
            var outside = await Assert.ThrowsAsync<UtilityReadingException>(() =>
                service.OpenPeriodAsync(
                    data.ContractId,
                    new DateOnly(Year - 1, 12, 1),
                    new DateOnly(Year - 1, 12, 31),
                    Owner(data)));
            Assert.Equal(nameof(UtilityReadingErrorCode.PeriodOutsideContract), outside.Code);

            var reversed = await Assert.ThrowsAsync<UtilityReadingException>(() =>
                service.OpenPeriodAsync(
                    data.ContractId,
                    OctoberEnd,
                    OctoberStart,
                    Owner(data)));
            Assert.Equal(nameof(UtilityReadingErrorCode.InvalidPeriod), reversed.Code);
        }

        await fixture.ResetAsync();
        await using (var db = fixture.CreateContext())
        {
            var pending = await SeedContractAsync(db, ContractStatus.Pending);
            var service = NewService(db);

            var unapproved = await Assert.ThrowsAsync<UtilityReadingException>(() =>
                service.OpenPeriodAsync(pending.ContractId, OctoberStart, OctoberEnd, Owner(pending)));
            Assert.Equal(nameof(UtilityReadingErrorCode.ContractNotApproved), unapproved.Code);
        }
    }

    private static UtilityReadingService NewService(ApplicationDbContext db) =>
        new(db, new UtilityTariffService(db), TimeProvider.System);

    private static PropertyCaller Owner(SeedData data) =>
        new(data.ChuTroAId, Roles.ChuTro);

    private async Task<SeedData> SeedApprovedContractAsync() =>
        await SeedApprovedContractAsync(ContractStatus.Approved);

    private async Task<SeedData> SeedApprovedContractAsync(ContractStatus status)
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        return await SeedContractAsync(db, status);
    }

    private static async Task<SeedData> SeedContractAsync(
        ApplicationDbContext db,
        ContractStatus status)
    {
        var ids = await TestDataSeeder.SeedAsync(db);

        var contractNumber = "HD-P5-" + Guid.NewGuid().ToString("N")[..12];
        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = contractNumber,
            RoomId = ids.RoomAId,
            StartDate = new DateTime(Year, 1, 1),
            EndDate = new DateTime(Year, 12, 31),
            SnapshotPrice = TestDataSeeder.RoomAStandardPrice,
            DepositAmount = TestDataSeeder.RoomAStandardPrice,
            Status = status,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = ids.TenantAId,
        });
        await db.SaveChangesAsync();

        return new SeedData(
            ids.ChuTroAId,
            ids.ChuTroBId,
            ids.QuanLyId,
            ids.KhachThueId,
            ids.PropertyAId,
            contract.ContractId);
    }

    private static async Task SeedServiceCatalogsAsync(
        ApplicationDbContext db,
        int propertyId,
        decimal internetPrice = 100_000m)
    {
        await AddCatalogAsync(
            db, propertyId, "Internet", "tháng", "fixed-monthly", internetPrice, isReadOnly: false);
        await AddCatalogAsync(
            db, propertyId, "Vệ sinh", "người", "fixed-monthly", 30_000m, isReadOnly: false);
        // Danh mục điện/nước đã nằm trong biểu giá khóa: không được cộng trùng vào hóa đơn.
        await AddCatalogAsync(
            db, propertyId, "Điện theo danh mục", "kWh", "meter", 3_000m, isReadOnly: false);
        await AddCatalogAsync(
            db, propertyId, "Nước hệ thống", "m³", "meter", 9_000m, isReadOnly: true);
    }

    private static async Task<ServiceCatalog> AddCatalogAsync(
        ApplicationDbContext db,
        int? propertyId,
        string name,
        string unit,
        string calculationMethod,
        decimal unitPrice,
        bool isReadOnly)
    {
        var catalog = new ServiceCatalog
        {
            PropertyId = propertyId,
            IsReadOnly = isReadOnly,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.ServiceCatalogs.Add(catalog);
        await db.SaveChangesAsync();

        db.ServicePriceHistories.Add(new ServicePriceHistory
        {
            ServiceCatalogId = catalog.Id,
            Name = name,
            Unit = unit,
            CalculationMethod = calculationMethod,
            UnitPrice = unitPrice,
            EffectiveDate = new DateOnly(Year, 1, 1),
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        return catalog;
    }

    private sealed record SeedData(
        string ChuTroAId,
        string ChuTroBId,
        string QuanLyId,
        string KhachThueId,
        int PropertyAId,
        Guid ContractId);

    /// <summary>Giả lập biểu giá nước lỗi sau khi điện đã snapshot để kiểm tra rollback + retry.</summary>
    private sealed class WaterFailingTariffService(IUtilityTariffService inner) : IUtilityTariffService
    {
        public Task<TariffDto> CalculateForPeriodAsync(
            TariffType type,
            string scopeCode,
            DateOnly periodStart,
            DateOnly periodEnd,
            decimal quantity,
            int residentCount = 1,
            CancellationToken cancellationToken = default) =>
            inner.CalculateForPeriodAsync(
                type, scopeCode, periodStart, periodEnd, quantity, residentCount, cancellationToken);

        public Task<TariffDto> SnapshotForPeriodAsync(
            int billingPeriodId,
            TariffType type,
            string scopeCode,
            DateOnly periodStart,
            DateOnly periodEnd,
            decimal quantity,
            int residentCount = 1,
            CancellationToken cancellationToken = default) =>
            type == TariffType.Water
                ? throw new InvalidOperationException("Water tariff is unavailable.")
                : inner.SnapshotForPeriodAsync(
                    billingPeriodId, type, scopeCode, periodStart, periodEnd,
                    quantity, residentCount, cancellationToken);

        public Task<TariffDto?> GetPeriodSnapshotAsync(
            int snapshotId,
            CancellationToken cancellationToken = default) =>
            inner.GetPeriodSnapshotAsync(snapshotId, cancellationToken);
    }
}
