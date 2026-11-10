using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Properties;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Invoices;

public sealed class InvoiceQueryServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int Year = 2026;

    [SqlServerFact]
    public async Task GetInvoicesAsync_ChuTroOnlySeesOwnProperties()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);

        var resultA = await service.GetInvoicesAsync(
            new InvoiceFilter(),
            new PropertyCaller(data.ChuTroAId, Roles.ChuTro));
        var resultB = await service.GetInvoicesAsync(
            new InvoiceFilter(),
            new PropertyCaller(data.ChuTroBId, Roles.ChuTro));

        Assert.Equal([data.InvoiceAOctoberId, data.InvoiceASeptemberId], resultA.Items.Select(item => item.Id));
        Assert.Equal([data.InvoiceBOctoberId], resultB.Items.Select(item => item.Id));
        Assert.Equal(2, resultA.Summary.IssuedCount + resultA.Summary.PaidCount);
        Assert.Equal(0, resultA.Summary.DraftCount);
        Assert.Equal(data.InvoiceAOctoberTotal, resultA.Summary.OutstandingAmount);
    }

    [SqlServerFact]
    public async Task GetInvoicesAsync_QuanLyOnlySeesAssignedProperty()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);

        var result = await service.GetInvoicesAsync(
            new InvoiceFilter(),
            new PropertyCaller(data.QuanLyId, Roles.QuanLy));

        Assert.Equal([data.InvoiceAOctoberId, data.InvoiceASeptemberId], result.Items.Select(item => item.Id));
        Assert.DoesNotContain(result.Items, item => item.ContractId == data.ContractBId);
    }

    [SqlServerFact]
    public async Task GetInvoicesAsync_KhachThueOnlySeesOwnContract()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);

        var result = await service.GetInvoicesAsync(
            new InvoiceFilter(),
            new PropertyCaller(data.KhachThueId, Roles.KhachThue));

        Assert.Equal([data.InvoiceAOctoberId, data.InvoiceASeptemberId], result.Items.Select(item => item.Id));
        Assert.All(result.Items, item => Assert.Equal(data.ContractAId, item.ContractId));
    }

    [SqlServerFact]
    public async Task GetInvoicesAsync_AdminIsRejected()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetInvoicesAsync(
            new InvoiceFilter(),
            new PropertyCaller(data.AdminId, Roles.Admin)));
    }

    [SqlServerFact]
    public async Task GetInvoicesAsync_FiltersByPeriodContractAndStatus()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);
        var caller = new PropertyCaller(data.ChuTroAId, Roles.ChuTro);

        var october = await service.GetInvoicesAsync(
            new InvoiceFilter { Year = Year, Month = 10 },
            caller);
        var september = await service.GetInvoicesAsync(
            new InvoiceFilter { Year = Year, Month = 9 },
            caller);
        var paid = await service.GetInvoicesAsync(
            new InvoiceFilter { Status = InvoiceStatus.Paid },
            caller);
        var otherContract = await service.GetInvoicesAsync(
            new InvoiceFilter { ContractId = data.ContractBId },
            caller);

        Assert.Equal([data.InvoiceAOctoberId], october.Items.Select(item => item.Id));
        Assert.Equal([data.InvoiceASeptemberId], september.Items.Select(item => item.Id));
        Assert.Equal([data.InvoiceASeptemberId], paid.Items.Select(item => item.Id));
        Assert.Empty(otherContract.Items);
        Assert.Equal(0, otherContract.TotalCount);
    }

    [SqlServerFact]
    public async Task GetInvoicesAsync_FiltersByKeywordAmountAndPaymentState()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);
        var caller = new PropertyCaller(data.ChuTroAId, Roles.ChuTro);

        var byRoom = await service.GetInvoicesAsync(new InvoiceFilter { Keyword = "A101" }, caller);
        var byTenant = await service.GetInvoicesAsync(new InvoiceFilter { Keyword = "Khách thuê A" }, caller);
        var byContract = await service.GetInvoicesAsync(new InvoiceFilter { Keyword = "HD-A-2026" }, caller);
        var byCode = await service.GetInvoicesAsync(
            new InvoiceFilter { Keyword = $"HD-{data.InvoiceASeptemberId}" },
            caller);
        var byAmount = await service.GetInvoicesAsync(
            new InvoiceFilter { MinAmount = 2_950_000m },
            caller);
        var partial = await service.GetInvoicesAsync(
            new InvoiceFilter { PaymentState = InvoicePaymentState.Partial },
            caller);

        Assert.Equal([data.InvoiceAOctoberId, data.InvoiceASeptemberId], byRoom.Items.Select(item => item.Id));
        Assert.Equal([data.InvoiceAOctoberId, data.InvoiceASeptemberId], byTenant.Items.Select(item => item.Id));
        Assert.Equal([data.InvoiceAOctoberId, data.InvoiceASeptemberId], byContract.Items.Select(item => item.Id));
        Assert.Equal([data.InvoiceASeptemberId], byCode.Items.Select(item => item.Id));
        Assert.Equal([data.InvoiceASeptemberId], byAmount.Items.Select(item => item.Id));
        Assert.Empty(partial.Items);
    }

    [SqlServerFact]
    public async Task GetInvoicesAsync_PeriodFilterUsesInclusiveOverlap()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);

        var result = await service.GetInvoicesAsync(
            new InvoiceFilter { Year = Year, Month = 10 },
            new PropertyCaller(data.ChuTroAId, Roles.ChuTro));

        // Kỳ 01/10–31/10 nằm trọn trong tháng 10 và không lẫn hóa đơn tháng 9.
        Assert.Single(result.Items);
        Assert.Equal(data.InvoiceAOctoberId, result.Items[0].Id);
    }

    [SqlServerFact]
    public async Task GetInvoiceAsync_ReturnsOwnInvoiceWithOrderedItems()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);

        var invoice = await service.GetInvoiceAsync(
            data.InvoiceAOctoberId,
            new PropertyCaller(data.ChuTroAId, Roles.ChuTro));

        Assert.Equal(data.ContractAId, invoice.ContractId);
        Assert.Equal("HD-A-2026", invoice.ContractNumber);
        Assert.Equal("A101", invoice.RoomCode);
        Assert.Equal(TestDataSeeder.PropertyAName, invoice.PropertyName);
        Assert.Equal(["Khách thuê A"], invoice.TenantNames);
        Assert.Equal(2, invoice.Items.Count);
        Assert.Equal(InvoiceItemType.Rent, invoice.Items[0].ItemType);
        Assert.Equal(InvoiceItemType.Discount, invoice.Items[1].ItemType);
        Assert.Equal(-100_000m, invoice.Items[1].Amount);
        Assert.Equal(data.InvoiceAOctoberTotal, invoice.TotalAmount);
        Assert.Equal(0m, invoice.PaidAmount);
        Assert.Equal(data.InvoiceAOctoberTotal, invoice.OutstandingAmount);
    }

    [SqlServerFact]
    public async Task GetInvoiceAsync_OutsideScopeLooksLikeNotFound()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetInvoiceAsync(
            data.InvoiceAOctoberId,
            new PropertyCaller(data.ChuTroBId, Roles.ChuTro)));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetInvoiceAsync(
            data.InvoiceBOctoberId,
            new PropertyCaller(data.KhachThueId, Roles.KhachThue)));

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetInvoiceAsync(
            999_999,
            new PropertyCaller(data.ChuTroAId, Roles.ChuTro)));
    }

    [SqlServerFact]
    public async Task GetContractOptionsAsync_IsLimitedToCallerScope()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        var service = new InvoiceQueryService(db);

        var optionsA = await service.GetContractOptionsAsync(
            new PropertyCaller(data.ChuTroAId, Roles.ChuTro));
        var optionsB = await service.GetContractOptionsAsync(
            new PropertyCaller(data.ChuTroBId, Roles.ChuTro));

        Assert.Equal([data.ContractAId], optionsA.Select(option => option.ContractId));
        Assert.Equal([data.ContractBId], optionsB.Select(option => option.ContractId));
        Assert.Equal("A101", optionsA[0].RoomCode);
        Assert.Equal(TestDataSeeder.PropertyAName, optionsA[0].PropertyName);
    }

    [SqlServerFact]
    public async Task UniqueIndex_RejectsDuplicateContractAndPeriodStart()
    {
        var data = await SeedInvoicesAsync();

        await using var db = fixture.CreateContext();
        db.Invoices.Add(new Invoice
        {
            ContractId = data.ContractAId,
            PeriodStart = new DateOnly(Year, 10, 1),
            PeriodEnd = new DateOnly(Year, 10, 31),
            Status = InvoiceStatus.Draft,
            TotalAmount = 500_000m,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private async Task<SeedInvoiceData> SeedInvoicesAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var contractA = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-A-2026",
            RoomId = ids.RoomAId,
            StartDate = new DateTime(Year, 1, 1),
            EndDate = new DateTime(Year, 12, 31),
            SnapshotPrice = TestDataSeeder.RoomAStandardPrice,
            DepositAmount = TestDataSeeder.RoomAStandardPrice,
            Status = ContractStatus.Pending,
        };
        var contractB = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-B-2026",
            RoomId = ids.RoomBId,
            StartDate = new DateTime(Year, 1, 1),
            EndDate = new DateTime(Year, 12, 31),
            SnapshotPrice = TestDataSeeder.RoomBStandardPrice,
            DepositAmount = TestDataSeeder.RoomBStandardPrice,
            Status = ContractStatus.Pending,
        };
        db.Contracts.AddRange(contractA, contractB);
        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contractA.ContractId,
            TenantId = ids.TenantAId,
        });
        await db.SaveChangesAsync();

        const decimal invoiceAOctoberTotal = 2_900_000m;
        var invoiceAOctober = new Invoice
        {
            ContractId = contractA.ContractId,
            PeriodStart = new DateOnly(Year, 10, 1),
            PeriodEnd = new DateOnly(Year, 10, 31),
            Status = InvoiceStatus.Issued,
            TotalAmount = invoiceAOctoberTotal,
            Items =
            {
                new InvoiceItem
                {
                    ItemType = InvoiceItemType.Rent,
                    Description = "Tiền thuê tháng 10",
                    Unit = "tháng",
                    Quantity = 1m,
                    UnitPrice = 3_000_000m,
                    Amount = 3_000_000m,
                    DisplayOrder = 1,
                },
                new InvoiceItem
                {
                    ItemType = InvoiceItemType.Discount,
                    Description = "Giảm giá",
                    Quantity = 1m,
                    UnitPrice = 100_000m,
                    Amount = -100_000m,
                    DisplayOrder = 2,
                },
            },
        };
        var invoiceASeptember = new Invoice
        {
            ContractId = contractA.ContractId,
            PeriodStart = new DateOnly(Year, 9, 1),
            PeriodEnd = new DateOnly(Year, 9, 30),
            Status = InvoiceStatus.Paid,
            TotalAmount = 3_000_000m,
        };
        var invoiceBOctober = new Invoice
        {
            ContractId = contractB.ContractId,
            PeriodStart = new DateOnly(Year, 10, 1),
            PeriodEnd = new DateOnly(Year, 10, 31),
            Status = InvoiceStatus.Draft,
            TotalAmount = 4_000_000m,
        };
        db.Invoices.AddRange(invoiceAOctober, invoiceASeptember, invoiceBOctober);
        await db.SaveChangesAsync();

        return new SeedInvoiceData(
            ids.AdminId,
            ids.ChuTroAId,
            ids.ChuTroBId,
            ids.QuanLyId,
            ids.KhachThueId,
            contractA.ContractId,
            contractB.ContractId,
            invoiceAOctober.Id,
            invoiceASeptember.Id,
            invoiceBOctober.Id,
            invoiceAOctoberTotal);
    }

    private sealed record SeedInvoiceData(
        string AdminId,
        string ChuTroAId,
        string ChuTroBId,
        string QuanLyId,
        string KhachThueId,
        Guid ContractAId,
        Guid ContractBId,
        int InvoiceAOctoberId,
        int InvoiceASeptemberId,
        int InvoiceBOctoberId,
        decimal InvoiceAOctoberTotal);
}
