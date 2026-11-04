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

/// <summary>
/// VHT-63 (đợt 3A) — test tiền và transaction P4–P6 trên SQL Server thật:
/// bộ số tính tay, snapshot nguồn, chống trùng kỳ, hai luồng lập/phát hành đồng thời,
/// rollback khi phát hành lỗi và sai quyền.
/// </summary>
public sealed class P6MoneyTransactionIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int Year = 2026;
    private static readonly DateOnly PeriodStart = new(Year, 10, 1);
    private static readonly DateOnly PeriodEnd = new(Year, 10, 31);

    // Bộ số tính tay của đợt 3A:
    // thuê 3.000.000 + điện 250.000 + nước 90.000 + internet 100.000 + rác 30.000 - giảm 50.000.
    private const decimal ExpectedTotal = 3_420_000m;

    [SqlServerFact]
    public async Task Tinh_tien_va_snapshot_dung_bo_so_tinh_tay()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var invoice = await service.CreateDraftAsync(
            Request(data.ContractId, SettledItems()),
            Owner(data));

        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(ExpectedTotal, invoice.TotalAmount);
        Assert.Equal(
            [3_000_000m, 250_000m, 90_000m, 100_000m, 30_000m, -50_000m],
            invoice.Items.Select(item => item.Amount));

        // Dòng điện có Amount nguồn 250.000 nhưng Quantity × UnitPrice chỉ 120.000:
        // service phải lưu đúng số nguồn đã chốt, không tính lại từ đơn giá.
        Assert.Equal(120m, invoice.Items[1].Quantity);
        Assert.Equal(1_000m, invoice.Items[1].UnitPrice);
        Assert.Equal(250_000m, invoice.Items[1].Amount);

        // Sau khi lập hóa đơn, nguồn P4/P5 đổi giá nhưng snapshot hóa đơn không được đổi.
        await using (var source = fixture.CreateContext())
        {
            var contract = await source.Contracts.SingleAsync(item => item.ContractId == data.ContractId);
            contract.SnapshotPrice = 9_999_999m;
            contract.DepositAmount = 1m;

            var room = await source.Rooms
                .IgnoreQueryFilters()
                .SingleAsync(item => item.Id == data.RoomId);
            room.StandardPrice = 8_888_888m;

            await source.SaveChangesAsync();
        }

        await using var reloadDb = fixture.CreateContext();
        var reloaded = await new InvoiceQueryService(reloadDb).GetInvoiceAsync(invoice.Id, Owner(data));

        Assert.Equal(ExpectedTotal, reloaded.TotalAmount);
        Assert.Equal(3_000_000m, reloaded.Items[0].Amount);
        Assert.Equal(250_000m, reloaded.Items[1].Amount);
        Assert.Equal(-50_000m, reloaded.Items[^1].Amount);
    }

    [SqlServerFact]
    public async Task Trung_ky_cung_du_lieu_khong_ghi_trung_va_khac_du_lieu_bi_chan()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var first = await service.CreateDraftAsync(
            Request(data.ContractId, SettledItems()),
            Owner(data));
        var repeated = await service.CreateDraftAsync(
            Request(data.ContractId, SettledItems()),
            Owner(data));

        Assert.Equal(first.Id, repeated.Id);

        var conflict = await Assert.ThrowsAsync<InvoiceOperationException>(() =>
            service.CreateDraftAsync(
                Request(data.ContractId, [RentItem(1_000_000m)]),
                Owner(data)));
        Assert.Equal(InvoiceErrorCodes.DuplicateInvoice, conflict.ErrorCode);

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
        Assert.Equal(6, await verify.InvoiceItems.CountAsync(item => item.InvoiceId == first.Id));
    }

    [SqlServerFact]
    public async Task Hai_luong_lap_cung_ky_dong_thoi_chi_ghi_mot_hoa_don()
    {
        var data = await SeedApprovedContractAsync();

        await using var db1 = fixture.CreateContext();
        await using var db2 = fixture.CreateContext();

        var results = await Task.WhenAll(
            NewService(db1).CreateDraftAsync(
                Request(data.ContractId, SettledItems()),
                Owner(data)),
            NewService(db2).CreateDraftAsync(
                Request(data.ContractId, SettledItems()),
                Owner(data)));

        Assert.Single(results.Select(result => result.Id).Distinct());

        await using var verify = fixture.CreateContext();
        var stored = await verify.Invoices
            .Include(item => item.Items)
            .SingleAsync(item => item.ContractId == data.ContractId);
        Assert.Equal(ExpectedTotal, stored.TotalAmount);
        Assert.Equal(6, stored.Items.Count);
        Assert.Equal(6, await verify.InvoiceItems.CountAsync(item => item.InvoiceId == stored.Id));
    }

    [SqlServerFact]
    public async Task Hai_luong_phat_hanh_dong_thoi_chi_doi_trang_thai_mot_lan()
    {
        var data = await SeedApprovedContractAsync();

        await using var setupDb = fixture.CreateContext();
        var draft = await NewService(setupDb).CreateDraftAsync(
            Request(data.ContractId, SettledItems()),
            Owner(data));

        await using var db1 = fixture.CreateContext();
        await using var db2 = fixture.CreateContext();

        var results = await Task.WhenAll(
            NewService(db1).IssueAsync(draft.Id, Owner(data)),
            NewService(db2).IssueAsync(draft.Id, Owner(data)));

        Assert.All(results, result => Assert.Equal(InvoiceStatus.Issued, result.Status));
        Assert.All(results, result => Assert.NotNull(result.IssuedAt));

        await using var verify = fixture.CreateContext();
        var stored = await verify.Invoices.SingleAsync(item => item.Id == draft.Id);
        Assert.Equal(InvoiceStatus.Issued, stored.Status);
        Assert.Equal(1, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
        Assert.Equal(6, await verify.InvoiceItems.CountAsync(item => item.InvoiceId == draft.Id));
    }

    [SqlServerFact]
    public async Task Rollback_khi_phat_hanh_loi_khong_de_lai_hoa_don_nhap()
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
                Request(data.ContractId, SettledItems()),
                Owner(data)));

        await using var verify = fixture.CreateContext();
        Assert.Equal(0, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
        Assert.Equal(0, await verify.InvoiceItems.CountAsync());
    }

    [SqlServerFact]
    public async Task Sai_quyen_khong_lap_va_khong_phat_hanh_duoc()
    {
        var data = await SeedApprovedContractAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var draft = await service.CreateDraftAsync(
            Request(data.ContractId, SettledItems()),
            Owner(data));

        foreach (var caller in new[]
                 {
                     new PropertyCaller(data.ChuTroBId, Roles.ChuTro),
                     new PropertyCaller(data.KhachThueId, Roles.KhachThue),
                     new PropertyCaller(data.AdminId, Roles.Admin),
                 })
        {
            var createError = await Assert.ThrowsAnyAsync<Exception>(() =>
                service.CreateDraftAsync(
                    Request(data.ContractId, SettledItems()),
                    caller));
            Assert.True(
                createError is InvoiceNotFoundException or InvoiceAuthorizationException,
                $"Sai quyền lập hóa đơn phải bị chặn, nhận: {createError.GetType().Name}");

            var issueError = await Assert.ThrowsAnyAsync<Exception>(() =>
                service.IssueAsync(draft.Id, caller));
            Assert.True(
                issueError is InvoiceNotFoundException or InvoiceAuthorizationException,
                $"Sai quyền phát hành phải bị chặn, nhận: {issueError.GetType().Name}");
        }

        await using var verify = fixture.CreateContext();
        var stored = await verify.Invoices.SingleAsync(item => item.Id == draft.Id);
        Assert.Equal(InvoiceStatus.Draft, stored.Status);
        Assert.Equal(1, await verify.Invoices.CountAsync(item => item.ContractId == data.ContractId));
    }

    private static InvoiceService NewService(ApplicationDbContext db) =>
        new(db, new InvoiceQueryService(db), TimeProvider.System);

    private static PropertyCaller Owner(SeedData data) =>
        new(data.ChuTroAId, Roles.ChuTro);

    private static CreateInvoiceRequest Request(
        Guid contractId,
        IReadOnlyList<InvoiceItemInputDto> items) =>
        new()
        {
            ContractId = contractId,
            PeriodStart = PeriodStart,
            PeriodEnd = PeriodEnd,
            Items = items,
        };

    private static IReadOnlyList<InvoiceItemInputDto> SettledItems() =>
    [
        RentItem(3_000_000m),
        new()
        {
            ItemType = InvoiceItemType.Electric,
            Description = "Điện kỳ 10/2026",
            Unit = "kWh",
            Quantity = 120m,
            UnitPrice = 1_000m,
            Amount = 250_000m,
        },
        new()
        {
            ItemType = InvoiceItemType.Water,
            Description = "Nước kỳ 10/2026",
            Unit = "m³",
            Quantity = 6m,
            UnitPrice = 15_000m,
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
            ItemType = InvoiceItemType.Service,
            Description = "Rác",
            Unit = "tháng",
            Quantity = 1m,
            UnitPrice = 30_000m,
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

    private async Task<SeedData> SeedApprovedContractAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-P6-TIEN-2026",
            RoomId = ids.RoomAId,
            StartDate = new DateTime(Year, 1, 1),
            EndDate = new DateTime(Year, 12, 31),
            SnapshotPrice = TestDataSeeder.RoomAStandardPrice,
            DepositAmount = TestDataSeeder.RoomAStandardPrice,
            Status = ContractStatus.Approved,
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
            ids.RoomAId,
            contract.ContractId);
    }

    private sealed record SeedData(
        string AdminId,
        string ChuTroAId,
        string ChuTroBId,
        string KhachThueId,
        int RoomId,
        Guid ContractId);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    /// <summary>
    /// Cố ý lỗi ở lần đọc thứ hai (sau khi đã tạo nháp, trong lúc phát hành) để kiểm
    /// transaction của CreateAndIssueAsync phải rollback cả hóa đơn và dòng.
    /// </summary>
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
                throw new InvalidOperationException("Mô phỏng lỗi phát hành sau khi đã tạo nháp.");
            }

            return inner.GetInvoiceAsync(invoiceId, caller, cancellationToken);
        }

        public Task<IReadOnlyList<InvoiceContractOptionDto>> GetContractOptionsAsync(
            PropertyCaller caller,
            CancellationToken cancellationToken = default) =>
            inner.GetContractOptionsAsync(caller, cancellationToken);
    }
}
