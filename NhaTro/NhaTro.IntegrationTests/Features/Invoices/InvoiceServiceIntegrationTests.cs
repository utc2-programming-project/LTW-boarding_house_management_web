using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Properties;

namespace NhaTro.IntegrationTests.Features.Invoices;

public sealed class InvoiceServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int Year = 2026;

    [SqlServerFact]
    public async Task CreateDraftAsync_FromSettledSource_SnapshotsLinesAndTotal()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var invoice = await service.CreateDraftAsync(
            Request(data.ContractId, DiscountItems()),
            Owner(data));

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(3_390_000m, invoice.TotalAmount);
        Assert.Equal(5, invoice.Items.Count);
        Assert.Equal(InvoiceItemType.Rent, invoice.Items[0].ItemType);
        Assert.Equal(250_000m, invoice.Items[1].Amount);
        Assert.Equal(-50_000m, invoice.Items[^1].Amount);
        Assert.Equal("Tiền thuê", invoice.Items[0].Description);

        await using var verify = fixture.CreateContext();
        var stored = await verify.Invoices
            .Include(item => item.Items)
            .SingleAsync(item => item.Id == invoice.Id);
        Assert.Equal(invoice.TotalAmount, stored.TotalAmount);
        Assert.Equal(invoice.Items.Select(item => item.Amount), stored.Items
            .OrderBy(item => item.DisplayOrder)
            .Select(item => item.Amount));
    }

    [SqlServerFact]
    public async Task CreateDraftAsync_DuplicateSend_ReturnsExistingInvoice()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var request = Request(data.ContractId, DiscountItems());

        var first = await service.CreateDraftAsync(request, Owner(data));
        var second = await service.CreateDraftAsync(request, Owner(data));

        Assert.Equal(first.Id, second.Id);

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
        Assert.Equal(first.Items.Count, await verify.InvoiceItems.CountAsync(item => item.InvoiceId == first.Id));
    }

    [SqlServerFact]
    public async Task CreateDraftAsync_DuplicatePeriodWithDifferentContent_IsRejected()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        await service.CreateDraftAsync(
            Request(data.ContractId, DiscountItems()),
            Owner(data));

        var exception = await Assert.ThrowsAsync<InvoiceOperationException>(() =>
            service.CreateDraftAsync(
                Request(data.ContractId, [RentItem(1_000_000m)]),
                Owner(data)));

        Assert.Equal(InvoiceErrorCodes.DuplicateInvoice, exception.ErrorCode);

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
    }

    [SqlServerFact]
    public async Task IssueAsync_PublishesOnce_AndRepeatedCallIsIdempotent()
    {
        var data = await SeedApprovedContractAsync();
        var now = new DateTimeOffset(2026, 11, 4, 10, 30, 0, TimeSpan.Zero);

        await using var db = fixture.CreateContext();
        var service = NewService(db, new FixedTimeProvider(now));

        var draft = await service.CreateDraftAsync(
            Request(data.ContractId, DiscountItems()),
            Owner(data));
        var issued = await service.IssueAsync(draft.Id, Owner(data));
        var repeated = await service.IssueAsync(draft.Id, Owner(data));

        Assert.Equal(InvoiceStatus.Issued, issued.Status);
        Assert.Equal(now.UtcDateTime, issued.IssuedAt);
        Assert.Equal(issued.Id, repeated.Id);
        Assert.Equal(issued.Status, repeated.Status);

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
        Assert.Equal(InvoiceStatus.Issued, (await verify.Invoices.SingleAsync(item => item.Id == draft.Id)).Status);
    }

    [SqlServerFact]
    public async Task CreateAndIssueAsync_RollsBackDraft_WhenIssueFails()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var realQuery = new InvoiceQueryService(db);
        var service = new InvoiceService(
            db,
            new FailOnSecondReadQueryService(realQuery),
            new FixedTimeProvider(new DateTimeOffset(2026, 11, 4, 10, 30, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAndIssueAsync(
                Request(data.ContractId, DiscountItems()),
                Owner(data)));

        await using var verify = fixture.CreateContext();
        Assert.Equal(0, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
        Assert.Equal(0, await verify.InvoiceItems.CountAsync());
    }

    [SqlServerFact]
    public async Task IssueAsync_RejectsDraftOnUnapprovedContract()
    {
        var data = await SeedApprovedContractAsync(status: ContractStatus.Pending);

        await using var db = fixture.CreateContext();
        var invoice = new Invoice
        {
            ContractId = data.ContractId,
            PeriodStart = new DateOnly(Year, 10, 1),
            PeriodEnd = new DateOnly(Year, 10, 31),
            Status = InvoiceStatus.Draft,
            TotalAmount = 3_000_000m,
            Items =
            {
                new InvoiceItem
                {
                    ItemType = InvoiceItemType.Rent,
                    Description = "Tiền thuê",
                    Unit = "tháng",
                    Quantity = 1m,
                    UnitPrice = 3_000_000m,
                    Amount = 3_000_000m,
                    DisplayOrder = 1,
                },
            },
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var service = NewService(db);
        var exception = await Assert.ThrowsAsync<InvoiceOperationException>(() =>
            service.IssueAsync(invoice.Id, Owner(data)));

        Assert.Equal(InvoiceErrorCodes.ContractNotApproved, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task CreateDraftAsync_RejectsContractOutsideCallerScope()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var exception = await Assert.ThrowsAsync<InvoiceNotFoundException>(() =>
            service.CreateDraftAsync(
                Request(data.ContractId, DiscountItems()),
                new PropertyCaller(data.ChuTroBId, Roles.ChuTro)));

        Assert.Equal(InvoiceErrorCodes.ContractNotFound, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task CreateDraftAsync_RejectsKhachThueAndAdmin()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var tenantException = await Assert.ThrowsAsync<InvoiceAuthorizationException>(() =>
            service.CreateDraftAsync(
                Request(data.ContractId, DiscountItems()),
                new PropertyCaller(data.KhachThueId, Roles.KhachThue)));
        var adminException = await Assert.ThrowsAsync<InvoiceAuthorizationException>(() =>
            service.CreateDraftAsync(
                Request(data.ContractId, DiscountItems()),
                new PropertyCaller(data.AdminId, Roles.Admin)));

        Assert.Equal(InvoiceErrorCodes.Forbidden, tenantException.ErrorCode);
        Assert.Equal(InvoiceErrorCodes.Forbidden, adminException.ErrorCode);
    }

    [SqlServerFact]
    public async Task CreateDraftAsync_RejectsUnapprovedContract()
    {
        var data = await SeedApprovedContractAsync(status: ContractStatus.Pending);

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var exception = await Assert.ThrowsAsync<InvoiceOperationException>(() =>
            service.CreateDraftAsync(
                Request(data.ContractId, DiscountItems()),
                Owner(data)));

        Assert.Equal(InvoiceErrorCodes.ContractNotApproved, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task CreateDraftAsync_RejectsPeriodOutsideContract()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var exception = await Assert.ThrowsAsync<InvoiceValidationException>(() =>
            service.CreateDraftAsync(
                new CreateInvoiceRequest
                {
                    ContractId = data.ContractId,
                    PeriodStart = new DateOnly(Year, 12, 1),
                    PeriodEnd = new DateOnly(Year + 1, 1, 31),
                    Items = DiscountItems(),
                },
                Owner(data)));

        Assert.Equal(InvoiceErrorCodes.InvalidPeriod, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task CreateDraftAsync_RejectsEmptyItems()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var exception = await Assert.ThrowsAsync<InvoiceValidationException>(() =>
            service.CreateDraftAsync(
                Request(data.ContractId, []),
                Owner(data)));

        Assert.Equal(InvoiceErrorCodes.ItemsRequired, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task IssueAsync_OutsideScopeLooksLikeNotFound()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var draft = await service.CreateDraftAsync(
            Request(data.ContractId, DiscountItems()),
            Owner(data));

        var exception = await Assert.ThrowsAsync<InvoiceNotFoundException>(() =>
            service.IssueAsync(draft.Id, new PropertyCaller(data.ChuTroBId, Roles.ChuTro)));

        Assert.Equal(InvoiceErrorCodes.InvoiceNotFound, exception.ErrorCode);
    }

    private static InvoiceService NewService(ApplicationDbContext db, TimeProvider? clock = null) =>
        new(db, new InvoiceQueryService(db), clock ?? TimeProvider.System);

    private static PropertyCaller Owner(SeedData data) =>
        new(data.ChuTroAId, Roles.ChuTro);

    private static CreateInvoiceRequest Request(
        Guid contractId,
        IReadOnlyList<InvoiceItemInputDto> items) =>
        new()
        {
            ContractId = contractId,
            PeriodStart = new DateOnly(Year, 10, 1),
            PeriodEnd = new DateOnly(Year, 10, 31),
            Items = items,
        };

    private static IReadOnlyList<InvoiceItemInputDto> DiscountItems() =>
    [
        RentItem(3_000_000m),
        new()
        {
            ItemType = InvoiceItemType.Electric,
            Description = "Điện kỳ 10/2026",
            Unit = "kWh",
            Quantity = 120m,
            UnitPrice = 2_083.333m,
            Amount = 250_000m,
        },
        new()
        {
            ItemType = InvoiceItemType.Water,
            Description = "Nước kỳ 10/2026",
            Unit = "m³",
            Quantity = 6m,
            UnitPrice = 15_000m,
            Amount = 90_000m,
        },
        new()
        {
            ItemType = InvoiceItemType.Service,
            Description = "Internet",
            Unit = "tháng",
            Quantity = 1m,
            UnitPrice = 100_000m,
        },
        new()
        {
            ItemType = InvoiceItemType.Discount,
            Description = "Giảm giá",
            Unit = "lần",
            Quantity = 1m,
            UnitPrice = 50_000m,
            Amount = 50_000m,
        },
    ];

    private static InvoiceItemInputDto RentItem(decimal amount) =>
        new()
        {
            ItemType = InvoiceItemType.Rent,
            Description = "Tiền thuê",
            Unit = "tháng",
            Quantity = 1m,
            UnitPrice = amount,
            Amount = amount,
        };

    private async Task<SeedData> SeedApprovedContractAsync(
        ContractStatus status = ContractStatus.Approved)
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-P6-2026",
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
            ids.AdminId,
            ids.ChuTroAId,
            ids.ChuTroBId,
            ids.KhachThueId,
            contract.ContractId);
    }

    private sealed record SeedData(
        string AdminId,
        string ChuTroAId,
        string ChuTroBId,
        string KhachThueId,
        Guid ContractId);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FailOnSecondReadQueryService(IInvoiceQueryService inner) : IInvoiceQueryService
    {
        private int _reads;

        public Task<InvoiceListResult> GetInvoicesAsync(
            InvoiceFilter filter,
            PropertyCaller caller,
            CancellationToken cancellationToken = default) =>
            inner.GetInvoicesAsync(filter, caller, cancellationToken);

        public Task<InvoiceDto> GetInvoiceAsync(
            int invoiceId,
            PropertyCaller caller,
            CancellationToken cancellationToken = default)
        {
            _reads++;
            if (_reads == 2)
            {
                throw new InvalidOperationException("Mô phỏng lỗi phát hành.");
            }

            return inner.GetInvoiceAsync(invoiceId, caller, cancellationToken);
        }

        public Task<IReadOnlyList<InvoiceContractOptionDto>> GetContractOptionsAsync(
            PropertyCaller caller,
            CancellationToken cancellationToken = default) =>
            inner.GetContractOptionsAsync(caller, cancellationToken);
    }
}
