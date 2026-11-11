using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Properties;
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;

namespace NhaTro.IntegrationTests.Features.Invoices;

/// <summary>
/// VHT-56 — hàng đợi xác nhận thanh toán của Quản lý/Chủ trọ trên SQL Server thật:
/// chỉ thấy yêu cầu trong phạm vi nhà được giao, Admin/Khách thuê bị chặn,
/// lọc trạng thái đúng và tổng hợp không tính yêu cầu đang chờ là tiền đã thu.
/// </summary>
public sealed class PaymentReviewQueryIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int Year = 2026;
    private const decimal Total = 1_500_000m;

    [SqlServerFact]
    public async Task Chu_tro_va_quan_ly_chi_thay_yeu_cau_trong_pham_vi_duoc_giao()
    {
        var data = await SeedAsync();

        var invoiceA = await CreateInvoiceAsync(
            data.ContractAId,
            data.ChuTroAId,
            new DateOnly(Year, 10, 1),
            new DateOnly(Year, 10, 31));
        var invoiceB = await CreateInvoiceAsync(
            data.ContractBId,
            data.ChuTroBId,
            new DateOnly(Year, 10, 1),
            new DateOnly(Year, 10, 31));

        var paymentA = await SubmitAsync(data, invoiceA, "FT-REVIEW-A");
        var paymentB = await SubmitAsync(data, invoiceB, "FT-REVIEW-B");

        await using var db = fixture.CreateContext();
        var service = new PaymentReviewQueryService(db);

        var ownerA = await service.GetRequestsAsync(new PaymentReviewFilter(), OwnerA(data));
        Assert.Equal(1, ownerA.TotalCount);
        var itemA = Assert.Single(ownerA.Items);
        Assert.Equal(paymentA, itemA.Payment.Id);
        Assert.Equal(invoiceA, itemA.InvoiceId);
        Assert.Equal(TestDataSeeder.RoomACode, itemA.RoomCode);
        Assert.Equal(TestDataSeeder.PropertyAName, itemA.PropertyName);
        Assert.Equal(Total, itemA.InvoiceTotal);
        Assert.Contains("Khách thuê A", itemA.TenantNames);

        var manager = await service.GetRequestsAsync(
            new PaymentReviewFilter(),
            new PropertyCaller(data.QuanLyId, Roles.QuanLy));
        Assert.Equal(1, manager.TotalCount);
        Assert.Equal(paymentA, Assert.Single(manager.Items).Payment.Id);

        var ownerB = await service.GetRequestsAsync(new PaymentReviewFilter(), OwnerB(data));
        Assert.Equal(1, ownerB.TotalCount);
        Assert.Equal(paymentB, Assert.Single(ownerB.Items).Payment.Id);

        var managerDetail = await service.GetRequestAsync(
            paymentA,
            new PropertyCaller(data.QuanLyId, Roles.QuanLy));
        Assert.Equal(TestDataSeeder.PropertyAName, managerDetail.PropertyName);

        // Ngoài phạm vi trả 404 để không lộ sự tồn tại của yêu cầu.
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetRequestAsync(paymentA, OwnerB(data)));
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            service.GetRequestAsync(
                paymentB,
                new PropertyCaller(data.QuanLyId, Roles.QuanLy)));

        // Admin và Khách thuê không được dùng màn hình xác nhận.
        foreach (var caller in new[]
                 {
                     new PropertyCaller(data.AdminId, Roles.Admin),
                     new PropertyCaller(data.KhachThueId, Roles.KhachThue),
                 })
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.GetRequestsAsync(new PaymentReviewFilter(), caller));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.GetRequestAsync(paymentA, caller));
        }
    }

    [SqlServerFact]
    public async Task Loc_trang_thai_va_tong_hop_khong_tinh_yeu_cau_dang_cho_la_da_thu()
    {
        var data = await SeedAsync();

        var paidInvoice = await CreateInvoiceAsync(
            data.ContractAId,
            data.ChuTroAId,
            new DateOnly(Year, 10, 1),
            new DateOnly(Year, 10, 31));
        var reviewedInvoice = await CreateInvoiceAsync(
            data.ContractAId,
            data.ChuTroAId,
            new DateOnly(Year, 11, 1),
            new DateOnly(Year, 11, 30));

        var confirmedPayment = await SubmitAsync(data, paidInvoice, "FT-CONFIRM");
        await ConfirmAsync(data, confirmedPayment);

        var rejectedPayment = await SubmitAsync(data, reviewedInvoice, "FT-REJECT");
        await RejectAsync(data, rejectedPayment, "Ảnh mờ, không đọc được số tiền");

        var pendingPayment = await SubmitAsync(data, reviewedInvoice, "FT-PENDING");

        await using var db = fixture.CreateContext();
        var service = new PaymentReviewQueryService(db);

        var pending = await service.GetRequestsAsync(new PaymentReviewFilter(), OwnerA(data));
        Assert.Equal(1, pending.TotalCount);
        Assert.Equal(pendingPayment, Assert.Single(pending.Items).Payment.Id);
        Assert.Equal(1, pending.Summary.PendingCount);
        Assert.Equal(1, pending.Summary.ConfirmedCount);
        Assert.Equal(1, pending.Summary.RejectedCount);
        Assert.Equal(Total, pending.Summary.PendingAmount);

        var confirmed = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = PaymentStatus.Confirmed },
            OwnerA(data));
        Assert.Equal(1, confirmed.TotalCount);
        Assert.Equal(confirmedPayment, Assert.Single(confirmed.Items).Payment.Id);

        var rejected = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = PaymentStatus.Rejected },
            OwnerA(data));
        Assert.Equal(1, rejected.TotalCount);
        Assert.Equal(rejectedPayment, Assert.Single(rejected.Items).Payment.Id);

        var all = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = null },
            OwnerA(data));
        Assert.Equal(3, all.TotalCount);
        Assert.Equal(3, all.Items.Count);
        Assert.Equal(Total, all.Summary.PendingAmount);

        // Hóa đơn đã xác nhận được tính tiền, hóa đơn còn yêu cầu chờ thì chưa.
        await using var verify = fixture.CreateContext();
        var invoiceQuery = new InvoiceQueryService(verify);
        var paid = await invoiceQuery.GetInvoiceAsync(paidInvoice, OwnerA(data));
        Assert.Equal(InvoiceStatus.Paid, paid.Status);
        Assert.Equal(Total, paid.PaidAmount);

        var reviewed = await invoiceQuery.GetInvoiceAsync(reviewedInvoice, OwnerA(data));
        Assert.Equal(InvoiceStatus.Issued, reviewed.Status);
        Assert.Equal(0m, reviewed.PaidAmount);
    }

    [SqlServerFact]
    public async Task Loc_tu_khoa_ky_va_khoang_so_tien()
    {
        var data = await SeedAsync();

        var octoberInvoice = await CreateInvoiceAsync(
            data.ContractAId,
            data.ChuTroAId,
            new DateOnly(Year, 10, 1),
            new DateOnly(Year, 10, 31));
        var novemberInvoice = await CreateInvoiceAsync(
            data.ContractAId,
            data.ChuTroAId,
            new DateOnly(Year, 11, 1),
            new DateOnly(Year, 11, 30));

        var octoberPayment = await SubmitAsync(data, octoberInvoice, "FT-FILTER-OCT");
        var novemberPayment = await SubmitAsync(data, novemberInvoice, "FT-FILTER-NOV");

        await using var db = fixture.CreateContext();
        var service = new PaymentReviewQueryService(db);

        var byRequest = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = null, Keyword = $"YC-{novemberPayment}" },
            OwnerA(data));
        var byInvoice = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = null, Keyword = $"HD-{octoberInvoice}" },
            OwnerA(data));
        var byRoom = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = null, Keyword = TestDataSeeder.RoomACode },
            OwnerA(data));
        var byTenant = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = null, Keyword = "Khách thuê A" },
            OwnerA(data));
        var byPeriod = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = null, Year = Year, Month = 10 },
            OwnerA(data));
        var byAmount = await service.GetRequestsAsync(
            new PaymentReviewFilter { Status = null, MinAmount = Total, MaxAmount = Total },
            OwnerA(data));

        Assert.Equal([novemberPayment], byRequest.Items.Select(item => item.Payment.Id));
        Assert.Equal([octoberPayment], byInvoice.Items.Select(item => item.Payment.Id));
        Assert.Equal(2, byRoom.TotalCount);
        Assert.Equal(2, byTenant.TotalCount);
        Assert.Equal([octoberPayment], byPeriod.Items.Select(item => item.Payment.Id));
        Assert.Equal(2, byAmount.TotalCount);
    }

    private static PropertyCaller OwnerA(SeedData data) =>
        new(data.ChuTroAId, Roles.ChuTro);

    private static PropertyCaller OwnerB(SeedData data) =>
        new(data.ChuTroBId, Roles.ChuTro);

    private static PaymentService NewPaymentService(ApplicationDbContext db) =>
        new(db, TimeProvider.System);

    private async Task<int> SubmitAsync(SeedData data, int invoiceId, string transactionCode)
    {
        var proofId = await AddProofAsync(data, invoiceId);
        await using var db = fixture.CreateContext();
        var payment = await NewPaymentService(db).SubmitAsync(
            invoiceId,
            new SubmitPaymentRequest
            {
                Amount = Total,
                TransferredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                TransactionCode = transactionCode,
                ProofFileIds = [proofId],
            },
            new PropertyCaller(data.KhachThueId, Roles.KhachThue));

        return payment.Id;
    }

    private async Task ConfirmAsync(SeedData data, int paymentId)
    {
        await using var db = fixture.CreateContext();
        await NewPaymentService(db).ConfirmAsync(paymentId, OwnerA(data));
    }

    private async Task RejectAsync(SeedData data, int paymentId, string reason)
    {
        await using var db = fixture.CreateContext();
        await NewPaymentService(db).RejectAsync(
            paymentId,
            new RejectPaymentRequest { Reason = reason },
            OwnerA(data));
    }

    private async Task<Guid> AddProofAsync(SeedData data, int invoiceId)
    {
        await using var db = fixture.CreateContext();
        var file = new StoredFile
        {
            Id = Guid.NewGuid(),
            FileName = "minh-chung.png",
            StoredFileName = $"{Guid.NewGuid():N}.png",
            RelativePath = $"2026/11/{Guid.NewGuid():N}.png",
            ContentType = "image/png",
            FileSizeBytes = 128,
            FileCategory = FileCategory.PaymentProof,
            UploadedByUserId = data.KhachThueId,
            UploadedAt = DateTimeOffset.UtcNow,
            RelatedEntityId = invoiceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RelatedEntityType = RelatedEntityType.Payment,
        };
        db.StoredFiles.Add(file);
        await db.SaveChangesAsync();
        return file.Id;
    }

    private async Task<SeedData> SeedAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var contractA = NewContract(ids.RoomAId, "HD-P6-REVIEW-A");
        var contractB = NewContract(ids.RoomBId, "HD-P6-REVIEW-B");
        db.Contracts.AddRange(contractA, contractB);
        db.ContractMembers.AddRange(
            new ContractMember { ContractId = contractA.ContractId, TenantId = ids.TenantAId },
            new ContractMember { ContractId = contractB.ContractId, TenantId = ids.TenantAId });
        await db.SaveChangesAsync();

        return new SeedData(
            ids.AdminId,
            ids.ChuTroAId,
            ids.ChuTroBId,
            ids.QuanLyId,
            ids.KhachThueId,
            contractA.ContractId,
            contractB.ContractId);
    }

    private static Contract NewContract(int roomId, string number) =>
        new()
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = number,
            RoomId = roomId,
            StartDate = new DateTime(Year, 1, 1),
            EndDate = new DateTime(Year, 12, 31),
            SnapshotPrice = TestDataSeeder.RoomAStandardPrice,
            DepositAmount = TestDataSeeder.RoomAStandardPrice,
            Status = ContractStatus.Approved,
        };

    private async Task<int> CreateInvoiceAsync(
        Guid contractId,
        string ownerId,
        DateOnly periodStart,
        DateOnly periodEnd)
    {
        await using var db = fixture.CreateContext();
        var invoices = new InvoiceService(db, new InvoiceQueryService(db), TimeProvider.System);
        var issued = await invoices.CreateAndIssueAsync(
            new CreateInvoiceRequest
            {
                ContractId = contractId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Items =
                [
                    new InvoiceItemInputDto
                    {
                        ItemType = InvoiceItemType.Rent,
                        Description = "Tiền thuê",
                        Unit = "tháng",
                        Quantity = 1m,
                        UnitPrice = Total,
                        Amount = Total,
                    },
                ],
            },
            new PropertyCaller(ownerId, Roles.ChuTro));

        return issued.Id;
    }

    private sealed record SeedData(
        string AdminId,
        string ChuTroAId,
        string ChuTroBId,
        string QuanLyId,
        string KhachThueId,
        Guid ContractAId,
        Guid ContractBId);
}
