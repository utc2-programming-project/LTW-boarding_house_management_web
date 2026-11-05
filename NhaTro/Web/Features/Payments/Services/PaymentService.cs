using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Payments.Entities;
using Web.Features.Properties;
using Web.Infrastructure.Time;

namespace Web.Features.Payments.Services;

public static class PaymentErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string InvoiceNotFound = nameof(InvoiceNotFound);
    public const string PaymentNotFound = nameof(PaymentNotFound);
    public const string InvalidStatus = nameof(InvalidStatus);
    public const string AmountMismatch = nameof(AmountMismatch);
    public const string ProofRequired = nameof(ProofRequired);
    public const string InvalidTransferredAt = nameof(InvalidTransferredAt);
    public const string AlreadyReviewed = nameof(AlreadyReviewed);
    public const string RejectionReasonRequired = nameof(RejectionReasonRequired);
}

public sealed class PaymentAuthorizationException(string message, string errorCode)
    : UnauthorizedAccessException(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class PaymentException(string message, string errorCode)
    : InvalidOperationException(message)
{
    public string ErrorCode { get; } = errorCode;
}

public sealed class PaymentNotFoundException(string message, string errorCode)
    : KeyNotFoundException(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>
/// P6 — OP37 khách thuê gửi minh chứng thanh toán; OP38 Chủ trọ/Quản lý xác nhận.
/// Admin và Khách thuê không được xác nhận; phạm vi Chủ trọ/Quản lý lấy theo tòa nhà của hóa đơn.
/// </summary>
public sealed class PaymentService(ApplicationDbContext db, TimeProvider clock)
{
    public async Task<PaymentDto> SubmitAsync(
        int invoiceId,
        SubmitPaymentRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsKhachThue)
        {
            throw Forbidden("Chỉ Khách thuê được gửi yêu cầu thanh toán.");
        }

        var invoice = await db.Invoices.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            throw new PaymentNotFoundException(
                "Không tìm thấy hóa đơn.",
                PaymentErrorCodes.InvoiceNotFound);
        }

        var ownsInvoice = await (
            from contract in db.Contracts.AsNoTracking()
            join member in db.ContractMembers.AsNoTracking()
                on contract.ContractId equals member.ContractId
            join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking()
                on member.TenantId equals tenant.Id
            where contract.ContractId == invoice.ContractId
                && tenant.UserId == caller.UserId
                && contract.Status == ContractStatus.Approved
            select contract.ContractId)
            .AnyAsync(cancellationToken);

        if (!ownsInvoice)
        {
            throw Forbidden("Hóa đơn không thuộc hợp đồng của khách thuê.");
        }

        if (invoice.Status != InvoiceStatus.Issued)
        {
            throw new PaymentException(
                "Chỉ hóa đơn đã phát hành mới được gửi thanh toán.",
                PaymentErrorCodes.InvalidStatus);
        }

        if (request.Amount != invoice.TotalAmount)
        {
            throw new PaymentException(
                "Số tiền phải bằng đúng tổng hóa đơn; không hỗ trợ thanh toán một phần.",
                PaymentErrorCodes.AmountMismatch);
        }

        if (request.ProofFileIds is null || request.ProofFileIds.Count == 0)
        {
            throw new PaymentException(
                "Phải gửi ít nhất một tệp minh chứng.",
                PaymentErrorCodes.ProofRequired);
        }

        if (request.TransferredAt > clock.UtcNow())
        {
            throw new PaymentException(
                "Thời điểm chuyển tiền không được ở tương lai.",
                PaymentErrorCodes.InvalidTransferredAt);
        }

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Amount = request.Amount,
            TransferredAt = request.TransferredAt,
            TransactionCode = request.TransactionCode?.Trim(),
            Status = PaymentStatus.Pending,
            ProofFileIdsJson = JsonSerializer.Serialize(request.ProofFileIds),
            RequestedAt = clock.UtcNow(),
            SubmittedByUserId = caller.UserId,
        };

        db.Set<Payment>().Add(payment);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(payment);
    }

    public async Task<PaymentDto> ConfirmAsync(
        int paymentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw Forbidden("Chỉ Chủ trọ hoặc Quản lý được xác nhận thanh toán.");
        }

        var payment = await LoadInScopeAsync(paymentId, caller, cancellationToken);
        if (payment.Status != PaymentStatus.Pending)
        {
            throw new PaymentException(
                "Yêu cầu thanh toán đã được xử lý.",
                PaymentErrorCodes.AlreadyReviewed);
        }

        payment.Status = PaymentStatus.Confirmed;
        payment.ReviewedAt = clock.UtcNow();
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(payment);
    }

    public async Task<PaymentDto> RejectAsync(
        int paymentId,
        string reason,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw Forbidden("Chỉ Chủ trọ hoặc Quản lý được từ chối thanh toán.");
        }

        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            throw new PaymentException(
                "Phải nhập lý do từ chối.",
                PaymentErrorCodes.RejectionReasonRequired);
        }

        var payment = await LoadInScopeAsync(paymentId, caller, cancellationToken);
        if (payment.Status != PaymentStatus.Pending)
        {
            throw new PaymentException(
                "Yêu cầu thanh toán đã được xử lý.",
                PaymentErrorCodes.AlreadyReviewed);
        }

        payment.Status = PaymentStatus.Rejected;
        payment.ReviewedAt = clock.UtcNow();
        payment.RejectionReason = normalizedReason;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(payment);
    }

    private async Task<Payment> LoadInScopeAsync(
        int paymentId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var payment = await db.Set<Payment>()
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            throw new PaymentNotFoundException(
                "Không tìm thấy yêu cầu thanh toán.",
                PaymentErrorCodes.PaymentNotFound);
        }

        var propertyId = await (
            from invoice in db.Invoices.AsNoTracking()
            join contract in db.Contracts.AsNoTracking()
                on invoice.ContractId equals contract.ContractId
            join room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
                on contract.RoomId equals room.Id
            where invoice.Id == payment.InvoiceId
            select (int?)room.PropertyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (propertyId is null)
        {
            throw new PaymentNotFoundException(
                "Không xác định được tòa nhà của hóa đơn.",
                PaymentErrorCodes.InvoiceNotFound);
        }

        var inScope = caller.IsChuTro
            ? await db.Properties.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(property => property.Id == propertyId && property.OwnerId == caller.UserId, cancellationToken)
            : await db.PropertyManagers.AsNoTracking()
                .AnyAsync(assignment => assignment.PropertyId == propertyId
                    && assignment.ManagerUserId == caller.UserId
                    && assignment.RevokedAt == null, cancellationToken);

        if (!inScope)
        {
            throw new PaymentAuthorizationException(
                "Yêu cầu thanh toán nằm ngoài phạm vi được phép.",
                PaymentErrorCodes.Forbidden);
        }

        return payment;
    }

    private static PaymentAuthorizationException Forbidden(string message) =>
        new(message, PaymentErrorCodes.Forbidden);

    private static PaymentDto ToDto(Payment payment)
    {
        return new PaymentDto
        {
            Id = payment.Id,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount,
            TransferredAt = payment.TransferredAt,
            TransactionCode = payment.TransactionCode,
            Status = payment.Status,
            ProofFileIds = string.IsNullOrWhiteSpace(payment.ProofFileIdsJson)
                ? []
                : JsonSerializer.Deserialize<List<Guid>>(payment.ProofFileIdsJson) ?? [],
            RequestedAt = payment.RequestedAt,
            ReviewedAt = payment.ReviewedAt,
            RejectionReason = payment.RejectionReason,
        };
    }
}
