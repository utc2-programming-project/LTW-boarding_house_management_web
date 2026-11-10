using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Features.PermissionMatrix;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;
using System.Globalization;

namespace NhaTro.IntegrationTests.Features.TestReadiness;

/// <summary>
/// VHT-48 3A — chứng minh DB chặn tiền sai và xác nhận kép sau migration AddFinancialConstraints.
/// Dùng SQL trực tiếp cho CHECK/unique (đúng tầng bảo vệ cuối) và service thật cho concurrency.
/// </summary>
public sealed class FinancialSchemaConstraintTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private async Task<PermissionMatrixWorld> NewWorldAsync()
    {
        var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();
        return world;
    }

    private static Task<int> InsertPaymentAsync(
        ApplicationDbContext db,
        int invoiceId,
        decimal amount,
        int status,
        DateTimeOffset? reviewedAt = null,
        string? reason = null) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO Payments
                (InvoiceId, Amount, TransferredAt, Status, RequestedAt, ReviewedAt, RejectionReason, RequestedByUserId)
            VALUES
                ({invoiceId}, {amount}, SYSDATETIMEOFFSET(), {status}, SYSDATETIMEOFFSET(), {reviewedAt}, {reason}, {PermissionMatrixWorld.TenantUserAId})
            """);

    private static async Task<SqlException> AssertSqlRejectedAsync(
        ApplicationDbContext db,
        Func<Task<int>> action,
        string constraintName)
    {
        var exception = await Assert.ThrowsAsync<SqlException>(action);
        Assert.Contains(constraintName, exception.Message, StringComparison.OrdinalIgnoreCase);
        return exception;
    }

    [SqlServerFact]
    public async Task Payment_Amount_khong_duong_bi_check_chan()
    {
        await using var world = await NewWorldAsync();
        await using var db = world.NewDb();

        await AssertSqlRejectedAsync(
            db,
            () => InsertPaymentAsync(db, world.Data.InvoiceIssuedId, 0m, (int)PaymentStatus.Pending),
            "CK_Payments_Amount");
    }

    [SqlServerFact]
    public async Task Payment_Status_ngoai_enum_bi_check_chan()
    {
        await using var world = await NewWorldAsync();
        await using var db = world.NewDb();

        await AssertSqlRejectedAsync(
            db,
            () => InsertPaymentAsync(
                db, world.Data.InvoiceIssuedId, 1_500_000m, 9, reviewedAt: DateTimeOffset.UtcNow),
            "CK_Payments_Status");
    }

    [SqlServerFact]
    public async Task Payment_Pending_khong_duoc_co_ReviewedAt()
    {
        await using var world = await NewWorldAsync();
        await using var db = world.NewDb();

        await AssertSqlRejectedAsync(
            db,
            () => InsertPaymentAsync(
                db, world.Data.InvoiceIssuedId, 1_500_000m, (int)PaymentStatus.Pending,
                reviewedAt: DateTimeOffset.UtcNow),
            "CK_Payments_ReviewedAt");
    }

    [SqlServerFact]
    public async Task Payment_Confirmed_thieu_ReviewedAt_bi_check_chan()
    {
        await using var world = await NewWorldAsync();
        await using var db = world.NewDb();

        await AssertSqlRejectedAsync(
            db,
            () => InsertPaymentAsync(
                db, world.Data.InvoiceIssuedId, 1_500_000m, (int)PaymentStatus.Confirmed),
            "CK_Payments_ReviewedAt");
    }

    [SqlServerFact]
    public async Task Payment_Rejected_thieu_ly_do_bi_check_chan()
    {
        await using var world = await NewWorldAsync();
        await using var db = world.NewDb();

        await AssertSqlRejectedAsync(
            db,
            () => InsertPaymentAsync(
                db, world.Data.InvoiceIssuedId, 1_500_000m, (int)PaymentStatus.Rejected,
                reviewedAt: DateTimeOffset.UtcNow),
            "CK_Payments_RejectionReason");
    }

    [SqlServerFact]
    public async Task Hai_Payment_Confirmed_cung_hoa_don_bi_unique_chan()
    {
        await using var world = await NewWorldAsync();
        await using var db = world.NewDb();
        var reviewedAt = DateTimeOffset.UtcNow;

        await InsertPaymentAsync(
            db, world.Data.InvoiceIssuedId, 1_500_000m, (int)PaymentStatus.Confirmed, reviewedAt);
        var exception = await Assert.ThrowsAsync<SqlException>(() => InsertPaymentAsync(
            db, world.Data.InvoiceIssuedId, 1_500_000m, (int)PaymentStatus.Confirmed, reviewedAt));

        Assert.Contains("UX_Payments_InvoiceStatus", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [SqlServerFact]
    public async Task Mot_Pending_va_mot_Confirmed_cung_hoa_don_hop_le()
    {
        await using var world = await NewWorldAsync();
        await using var db = world.NewDb();
        var reviewedAt = DateTimeOffset.UtcNow;

        await InsertPaymentAsync(
            db, world.Data.InvoiceIssuedId, 1_500_000m, (int)PaymentStatus.Pending);
        await InsertPaymentAsync(
            db, world.Data.InvoiceIssuedId, 1_500_000m, (int)PaymentStatus.Confirmed, reviewedAt);

        var statuses = await db.Set<Web.Features.Invoices.Entities.Payment>()
            .AsNoTracking()
            .Where(item => item.InvoiceId == world.Data.InvoiceIssuedId)
            .Select(item => item.Status)
            .ToListAsync();
        Assert.Contains(PaymentStatus.Pending, statuses);
        Assert.Contains(PaymentStatus.Confirmed, statuses);
    }

    [SqlServerFact]
    public async Task Hai_luong_xac_nhan_dong_thoi_chi_ghi_mot_lan_thu()
    {
        await using var world = await NewWorldAsync();
        var invoice = await world.NewInvoiceQuery().GetInvoiceAsync(world.Data.InvoiceIssuedId, world.OwnerA);
        // Minh chứng thật phải thuộc đúng khách thuê và hóa đơn đang gửi.
        var proofId = Guid.NewGuid();
        await using (var proofDb = world.NewDb())
        {
            proofDb.StoredFiles.Add(new StoredFile
            {
                Id = proofId,
                FileName = "financial-payment-proof.png",
                StoredFileName = $"{proofId:N}.png",
                RelativePath = $"probe/{proofId:N}.png",
                ContentType = "image/png",
                FileSizeBytes = 128,
                FileCategory = FileCategory.PaymentProof,
                UploadedByUserId = world.TenantAAsPropertyCaller.UserId,
                UploadedAt = DateTimeOffset.UtcNow,
                RelatedEntityId = world.Data.InvoiceIssuedId.ToString(CultureInfo.InvariantCulture),
                RelatedEntityType = RelatedEntityType.Payment,
            });
            await proofDb.SaveChangesAsync();
        }
        var pending = await world.NewPayments().SubmitAsync(
            world.Data.InvoiceIssuedId,
            new SubmitPaymentRequest
            {
                Amount = invoice.TotalAmount,
                TransferredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                TransactionCode = "VHT48-CONCURRENT",
                ProofFileIds = [proofId],
            },
            world.TenantAAsPropertyCaller);

        await using var db1 = world.NewDb();
        await using var db2 = world.NewDb();
        var results = await Task.WhenAll(
            new PaymentService(db1, TimeProvider.System).ConfirmAsync(pending.Id, world.OwnerA),
            new PaymentService(db2, TimeProvider.System).ConfirmAsync(pending.Id, world.OwnerA));

        // Xác nhận lặp trả cùng kết quả nhưng chỉ ghi một lần thu và một audit.
        Assert.All(results, result => Assert.Equal(PaymentStatus.Confirmed, result.Status));

        await using var verify = world.NewDb();
        var payment = await verify.Set<Web.Features.Invoices.Entities.Payment>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == pending.Id);
        Assert.Equal(PaymentStatus.Confirmed, payment.Status);
        Assert.NotNull(payment.ReviewedAt);

        Assert.Equal(1, await verify.PaymentAuditLogs.CountAsync(
            log => log.PaymentId == pending.Id && log.Action == PaymentAuditAction.Confirmed));
        var invoiceStatus = await verify.Invoices.AsNoTracking()
            .Where(item => item.Id == world.Data.InvoiceIssuedId)
            .Select(item => item.Status)
            .SingleAsync();
        Assert.Equal(InvoiceStatus.Paid, invoiceStatus);
    }

}
