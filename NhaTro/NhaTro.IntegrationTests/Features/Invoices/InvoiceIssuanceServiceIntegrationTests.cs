using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Data.Tariffs;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Properties;
using Web.Features.Utilities;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Services;

namespace NhaTro.IntegrationTests.Features.Invoices;

/// <summary>
/// VHT-137 (3B) — hóa đơn thật: tổng bằng tiền thuê + chi phí kỳ chỉ số VHT-51 đã chốt,
/// dùng snapshot điện/nước/dịch vụ đã lưu, và gửi lặp không tạo hóa đơn thứ hai.
/// </summary>
public sealed class InvoiceIssuanceServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int Year = 2026;
    private static readonly DateOnly PeriodStart = new(Year, 10, 1);
    private static readonly DateOnly PeriodEnd = new(Year, 10, 31);

    [SqlServerFact]
    public async Task PreviewPeriodAsync_ComposesRentUtilitiesAndServicesFromRealSources()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var source = await NewService(db).PreviewPeriodAsync(
            data.ContractId, PeriodStart, PeriodEnd, Owner(data));

        Assert.Equal(5, source.Items.Count);
        Assert.Equal(
            [
                InvoiceItemType.Rent,
                InvoiceItemType.Electric,
                InvoiceItemType.Water,
                InvoiceItemType.Service,
                InvoiceItemType.Service,
            ],
            source.Items.Select(item => item.ItemType).ToArray());

        Assert.Equal(3_000_000m, source.Items[0].Amount);
        // 110 kWh: 50×1.984 + 50×2.050 + 10×2.380 = 225.500.
        Assert.Equal(225_500m, source.Items[1].Amount);
        Assert.Equal(110m, source.Items[1].Quantity);
        // 6 m³: 4×6.700 + 2×12.900 = 52.600.
        Assert.Equal(52_600m, source.Items[2].Amount);
        Assert.Equal(6m, source.Items[2].Quantity);

        Assert.Equal("Internet", source.Items[3].Description);
        Assert.Equal(100_000m, source.Items[3].Amount);
        Assert.Equal("Rác", source.Items[4].Description);
        Assert.Equal(20_000m, source.Items[4].Amount);

        Assert.Equal(3_398_100m, source.TotalAmount);
        Assert.Equal(data.PeriodId, source.UtilityReadingId);
        Assert.Equal("A101", source.RoomCode);
    }

    [SqlServerFact]
    public async Task CreateAndIssueAsync_PersistsSnapshotLinesAndTotal()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var invoice = await NewService(db).CreateAndIssueAsync(
            data.ContractId, PeriodStart, PeriodEnd, Owner(data));

        Assert.Equal(InvoiceStatus.Issued, invoice.Status);
        Assert.NotNull(invoice.IssuedAt);
        Assert.Equal(3_398_100m, invoice.TotalAmount);
        Assert.Equal(invoice.TotalAmount, invoice.Items.Sum(item => item.Amount));

        await using var verify = fixture.CreateContext();
        var stored = await verify.Invoices
            .Include(item => item.Items)
            .SingleAsync(item => item.Id == invoice.Id);
        Assert.Equal(3_398_100m, stored.TotalAmount);
        Assert.Equal(5, stored.Items.Count);
        Assert.Equal(225_500m, stored.Items.Single(item => item.ItemType == InvoiceItemType.Electric).Amount);
    }

    [SqlServerFact]
    public async Task CreateDraftAsync_RepeatedSend_ReturnsSameInvoiceWithoutSecondRecord()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var first = await service.CreateDraftAsync(data.ContractId, PeriodStart, PeriodEnd, Owner(data));
        var second = await service.CreateDraftAsync(data.ContractId, PeriodStart, PeriodEnd, Owner(data));

        Assert.Equal(first.Id, second.Id);

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
        Assert.Equal(first.Items.Count, await verify.InvoiceItems.CountAsync(item => item.InvoiceId == first.Id));
    }

    [SqlServerFact]
    public async Task CreateAndIssueAsync_RepeatedSend_ReturnsSameIssuedInvoice()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var first = await service.CreateAndIssueAsync(data.ContractId, PeriodStart, PeriodEnd, Owner(data));
        var second = await service.CreateAndIssueAsync(data.ContractId, PeriodStart, PeriodEnd, Owner(data));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(InvoiceStatus.Issued, second.Status);

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_UsesLockedUtilitySnapshotInsteadOfRecomputing()
    {
        var data = await SeedAsync();
        await using (var db = fixture.CreateContext())
        {
            await db.UtilityBillingPeriodSnapshots
                .Where(snapshot => snapshot.BillingPeriodId == data.PeriodId
                    && snapshot.Type == TariffType.Electric)
                .ExecuteUpdateAsync(setters => setters.SetProperty(snapshot => snapshot.Total, 999_999m));
        }

        await using var verifyDb = fixture.CreateContext();
        var source = await NewService(verifyDb).PreviewPeriodAsync(
            data.ContractId, PeriodStart, PeriodEnd, Owner(data));

        var electric = source.Items.Single(item => item.ItemType == InvoiceItemType.Electric);
        Assert.Equal(999_999m, electric.Amount);
        Assert.Equal(3_000_000m + 999_999m + 52_600m + 120_000m, source.TotalAmount);
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_ExcludesServicesOutsideThePeriod()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var source = await NewService(db).PreviewPeriodAsync(
            data.ContractId, PeriodStart, PeriodEnd, Owner(data));

        Assert.DoesNotContain(source.Items, item => item.Description == "Nước uống");
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_ExcludesMeteredServiceCatalogToAvoidDoubleCharging()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var source = await NewService(db).PreviewPeriodAsync(
            data.ContractId, PeriodStart, PeriodEnd, Owner(data));

        Assert.DoesNotContain(source.Items, item => item.Description == "Điện dịch vụ");
        Assert.Single(source.Items, item => item.ItemType == InvoiceItemType.Electric);
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_RejectsPeriodWithoutReadings()
    {
        var data = await SeedAsync(saveReadings: false);

        await using var db = fixture.CreateContext();
        var exception = await Assert.ThrowsAsync<InvoiceValidationException>(() =>
            NewService(db).PreviewPeriodAsync(data.ContractId, PeriodStart, PeriodEnd, Owner(data)));

        Assert.Equal(InvoiceErrorCodes.UtilityPeriodMismatch, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_RejectsPeriodMissingServiceSnapshots()
    {
        var data = await SeedAsync();
        await using (var db = fixture.CreateContext())
        {
            await db.UtilityServicePeriodSnapshots.ExecuteDeleteAsync();
            await db.UtilityReadingPeriods
                .Where(period => period.Id == data.PeriodId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(period => period.HasServiceSnapshots, false));
        }

        await using var verifyDb = fixture.CreateContext();
        var exception = await Assert.ThrowsAsync<InvoiceValidationException>(() =>
            NewService(verifyDb).PreviewPeriodAsync(data.ContractId, PeriodStart, PeriodEnd, Owner(data)));

        Assert.Equal(InvoiceErrorCodes.UtilityPeriodMismatch, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_RejectsReadingWithDifferentEndDate()
    {
        var data = await SeedAsync(periodEnd: PeriodStart.AddDays(29));

        await using var db = fixture.CreateContext();
        var exception = await Assert.ThrowsAsync<InvoiceValidationException>(() =>
            NewService(db).PreviewPeriodAsync(data.ContractId, PeriodStart, PeriodEnd, Owner(data)));

        Assert.Equal(InvoiceErrorCodes.UtilityPeriodMismatch, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_RejectsTenantAndAdmin()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var tenant = await Assert.ThrowsAsync<InvoiceAuthorizationException>(() =>
            service.PreviewPeriodAsync(
                data.ContractId, PeriodStart, PeriodEnd,
                new PropertyCaller(data.KhachThueId, Roles.KhachThue)));
        var admin = await Assert.ThrowsAsync<InvoiceAuthorizationException>(() =>
            service.PreviewPeriodAsync(
                data.ContractId, PeriodStart, PeriodEnd,
                new PropertyCaller(data.AdminId, Roles.Admin)));

        Assert.Equal(InvoiceErrorCodes.Forbidden, tenant.ErrorCode);
        Assert.Equal(InvoiceErrorCodes.Forbidden, admin.ErrorCode);
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_HidesContractOutsideCallerScope()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var exception = await Assert.ThrowsAsync<InvoiceNotFoundException>(() =>
            NewService(db).PreviewPeriodAsync(
                data.ContractId, PeriodStart, PeriodEnd,
                new PropertyCaller(data.ChuTroBId, Roles.ChuTro)));

        Assert.Equal(InvoiceErrorCodes.ContractNotFound, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task PreviewPeriodAsync_RejectsPeriodOutsideContract()
    {
        var data = await SeedAsync();

        await using var db = fixture.CreateContext();
        var exception = await Assert.ThrowsAsync<InvoiceValidationException>(() =>
            NewService(db).PreviewPeriodAsync(
                data.ContractId,
                new DateOnly(Year + 1, 1, 1),
                new DateOnly(Year + 1, 1, 31),
                Owner(data)));

        Assert.Equal(InvoiceErrorCodes.InvalidPeriod, exception.ErrorCode);
    }

    private static InvoiceIssuanceService NewService(ApplicationDbContext db)
    {
        var readings = new UtilityReadingService(db, new UtilityTariffService(db));
        return new InvoiceIssuanceService(db, new InvoiceService(db, new InvoiceQueryService(db)), readings);
    }

    private static PropertyCaller Owner(SeedData data) => new(data.ChuTroAId, Roles.ChuTro);

    private async Task<SeedData> SeedAsync(
        bool saveReadings = true,
        DateOnly? periodEnd = null)
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-VHT137-2026",
            RoomId = ids.RoomAId,
            StartDate = new DateTime(Year, 1, 1),
            EndDate = new DateTime(Year, 12, 31),
            SnapshotPrice = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = ids.TenantAId,
        });
        await db.SaveChangesAsync();

        var catalogs = new ServiceCatalogService(db);
        var owner = new PropertyCaller(ids.ChuTroAId, Roles.ChuTro);
        await catalogs.CreateOrUpdateServicePriceAsync(
            new CreateServiceCatalogDto
            {
                PropertyId = ids.PropertyAId,
                Name = "Internet",
                Unit = "tháng",
                CalculationMethod = "FixedMonthly",
                UnitPrice = 100_000m,
                EffectiveDate = PeriodStart,
            },
            owner);
        await catalogs.CreateOrUpdateServicePriceAsync(
            new CreateServiceCatalogDto
            {
                PropertyId = ids.PropertyAId,
                Name = "Rác",
                Unit = "người",
                CalculationMethod = "PerPerson",
                UnitPrice = 20_000m,
                EffectiveDate = PeriodStart,
            },
            owner);
        // Dịch vụ bán theo kWh không được ghép lại thành dòng dịch vụ: điện đã tính theo chỉ số.
        await catalogs.CreateOrUpdateServicePriceAsync(
            new CreateServiceCatalogDto
            {
                PropertyId = ids.PropertyAId,
                Name = "Điện dịch vụ",
                Unit = "kWh",
                CalculationMethod = "FixedMonthly",
                UnitPrice = 9_000m,
                EffectiveDate = PeriodStart,
            },
            owner);
        // Đơn giá có hiệu lực sau ngày đầu kỳ thì không được chụp vào kỳ này.
        await catalogs.CreateOrUpdateServicePriceAsync(
            new CreateServiceCatalogDto
            {
                PropertyId = ids.PropertyAId,
                Name = "Nước uống",
                Unit = "tháng",
                CalculationMethod = "FixedMonthly",
                UnitPrice = 50_000m,
                EffectiveDate = PeriodEnd.AddDays(1),
            },
            owner);

        var readings = new UtilityReadingService(db, new UtilityTariffService(db));
        var period = await readings.OpenPeriodAsync(
            contract.ContractId,
            PeriodStart,
            periodEnd ?? PeriodEnd,
            owner);
        if (saveReadings)
        {
            await readings.SaveReadingsAsync(period.Id, electricEnd: 110m, waterEnd: 6m, owner);
        }

        return new SeedData(
            ids.AdminId,
            ids.ChuTroAId,
            ids.ChuTroBId,
            ids.KhachThueId,
            contract.ContractId,
            period.Id);
    }

    private sealed record SeedData(
        string AdminId,
        string ChuTroAId,
        string ChuTroBId,
        string KhachThueId,
        Guid ContractId,
        int PeriodId);
}
