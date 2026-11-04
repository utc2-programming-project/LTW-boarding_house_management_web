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
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;

namespace NhaTro.IntegrationTests.Features.Invoices;

/// <summary>
/// VHT-54 — yêu cầu thanh toán/xác nhận thu tiền trên SQL Server thật:
/// gửi không cộng tiền, gửi/xác nhận lặp không nhân đôi, hai luồng đồng thời chỉ ghi một lần thu,
/// từ chối không cộng tiền và phân quyền P6.
/// </summary>
public sealed class P6PaymentIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const int Year = 2026;
    private static readonly DateOnly PeriodStart = new(Year, 10, 1);
    private static readonly DateOnly PeriodEnd = new(Year, 10, 31);
    private const decimal Total = 1_500_000m;

    [SqlServerFact]
    public async Task Gui_yeu_cau_khong_cong_tien_va_gui_lap_tra_lai_cung_ban_ghi()
    {
        var data = await SeedIssuedInvoiceAsync();
        var proofId = await AddProofAsync(data, data.InvoiceId);

        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var request = Request(proofId, "FT-001");

        var submitted = await service.SubmitAsync(data.InvoiceId, request, Tenant(data));

        Assert.Equal(PaymentStatus.Pending, submitted.Status);
        Assert.Equal(Total, submitted.Amount);
        Assert.Equal([proofId], submitted.ProofFileIds);

        // Yêu cầu đang chờ chưa phải tiền đã thu và hóa đơn vẫn Issued.
        await using (var verify = fixture.CreateContext())
        {
            var invoice = await new InvoiceQueryService(verify)
                .GetInvoiceAsync(data.InvoiceId, Owner(data));
            Assert.Equal(InvoiceStatus.Issued, invoice.Status);
            Assert.Equal(0m, invoice.PaidAmount);
            Assert.Equal(Total, invoice.OutstandingAmount);
            Assert.Equal(PaymentStatus.Pending, invoice.LatestPayment!.Status);
            Assert.Equal(1, await verify.Payments.CountAsync());
            Assert.Equal(1, await verify.PaymentAuditLogs.CountAsync(log => log.Action == PaymentAuditAction.Submitted));
        }

        // Gửi lặp cùng dữ liệu bằng context mới (mô phỏng mất kết nối rồi thử lại).
        await using (var retryDb = fixture.CreateContext())
        {
            var repeated = await NewService(retryDb).SubmitAsync(data.InvoiceId, request, Tenant(data));
            Assert.Equal(submitted.Id, repeated.Id);
        }

        await using (var queryDb = fixture.CreateContext())
        {
            var queryService = NewService(queryDb);
            var latest = await queryService.GetLatestPaymentAsync(data.InvoiceId, Tenant(data));
            Assert.Equal(submitted.Id, latest!.Id);

            var history = await queryService.GetPaymentsAsync(data.InvoiceId, Tenant(data));
            Assert.Single(history);

            // Người ngoài phạm vi không tra được kết quả của hóa đơn này.
            Assert.Null(await queryService.GetLatestPaymentAsync(
                data.InvoiceId, new PropertyCaller(data.ChuTroBId, Roles.ChuTro)));
            await Assert.ThrowsAsync<PaymentNotFoundException>(() =>
                queryService.GetPaymentAsync(
                    submitted.Id, new PropertyCaller(data.ChuTroBId, Roles.ChuTro)));
        }

        await using var final = fixture.CreateContext();
        Assert.Equal(1, await final.Payments.CountAsync());
        Assert.Equal(1, await final.PaymentProofs.CountAsync());

        // Cùng hóa đơn nhưng nội dung khác khi đang chờ thì không được tạo bản ghi thứ hai.
        await using var conflictDb = fixture.CreateContext();
        var conflict = await Assert.ThrowsAsync<PaymentOperationException>(() =>
            NewService(conflictDb).SubmitAsync(
                data.InvoiceId, Request(proofId, "FT-KHAC"), Tenant(data)));
        Assert.Equal(PaymentErrorCodes.PendingPaymentExists, conflict.ErrorCode);
    }

    [SqlServerFact]
    public async Task Hai_luong_gui_dong_thoi_chi_ghi_mot_yeu_cau()
    {
        var data = await SeedIssuedInvoiceAsync();
        var proofId = await AddProofAsync(data, data.InvoiceId);

        await using var db1 = fixture.CreateContext();
        await using var db2 = fixture.CreateContext();

        // Dùng chung một request để hai luồng thực sự trùng nội dung (thời điểm chuyển tiền khớp).
        var request = Request(proofId, "FT-001");
        var results = await Task.WhenAll(
            NewService(db1).SubmitAsync(data.InvoiceId, request, Tenant(data)),
            NewService(db2).SubmitAsync(data.InvoiceId, request, Tenant(data)));

        Assert.Single(results.Select(result => result.Id).Distinct());

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Payments.CountAsync());
        Assert.Equal(1, await verify.PaymentProofs.CountAsync());
        Assert.Equal(1, await verify.PaymentAuditLogs.CountAsync(log => log.Action == PaymentAuditAction.Submitted));
    }

    [SqlServerFact]
    public async Task Xac_nhan_ghi_tien_hoa_don_va_audit_mot_lan_du_goi_lap()
    {
        var data = await SeedIssuedInvoiceAsync();
        var proofId = await AddProofAsync(data, data.InvoiceId);

        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var submitted = await service.SubmitAsync(data.InvoiceId, Request(proofId, "FT-001"), Tenant(data));

        var confirmed = await service.ConfirmAsync(submitted.Id, Owner(data));
        Assert.Equal(PaymentStatus.Confirmed, confirmed.Status);
        Assert.Equal(data.ChuTroAId, confirmed.ReviewedByUserId);

        var repeated = await service.ConfirmAsync(submitted.Id, Owner(data));
        Assert.Equal(confirmed.Id, repeated.Id);

        await using var verify = fixture.CreateContext();
        var invoice = await new InvoiceQueryService(verify).GetInvoiceAsync(data.InvoiceId, Owner(data));
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(Total, invoice.PaidAmount);
        Assert.Equal(0m, invoice.OutstandingAmount);
        Assert.Equal(PaymentStatus.Confirmed, invoice.LatestPayment!.Status);

        Assert.Equal(1, await verify.Payments.CountAsync());
        Assert.Equal(1, await verify.Payments.CountAsync(payment => payment.Status == PaymentStatus.Confirmed));
        Assert.Equal(1, await verify.PaymentAuditLogs.CountAsync(log => log.Action == PaymentAuditAction.Confirmed));
    }

    [SqlServerFact]
    public async Task Hai_luong_xac_nhan_dong_thoi_chi_ghi_mot_lan_thu()
    {
        var data = await SeedIssuedInvoiceAsync();
        var proofId = await AddProofAsync(data, data.InvoiceId);

        int paymentId;
        await using (var setup = fixture.CreateContext())
        {
            paymentId = (await NewService(setup)
                .SubmitAsync(data.InvoiceId, Request(proofId, "FT-001"), Tenant(data))).Id;
        }

        await using var db1 = fixture.CreateContext();
        await using var db2 = fixture.CreateContext();

        var results = await Task.WhenAll(
            NewService(db1).ConfirmAsync(paymentId, Owner(data)),
            NewService(db2).ConfirmAsync(paymentId, Owner(data)));

        Assert.All(results, result => Assert.Equal(PaymentStatus.Confirmed, result.Status));

        await using var verify = fixture.CreateContext();
        Assert.Equal(1, await verify.Payments.CountAsync(payment => payment.Status == PaymentStatus.Confirmed));
        Assert.Equal(1, await verify.PaymentAuditLogs.CountAsync(log => log.Action == PaymentAuditAction.Confirmed));
        var invoice = await verify.Invoices.SingleAsync(item => item.Id == data.InvoiceId);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    [SqlServerFact]
    public async Task Tu_choi_khong_cong_tien_va_cho_gui_lai_minh_chung_moi()
    {
        var data = await SeedIssuedInvoiceAsync();
        var firstProof = await AddProofAsync(data, data.InvoiceId);

        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var submitted = await service.SubmitAsync(data.InvoiceId, Request(firstProof, "FT-001"), Tenant(data));

        var rejected = await service.RejectAsync(
            submitted.Id,
            new RejectPaymentRequest { Reason = "Ảnh mờ, không đọc được số tiền" },
            Owner(data));

        Assert.Equal(PaymentStatus.Rejected, rejected.Status);
        Assert.Equal("Ảnh mờ, không đọc được số tiền", rejected.RejectionReason);

        await using (var verify = fixture.CreateContext())
        {
            var invoice = await new InvoiceQueryService(verify).GetInvoiceAsync(data.InvoiceId, Owner(data));
            Assert.Equal(InvoiceStatus.Issued, invoice.Status);
            Assert.Equal(0m, invoice.PaidAmount);
            Assert.Equal(PaymentStatus.Rejected, invoice.LatestPayment!.Status);
        }

        // Sau khi bị từ chối, khách được gửi minh chứng mới (một tệp khác).
        var secondProof = await AddProofAsync(data, data.InvoiceId);
        await using var retryDb = fixture.CreateContext();
        var second = await NewService(retryDb)
            .SubmitAsync(data.InvoiceId, Request(secondProof, "FT-002"), Tenant(data));

        Assert.NotEqual(submitted.Id, second.Id);
        Assert.Equal(PaymentStatus.Pending, second.Status);

        await using var final = fixture.CreateContext();
        Assert.Equal(2, await final.Payments.CountAsync());
        Assert.Equal(1, await final.Payments.CountAsync(payment => payment.Status == PaymentStatus.Rejected));
        Assert.Equal(1, await final.Payments.CountAsync(payment => payment.Status == PaymentStatus.Pending));
    }

    [SqlServerFact]
    public async Task Khach_va_admin_khong_xac_nhan_duoc_con_quan_ly_trong_pham_vi_thi_duoc()
    {
        var data = await SeedIssuedInvoiceAsync();
        var proofId = await AddProofAsync(data, data.InvoiceId);

        int paymentId;
        await using (var setup = fixture.CreateContext())
        {
            paymentId = (await NewService(setup)
                .SubmitAsync(data.InvoiceId, Request(proofId, "FT-001"), Tenant(data))).Id;
        }

        // Khách/Admin bị chặn ngay theo vai trò; Chủ trọ ngoài phạm vi nhận 404 để không lộ ID.
        foreach (var caller in new[]
                 {
                     Tenant(data),
                     new PropertyCaller(data.AdminId, Roles.Admin),
                 })
        {
            await using var db = fixture.CreateContext();
            var error = await Assert.ThrowsAsync<PaymentAuthorizationException>(() =>
                NewService(db).ConfirmAsync(paymentId, caller));
            Assert.Equal(PaymentErrorCodes.Forbidden, error.ErrorCode);
        }

        await using (var foreignDb = fixture.CreateContext())
        {
            var error = await Assert.ThrowsAsync<PaymentNotFoundException>(() =>
                NewService(foreignDb)
                    .ConfirmAsync(paymentId, new PropertyCaller(data.ChuTroBId, Roles.ChuTro)));
            Assert.Equal(PaymentErrorCodes.PaymentNotFound, error.ErrorCode);
        }

        await using (var verify = fixture.CreateContext())
        {
            Assert.Equal(InvoiceStatus.Issued,
                (await verify.Invoices.SingleAsync(item => item.Id == data.InvoiceId)).Status);
            Assert.Equal(0m, await verify.Payments
                .Where(payment => payment.Status == PaymentStatus.Confirmed)
                .SumAsync(payment => payment.Amount));
        }

        await using var managerDb = fixture.CreateContext();
        var confirmed = await NewService(managerDb)
            .ConfirmAsync(paymentId, new PropertyCaller(data.QuanLyId, Roles.QuanLy));
        Assert.Equal(PaymentStatus.Confirmed, confirmed.Status);
    }

    [SqlServerFact]
    public async Task Khong_cho_gui_sai_so_tien_hoac_minh_chung_ngoai_pham_vi()
    {
        var data = await SeedIssuedInvoiceAsync();
        var proofId = await AddProofAsync(data, data.InvoiceId);

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var amountError = await Assert.ThrowsAsync<PaymentValidationException>(() =>
            service.SubmitAsync(
                data.InvoiceId,
                new SubmitPaymentRequest
                {
                    Amount = Total - 1m,
                    TransferredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
                    TransactionCode = "FT-001",
                    ProofFileIds = [proofId],
                },
                Tenant(data)));
        Assert.Equal(PaymentErrorCodes.AmountMismatch, amountError.ErrorCode);

        // Chủ trọ B không thuộc hóa đơn nên không được thay khách gửi minh chứng.
        var foreignError = await Assert.ThrowsAsync<PaymentAuthorizationException>(() =>
            service.SubmitAsync(
                data.InvoiceId,
                Request(proofId, "FT-001"),
                new PropertyCaller(data.ChuTroBId, Roles.ChuTro)));
        Assert.Equal(PaymentErrorCodes.Forbidden, foreignError.ErrorCode);

        // Admin chỉ quản trị tài khoản/vai trò, không thao tác nghiệp vụ P6.
        var adminError = await Assert.ThrowsAsync<PaymentAuthorizationException>(() =>
            service.SubmitAsync(
                data.InvoiceId,
                Request(proofId, "FT-001"),
                new PropertyCaller(data.AdminId, Roles.Admin)));
        Assert.Equal(PaymentErrorCodes.Forbidden, adminError.ErrorCode);

        await using var verify = fixture.CreateContext();
        Assert.Equal(0, await verify.Payments.CountAsync());
    }

    private static PaymentService NewService(ApplicationDbContext db) =>
        new(db, TimeProvider.System);

    private static PropertyCaller Owner(SeedData data) =>
        new(data.ChuTroAId, Roles.ChuTro);

    private static PropertyCaller Tenant(SeedData data) =>
        new(data.KhachThueId, Roles.KhachThue);

    private static SubmitPaymentRequest Request(Guid proofId, string transactionCode) =>
        new()
        {
            Amount = Total,
            TransferredAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            TransactionCode = transactionCode,
            ProofFileIds = [proofId],
        };

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
            RelatedEntityId = invoiceId.ToString(),
            RelatedEntityType = RelatedEntityType.Payment,
        };
        db.StoredFiles.Add(file);
        await db.SaveChangesAsync();
        return file.Id;
    }

    private async Task<SeedData> SeedIssuedInvoiceAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-P6-TT-2026",
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

        var invoices = new InvoiceService(db, new InvoiceQueryService(db), TimeProvider.System);
        var issued = await invoices.CreateAndIssueAsync(
            new CreateInvoiceRequest
            {
                ContractId = contract.ContractId,
                PeriodStart = PeriodStart,
                PeriodEnd = PeriodEnd,
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
            new PropertyCaller(ids.ChuTroAId, Roles.ChuTro));

        return new SeedData(
            ids.AdminId,
            ids.ChuTroAId,
            ids.ChuTroBId,
            ids.QuanLyId,
            ids.KhachThueId,
            issued.Id);
    }

    private sealed record SeedData(
        string AdminId,
        string ChuTroAId,
        string ChuTroBId,
        string QuanLyId,
        string KhachThueId,
        int InvoiceId);
}
