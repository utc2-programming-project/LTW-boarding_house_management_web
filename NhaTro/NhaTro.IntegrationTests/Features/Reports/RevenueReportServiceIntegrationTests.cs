using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Reports.DTOs;
using Web.Features.Reports.Services;

namespace NhaTro.IntegrationTests.Features.Reports;

/// <summary>
/// VHT-57 — đối chiếu doanh thu/công nợ P8 trên SQL Server thật:
/// chỉ tiền đã xác nhận là doanh thu, chờ/từ chối bị loại, công nợ theo hóa đơn
/// hợp lệ, lọc kỳ/phạm vi, hai cơ sở ngày thu/kỳ hóa đơn và D5 chặn vai trò khác.
/// </summary>
public sealed class RevenueReportServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const decimal Rent = 3_000_000m;

    private static readonly DateOnly OctoberStart = new(2026, 10, 1);
    private static readonly DateOnly OctoberEnd = new(2026, 10, 31);
    private static readonly DateOnly SeptemberStart = new(2026, 9, 1);
    private static readonly DateOnly SeptemberEnd = new(2026, 9, 30);
    private static readonly DateOnly NovemberStart = new(2026, 11, 1);
    private static readonly DateOnly NovemberEnd = new(2026, 11, 30);

    private sealed record Seed(
        string OwnerAId,
        string OwnerBId,
        string ManagerId,
        string AdminId,
        string TenantUserId,
        int PropertyAId,
        int PropertyBId,
        int PaidOctoberInvoiceId,
        int PendingOctoberInvoiceId,
        int RejectedOctoberInvoiceId,
        int SeptemberInvoiceId);

    [SqlServerFact]
    public async Task Bao_cao_thang_10_chi_tinh_tien_da_xac_nhan_va_cong_no_hoa_don_hop_le()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        var report = await NewService(db).GetAsync(
            new RevenueReportFilter(OctoberStart, OctoberEnd),
            Owner(seed));

        // Chỉ hóa đơn hợp lệ trong kỳ: 1 đã thu + 1 chờ + 1 từ chối. Bản nháp và nhà B bị loại.
        Assert.Equal(3, report.InvoiceCount);
        Assert.Equal(2_000_000m, report.TotalRevenue);
        Assert.Equal(4_500_000m, report.Outstanding);
        Assert.Equal(6_500_000m, report.TotalInvoiced);
        Assert.Equal(1, report.PaidInvoiceCount);
        Assert.Equal(3_000_000m, report.PendingAmount);
        Assert.Equal(1_500_000m, report.RejectedAmount);
        Assert.Equal(RevenueDateBasis.InvoicePeriod, report.DateBasis);

        // Yêu cầu chờ/từ chối không bao giờ là doanh thu.
        Assert.DoesNotContain(report.Lines, line =>
            line.InvoiceId == seed.PendingOctoberInvoiceId && line.Paid > 0m);
        Assert.DoesNotContain(report.Lines, line =>
            line.InvoiceId == seed.RejectedOctoberInvoiceId && line.Paid > 0m);

        // Tổng báo cáo khớp từng dòng và từng nhóm đối chiếu.
        Assert.Equal(report.TotalRevenue, report.Lines.Sum(line => line.RecognizedRevenue));
        Assert.Equal(report.Outstanding, report.Lines.Sum(line => line.Outstanding));
        Assert.Equal(report.TotalInvoiced, report.Lines.Sum(line => line.Total));
        Assert.NotNull(report.Periods);
        Assert.NotNull(report.Properties);
        Assert.Equal(report.TotalRevenue, report.Periods!.Sum(group => group.RecognizedRevenue));
        Assert.Equal(report.Outstanding, report.Periods!.Sum(group => group.Outstanding));
        Assert.Equal(report.TotalRevenue, report.Properties!.Sum(group => group.RecognizedRevenue));
        Assert.Equal(report.Outstanding, report.Properties!.Sum(group => group.Outstanding));
        Assert.Single(report.Periods!);
        Assert.Equal("2026-10", report.Periods![0].PeriodKey);
        Assert.Equal(seed.PropertyAId, Assert.Single(report.Properties!).PropertyId);
    }

    [SqlServerFact]
    public async Task Loc_theo_ky_hóa_don_va_pham_vi_nha()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var september = await service.GetAsync(
            new RevenueReportFilter(SeptemberStart, SeptemberEnd),
            Owner(seed));

        Assert.Equal(1, september.InvoiceCount);
        Assert.Equal(3_000_000m, september.TotalRevenue);
        Assert.Equal(0m, september.Outstanding);
        Assert.Equal(3_000_000m, september.TotalInvoiced);
        Assert.Equal(1, september.PaidInvoiceCount);

        // Lọc đúng nhà A của chủ A.
        var propertyA = await service.GetAsync(
            new RevenueReportFilter(OctoberStart, OctoberEnd, seed.PropertyAId),
            Owner(seed));
        Assert.Equal(3, propertyA.InvoiceCount);

        // Nhà B thuộc chủ B: Chủ trọ A lọc sang phải rỗng, không lộ số liệu.
        var propertyB = await service.GetAsync(
            new RevenueReportFilter(OctoberStart, OctoberEnd, seed.PropertyBId),
            Owner(seed));
        Assert.Empty(propertyB.Lines);
        Assert.Equal(0m, propertyB.TotalRevenue);
        Assert.Equal(0m, propertyB.Outstanding);
        Assert.Equal(0, propertyB.InvoiceCount);
    }

    [SqlServerFact]
    public async Task Co_so_ngay_thu_ghi_nhan_theo_ReviewedAt_con_co_so_ky_hoa_don_giu_nguyen()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var byInvoicePeriod = await service.GetAsync(
            new RevenueReportFilter(OctoberStart, OctoberEnd),
            Owner(seed));
        var byPaymentDate = await service.GetAsync(
            new RevenueReportFilter(
                OctoberStart,
                OctoberEnd,
                DateBasis: RevenueDateBasis.PaymentDate),
            Owner(seed));

        // Hóa đơn tháng 9 đã thu nhưng xác nhận trong tháng 10: chỉ cơ sở ngày thu mới tính.
        Assert.Equal(2_000_000m, byInvoicePeriod.TotalRevenue);
        Assert.Equal(5_000_000m, byPaymentDate.TotalRevenue);
        Assert.Equal(RevenueDateBasis.PaymentDate, byPaymentDate.DateBasis);
        Assert.Equal(
            byPaymentDate.TotalRevenue,
            byPaymentDate.Lines.Sum(line => line.RecognizedRevenue));
        Assert.Equal(
            byPaymentDate.TotalRevenue,
            byPaymentDate.Properties!.Sum(group => group.RecognizedRevenue));

        // Công nợ vẫn theo hóa đơn hợp lệ trong kỳ, không phụ thuộc cơ sở doanh thu.
        Assert.Equal(byInvoicePeriod.Outstanding, byPaymentDate.Outstanding);
    }

    [SqlServerFact]
    public async Task Chi_Chu_tro_duoc_xem_bao_cao_tong_hop_theo_D5()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var filter = new RevenueReportFilter(OctoberStart, OctoberEnd);

        await Assert.ThrowsAsync<RevenueReportAuthorizationException>(
            () => service.GetAsync(filter, new PropertyCaller(seed.AdminId, Roles.Admin)));
        await Assert.ThrowsAsync<RevenueReportAuthorizationException>(
            () => service.GetAsync(filter, new PropertyCaller(seed.ManagerId, Roles.QuanLy)));
        await Assert.ThrowsAsync<RevenueReportAuthorizationException>(
            () => service.GetAsync(filter, new PropertyCaller(seed.TenantUserId, Roles.KhachThue)));
    }

    private static PropertyCaller Owner(Seed seed) =>
        new(seed.OwnerAId, Roles.ChuTro);

    private static RevenueReportService NewService(ApplicationDbContext db) => new(db);

    private async Task<Seed> PrepareAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var contractA = AddContract(db, "HD-A-T10", ids.RoomAId);
        var contractA2 = AddContract(db, "HD-A-T10-PENDING", ids.RoomAId);
        var contractA3 = AddContract(db, "HD-A-T10-REJECTED", ids.RoomAId);
        var contractASep = AddContract(db, "HD-A-T09", ids.RoomAId);
        var contractADraft = AddContract(db, "HD-A-DRAFT", ids.RoomAId);
        var contractB = AddContract(db, "HD-B-T10", ids.RoomBId);
        await db.SaveChangesAsync();

        var paidOctober = NewInvoice(contractA.ContractId, OctoberStart, OctoberEnd, 2_000_000m, InvoiceStatus.Paid);
        var pendingOctober = NewInvoice(contractA2.ContractId, OctoberStart, OctoberEnd, Rent, InvoiceStatus.Issued);
        var rejectedOctober = NewInvoice(contractA3.ContractId, OctoberStart, OctoberEnd, 1_500_000m, InvoiceStatus.Issued);
        var paidSeptember = NewInvoice(contractASep.ContractId, SeptemberStart, SeptemberEnd, Rent, InvoiceStatus.Paid);
        var draftInvoice = NewInvoice(contractADraft.ContractId, OctoberStart, OctoberEnd, 9_000_000m, InvoiceStatus.Draft);
        var paidOctoberB = NewInvoice(contractB.ContractId, OctoberStart, OctoberEnd, 4_000_000m, InvoiceStatus.Paid);
        db.Invoices.AddRange(
            paidOctober, pendingOctober, rejectedOctober, paidSeptember, draftInvoice, paidOctoberB);
        await db.SaveChangesAsync();

        // Tháng 10: một khoản đã xác nhận, một khoản chờ, một khoản bị từ chối.
        db.Payments.AddRange(
            NewPayment(paidOctober.Id, 2_000_000m, PaymentStatus.Confirmed, new DateTime(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc), ids.ChuTroAId),
            NewPayment(pendingOctober.Id, Rent, PaymentStatus.Pending, null, null),
            NewPayment(rejectedOctober.Id, 1_500_000m, PaymentStatus.Rejected, new DateTime(2026, 10, 12, 3, 0, 0, DateTimeKind.Utc), ids.ChuTroAId),
            // Xác nhận tháng 10 cho hóa đơn tháng 9 — khác biệt giữa hai cơ sở ngày.
            NewPayment(paidSeptember.Id, Rent, PaymentStatus.Confirmed, new DateTime(2026, 10, 5, 3, 0, 0, DateTimeKind.Utc), ids.ChuTroAId),
            // Bản nháp có payment Confirmed cũng phải bị loại khỏi báo cáo.
            NewPayment(draftInvoice.Id, 9_000_000m, PaymentStatus.Confirmed, new DateTime(2026, 10, 6, 3, 0, 0, DateTimeKind.Utc), ids.ChuTroAId),
            // Nhà B không thuộc phạm vi chủ A.
            NewPayment(paidOctoberB.Id, 4_000_000m, PaymentStatus.Confirmed, new DateTime(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc), ids.ChuTroBId));
        await db.SaveChangesAsync();

        return new Seed(
            ids.ChuTroAId,
            ids.ChuTroBId,
            ids.QuanLyId,
            ids.AdminId,
            ids.KhachThueId,
            ids.PropertyAId,
            ids.PropertyBId,
            paidOctober.Id,
            pendingOctober.Id,
            rejectedOctober.Id,
            paidSeptember.Id);
    }

    private static Contract AddContract(ApplicationDbContext db, string number, int roomId)
    {
        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = number,
            RoomId = roomId,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            SnapshotPrice = Rent,
            DepositAmount = Rent,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        return contract;
    }

    private static Invoice NewInvoice(
        Guid contractId,
        DateOnly start,
        DateOnly end,
        decimal total,
        InvoiceStatus status) => new()
    {
        ContractId = contractId,
        PeriodStart = start,
        PeriodEnd = end,
        TotalAmount = total,
        Status = status,
    };

    private static Payment NewPayment(
        int invoiceId,
        decimal amount,
        PaymentStatus status,
        DateTime? reviewedAt,
        string? reviewerId) => new()
    {
        InvoiceId = invoiceId,
        Amount = amount,
        TransferredAt = reviewedAt?.AddHours(-1) ?? DateTime.UtcNow,
        Status = status,
        RequestedAt = reviewedAt?.AddHours(-2) ?? DateTime.UtcNow,
        RequestedByUserId = "khach-thue",
        ReviewedAt = reviewedAt,
        ReviewedByUserId = reviewerId,
        RejectionReason = status == PaymentStatus.Rejected ? "Minh chứng không hợp lệ" : null,
    };
}
