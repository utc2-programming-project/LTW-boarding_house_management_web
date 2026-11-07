using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Features.PermissionMatrix;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Payments.Services;

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
                (InvoiceId, Amount, TransferredAt, Status, ProofFileIdsJson, RequestedAt, ReviewedAt, RejectionReason, SubmittedByUserId)
            VALUES
                ({invoiceId}, {amount}, SYSDATETIMEOFFSET(), {status}, N'[]', SYSDATETIMEOFFSET(), {reviewedAt}, {reason}, {PermissionMatrixWorld.TenantUserAId})
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

        Assert.Contains("UX_Payments_InvoiceConfirmed", exception.Message, StringComparison.OrdinalIgnoreCase);
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

        var statuses = await db.Set<Web.Features.Payments.Entities.Payment>()
            .AsNoTracking()
            .Where(item => item.InvoiceId == world.Data.InvoiceIssuedId)
            .Select(item => item.Status)
            .ToListAsync();
        Assert.Contains(PaymentStatus.Pending, statuses);
        Assert.Contains(PaymentStatus.Confirmed, statuses);
    }

    [SqlServerFact]
    public async Task Hai_luong_xac_nhan_dong_thoi_chi_mot_lan_thanh_cong()
    {
        await using var world = await NewWorldAsync();
        var invoice = await world.NewInvoiceQuery().GetInvoiceAsync(world.Data.InvoiceIssuedId, world.OwnerA);
        var pending = await world.NewPayments().SubmitAsync(
            world.Data.InvoiceIssuedId,
            new SubmitPaymentRequest
            {
                Amount = invoice.TotalAmount,
                TransferredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                TransactionCode = "VHT48-CONCURRENT",
                ProofFileIds = [Guid.NewGuid()],
            },
            world.TenantAAsPropertyCaller);

        await using var db1 = world.NewDb();
        await using var db2 = world.NewDb();
        var results = await Task.WhenAll(
            TryConfirmAsync(new PaymentService(db1, TimeProvider.System), pending.Id, world.OwnerA),
            TryConfirmAsync(new PaymentService(db2, TimeProvider.System), pending.Id, world.OwnerA));

        Assert.Equal(1, results.Count(result => result is null));
        Assert.Equal(1, results.Count(result => result is DbUpdateConcurrencyException));

        await using var verify = world.NewDb();
        var payment = await verify.Set<Web.Features.Payments.Entities.Payment>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == pending.Id);
        Assert.Equal(PaymentStatus.Confirmed, payment.Status);
        Assert.NotNull(payment.ReviewedAt);

        // Ghi nhận BLOCKED: dev chưa cập nhật Invoice.Paid/audit (thuộc VHT-54 chưa merge).
        var invoiceStatus = await verify.Invoices.AsNoTracking()
            .Where(item => item.Id == world.Data.InvoiceIssuedId)
            .Select(item => item.Status)
            .SingleAsync();
        Assert.Equal(InvoiceStatus.Issued, invoiceStatus);
    }

    private static async Task<Exception?> TryConfirmAsync(
        PaymentService service, int paymentId, Web.Features.Properties.PropertyCaller caller)
    {
        try
        {
            await service.ConfirmAsync(paymentId, caller);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
