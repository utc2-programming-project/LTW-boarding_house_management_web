using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.Entities;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Mapping;
using Web.Features.Properties;
using Web.Features.Storage.Enums;
using Web.Infrastructure.Time;

namespace Web.Features.Invoices.Services;

/// <summary>
/// Nghiệp vụ P6: khách gửi yêu cầu thanh toán, Quản lý/Chủ trọ xác nhận hoặc từ chối.
/// <para>
/// Chỉ xác nhận mới ghi tiền (Payment Confirmed + Invoice Paid + audit) và luôn nằm trong một
/// transaction. Gửi/xác nhận lặp trả lại kết quả đã có; unique index lọc và rowversion là chốt
/// chặn cuối khi hai luồng chạy đồng thời.
/// </para>
/// </summary>
public sealed class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly TimeProvider _timeProvider;
    private readonly Web.Features.Notifications.INotificationService? _notifications;

    public PaymentService(
        ApplicationDbContext context,
        TimeProvider? timeProvider = null,
        Web.Features.Notifications.INotificationService? notifications = null)
    {
        _context = context;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _notifications = notifications;
    }

    public async Task<PaymentDto> SubmitAsync(
        int invoiceId,
        SubmitPaymentRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        // Khách thuê gửi yêu cầu; Admin/Chủ trọ/Quản lý không được thay khách gửi minh chứng.
        if (!caller.IsKhachThue)
        {
            throw new PaymentAuthorizationException(
                PaymentErrorMessages.Forbidden,
                PaymentErrorCodes.Forbidden);
        }

        var proofIds = NormalizeProofIds(request.ProofFileIds);
        if (proofIds.Length == 0)
        {
            throw new PaymentValidationException(
                PaymentErrorMessages.ProofRequired,
                PaymentErrorCodes.ProofRequired,
                nameof(request.ProofFileIds));
        }

        if (request.Amount <= 0m)
        {
            throw new PaymentValidationException(
                PaymentErrorMessages.AmountMismatch,
                PaymentErrorCodes.AmountMismatch,
                nameof(request.Amount));
        }

        var transactionCode = NormalizeTransactionCode(request.TransactionCode);
        if (transactionCode is { Length: > 100 })
        {
            throw new PaymentValidationException(
                PaymentErrorMessages.InvalidTransactionCode,
                PaymentErrorCodes.InvalidTransactionCode,
                nameof(request.TransactionCode));
        }

        var now = _timeProvider.GetUtcNow();
        var transferredAt = request.TransferredAt.UtcDateTime;
        if (transferredAt > now.UtcDateTime)
        {
            throw new PaymentValidationException(
                PaymentErrorMessages.TransferredAtInFuture,
                PaymentErrorCodes.TransferredAtInFuture,
                nameof(request.TransferredAt));
        }

        var invoice = await ScopeInvoices(caller)
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);

        // Không phân biệt không tồn tại với ngoài phạm vi để không lộ ID hóa đơn của người khác.
        if (invoice is null)
        {
            throw new PaymentNotFoundException(
                PaymentErrorMessages.InvoiceNotFound,
                PaymentErrorCodes.InvoiceNotFound);
        }

        if (invoice.Status == InvoiceStatus.Draft)
        {
            throw new PaymentOperationException(
                PaymentErrorMessages.InvoiceNotIssued,
                PaymentErrorCodes.InvoiceNotIssued);
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            throw new PaymentOperationException(
                PaymentErrorMessages.InvoiceAlreadyPaid,
                PaymentErrorCodes.InvoiceAlreadyPaid);
        }

        // D13: không trả một phần; số tiền phải bằng đúng tổng hóa đơn.
        if (invoice.TotalAmount != request.Amount)
        {
            throw new PaymentValidationException(
                PaymentErrorMessages.AmountMismatch,
                PaymentErrorCodes.AmountMismatch,
                nameof(request.Amount));
        }

        var pending = await FindPendingPaymentAsync(invoiceId, cancellationToken);

        // Gửi lặp cùng nội dung trả lại yêu cầu đang chờ; khác nội dung bị chặn để không tạo bản ghi thứ hai.
        if (pending is not null)
        {
            if (Matches(pending, request.Amount, transferredAt, transactionCode, proofIds))
            {
                return PaymentMapping.ToDto(pending);
            }

            throw new PaymentOperationException(
                PaymentErrorMessages.PendingPaymentExists,
                PaymentErrorCodes.PendingPaymentExists);
        }

        if (await HasConfirmedPaymentAsync(invoiceId, cancellationToken))
        {
            throw new PaymentOperationException(
                PaymentErrorMessages.InvoiceAlreadyPaid,
                PaymentErrorCodes.InvoiceAlreadyPaid);
        }

        var alreadyLinked = await EnsureProofsBelongToInvoiceAsync(
            invoiceId, proofIds, caller.UserId, cancellationToken);

        // Hai lần gửi đồng thời: luồng kia vừa liên kết minh chứng và ghi yêu cầu trước khi ta kịp đọc.
        if (alreadyLinked)
        {
            var linked = await FindPendingPaymentAsync(invoiceId, cancellationToken);
            if (linked is not null
                && Matches(linked, request.Amount, transferredAt, transactionCode, proofIds))
            {
                return PaymentMapping.ToDto(linked);
            }

            throw InvalidProofException();
        }

        var payment = new Payment
        {
            InvoiceId = invoiceId,
            Amount = request.Amount,
            TransferredAt = transferredAt,
            TransactionCode = transactionCode,
            Status = PaymentStatus.Pending,
            RequestedAt = now.UtcDateTime,
            RequestedByUserId = caller.UserId,
        };

        foreach (var proofId in proofIds)
        {
            payment.Proofs.Add(new PaymentProof { FileId = proofId });
        }

        _context.Payments.Add(payment);
        _context.PaymentAuditLogs.Add(new PaymentAuditLog
        {
            Payment = payment,
            InvoiceId = invoiceId,
            Action = PaymentAuditAction.Submitted,
            FromStatus = null,
            ToStatus = PaymentStatus.Pending,
            Amount = payment.Amount,
            ActorUserId = caller.UserId,
            ActorRole = caller.Role,
            OccurredAt = now,
        });

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            // Hai lần gửi đồng thời: bản còn lại đã ghi trước, trả kết quả nếu cùng nội dung.
            var duplicate = await FindPendingPaymentAsync(invoiceId, cancellationToken);

            if (duplicate is not null
                && Matches(duplicate, request.Amount, transferredAt, transactionCode, proofIds))
            {
                return PaymentMapping.ToDto(duplicate);
            }

            if (await HasConfirmedPaymentAsync(invoiceId, cancellationToken))
            {
                throw new PaymentOperationException(
                    PaymentErrorMessages.InvoiceAlreadyPaid,
                    PaymentErrorCodes.InvoiceAlreadyPaid,
                    exception);
            }

            throw new PaymentOperationException(
                PaymentErrorMessages.PendingPaymentExists,
                PaymentErrorCodes.PendingPaymentExists,
                exception);
        }

        var dto = PaymentMapping.ToDto(payment, proofIds);
        var ownerId = await ResolveOwnerUserIdAsync(invoiceId, cancellationToken);
        if (_notifications is not null && !string.IsNullOrWhiteSpace(ownerId))
        {
            await _notifications.NotifyAsync(
                ownerId,
                "Khách vừa gửi minh chứng thanh toán",
                $"Hóa đơn HD-{invoiceId}: khách thuê đã gửi minh chứng {payment.Amount:0} ₫, chờ xác nhận.",
                $"/payment-requests/{payment.Id}");
        }

        return dto;
    }

    public async Task<PaymentDto> ConfirmAsync(
        int paymentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        EnsureManager(caller);

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var payment = await ScopePayments(caller)
            .Include(item => item.Proofs)
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            throw new PaymentNotFoundException(
                PaymentErrorMessages.PaymentNotFound,
                PaymentErrorCodes.PaymentNotFound);
        }

        // Gọi lặp sau khi đã xác nhận trả lại kết quả, không ghi thêm tiền/lần nữa.
        if (payment.Status == PaymentStatus.Confirmed)
        {
            return PaymentMapping.ToDto(payment);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            throw new PaymentOperationException(
                PaymentErrorMessages.InvalidPaymentStatus,
                PaymentErrorCodes.InvalidPaymentStatus);
        }

        var invoice = await _context.Invoices
            .SingleAsync(item => item.Id == payment.InvoiceId, cancellationToken);

        if (invoice.Status == InvoiceStatus.Paid ||
            (invoice.Status == InvoiceStatus.Issued &&
             await HasConfirmedPaymentAsync(payment.InvoiceId, cancellationToken)))
        {
            // Luồng khác có thể đã xác nhận sau lần đọc Payment Pending ở trên.
            // Chỉ trả kết quả lặp của chính yêu cầu này, không lấy khoản thu khác.
            var current = await _context.Payments
                .AsNoTracking()
                .Include(item => item.Proofs)
                .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);
            if (current?.Status == PaymentStatus.Confirmed)
            {
                return PaymentMapping.ToDto(current);
            }

            throw new PaymentOperationException(
                PaymentErrorMessages.InvoiceAlreadyPaid,
                PaymentErrorCodes.InvoiceAlreadyPaid);
        }

        if (invoice.Status != InvoiceStatus.Issued)
        {
            throw new PaymentOperationException(
                PaymentErrorMessages.InvoiceNotIssued,
                PaymentErrorCodes.InvoiceNotIssued);
        }

        var now = _timeProvider.GetUtcNow();
        payment.Status = PaymentStatus.Confirmed;
        payment.ReviewedAt = now.UtcDateTime;
        payment.ReviewedByUserId = caller.UserId;

        invoice.Status = InvoiceStatus.Paid;
        invoice.UpdatedAt = now.UtcDateTime;

        _context.PaymentAuditLogs.Add(new PaymentAuditLog
        {
            PaymentId = payment.Id,
            InvoiceId = payment.InvoiceId,
            Action = PaymentAuditAction.Confirmed,
            FromStatus = PaymentStatus.Pending,
            ToStatus = PaymentStatus.Confirmed,
            Amount = payment.Amount,
            ActorUserId = caller.UserId,
            ActorRole = caller.Role,
            OccurredAt = now,
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            // Luồng khác đã xác nhận trước: trả kết quả đã ghi thay vì tạo thêm một lần thu tiền.
            var confirmed = await FindConfirmedPaymentAsync(payment.InvoiceId, cancellationToken);
            if (confirmed?.Id == paymentId)
            {
                return PaymentMapping.ToDto(confirmed);
            }

            var current = await _context.Payments
                .AsNoTracking()
                .Include(item => item.Proofs)
                .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

            if (current?.Status == PaymentStatus.Confirmed)
            {
                return PaymentMapping.ToDto(current);
            }

            throw new PaymentOperationException(
                PaymentErrorMessages.ConcurrencyConflict,
                PaymentErrorCodes.ConcurrencyConflict,
                exception);
        }

        var confirmedDto = PaymentMapping.ToDto(payment);
        var confirmTenantId = await ResolveTenantUserIdAsync(payment.InvoiceId, cancellationToken);
        if (_notifications is not null && !string.IsNullOrWhiteSpace(confirmTenantId))
        {
            await _notifications.NotifyAsync(
                confirmTenantId,
                "Thanh toán đã được xác nhận",
                $"Hóa đơn HD-{payment.InvoiceId} đã thanh toán thành công {payment.Amount:0} ₫.",
                $"/my/invoices/{payment.InvoiceId}");
        }

        return confirmedDto;
    }

    public async Task<PaymentDto> RejectAsync(
        int paymentId,
        RejectPaymentRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);
        EnsureManager(caller);

        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw new PaymentValidationException(
                PaymentErrorMessages.RejectionReasonRequired,
                PaymentErrorCodes.RejectionReasonRequired,
                nameof(request.Reason));
        }

        if (reason.Length > 500)
        {
            throw new PaymentValidationException(
                PaymentErrorMessages.RejectionReasonTooLong,
                PaymentErrorCodes.RejectionReasonTooLong,
                nameof(request.Reason));
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var payment = await ScopePayments(caller)
            .Include(item => item.Proofs)
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            throw new PaymentNotFoundException(
                PaymentErrorMessages.PaymentNotFound,
                PaymentErrorCodes.PaymentNotFound);
        }

        // Gọi lặp cùng lý do sau khi đã từ chối trả lại kết quả.
        if (payment.Status == PaymentStatus.Rejected
            && string.Equals(payment.RejectionReason, reason, StringComparison.Ordinal))
        {
            return PaymentMapping.ToDto(payment);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            throw new PaymentOperationException(
                PaymentErrorMessages.InvalidPaymentStatus,
                PaymentErrorCodes.InvalidPaymentStatus);
        }

        var now = _timeProvider.GetUtcNow();
        payment.Status = PaymentStatus.Rejected;
        payment.ReviewedAt = now.UtcDateTime;
        payment.ReviewedByUserId = caller.UserId;
        payment.RejectionReason = reason;

        _context.PaymentAuditLogs.Add(new PaymentAuditLog
        {
            PaymentId = payment.Id,
            InvoiceId = payment.InvoiceId,
            Action = PaymentAuditAction.Rejected,
            FromStatus = PaymentStatus.Pending,
            ToStatus = PaymentStatus.Rejected,
            Amount = payment.Amount,
            ActorUserId = caller.UserId,
            ActorRole = caller.Role,
            OccurredAt = now,
            Note = reason,
        });

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateConcurrencyException or DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _context.ChangeTracker.Clear();

            var current = await _context.Payments
                .AsNoTracking()
                .Include(item => item.Proofs)
                .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

            if (current?.Status == PaymentStatus.Rejected
                && string.Equals(current.RejectionReason, reason, StringComparison.Ordinal))
            {
                return PaymentMapping.ToDto(current);
            }

            throw new PaymentOperationException(
                PaymentErrorMessages.ConcurrencyConflict,
                PaymentErrorCodes.ConcurrencyConflict,
                exception);
        }

        var rejectedDto = PaymentMapping.ToDto(payment);
        var rejectTenantId = await ResolveTenantUserIdAsync(payment.InvoiceId, cancellationToken);
        if (_notifications is not null && !string.IsNullOrWhiteSpace(rejectTenantId))
        {
            await _notifications.NotifyAsync(
                rejectTenantId,
                "Minh chứng thanh toán bị từ chối",
                $"Hóa đơn HD-{payment.InvoiceId}: {reason}",
                $"/my/invoices/{payment.InvoiceId}");
        }

        return rejectedDto;
    }

    public async Task<PaymentDto?> GetLatestPaymentAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var payment = await ScopePayments(caller)
            .AsNoTracking()
            .Include(item => item.Proofs)
            .Where(item => item.InvoiceId == invoiceId)
            .OrderByDescending(item => item.RequestedAt)
            .ThenByDescending(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return payment is null ? null : PaymentMapping.ToDto(payment);
    }

    public async Task<IReadOnlyList<PaymentDto>> GetPaymentsAsync(
        int invoiceId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var payments = await ScopePayments(caller)
            .AsNoTracking()
            .Include(item => item.Proofs)
            .Where(item => item.InvoiceId == invoiceId)
            .OrderByDescending(item => item.RequestedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return payments.Select(payment => PaymentMapping.ToDto(payment)).ToArray();
    }

    public async Task<PaymentDto> GetPaymentAsync(
        int paymentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var payment = await ScopePayments(caller)
            .AsNoTracking()
            .Include(item => item.Proofs)
            .SingleOrDefaultAsync(item => item.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            throw new PaymentNotFoundException(
                PaymentErrorMessages.PaymentNotFound,
                PaymentErrorCodes.PaymentNotFound);
        }

        return PaymentMapping.ToDto(payment);
    }

    private IQueryable<Payment> ScopePayments(PropertyCaller caller)
    {
        var invoiceIds = ScopeInvoices(caller).Select(invoice => invoice.Id);
        return _context.Payments.Where(payment => invoiceIds.Contains(payment.InvoiceId));
    }

    /// <summary>
    /// Hóa đơn người gọi được thao tác: Chủ trọ theo nhà sở hữu, Quản lý theo nhà được phân công,
    /// Khách thuê theo hợp đồng có tên mình. Admin không có quyền nghiệp vụ P6.
    /// </summary>
    private IQueryable<Invoice> ScopeInvoices(PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return from invoice in _context.Invoices
                   join contract in _context.Contracts on invoice.ContractId equals contract.ContractId
                   join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   join property in _context.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                   where property.OwnerId == caller.UserId
                   select invoice;
        }

        if (caller.IsQuanLy)
        {
            var propertyIds = _context.PropertyManagers
                .Where(assignment => assignment.ManagerUserId == caller.UserId && assignment.RevokedAt == null)
                .Select(assignment => assignment.PropertyId);

            return from invoice in _context.Invoices
                   join contract in _context.Contracts on invoice.ContractId equals contract.ContractId
                   join room in _context.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   where propertyIds.Contains(room.PropertyId)
                   select invoice;
        }

        if (caller.IsKhachThue)
        {
            var contractIds = from member in _context.ContractMembers
                              join tenant in _context.Tenants.IgnoreQueryFilters() on member.TenantId equals tenant.Id
                              where tenant.UserId == caller.UserId
                              select member.ContractId;

            return _context.Invoices.Where(invoice => contractIds.Contains(invoice.ContractId));
        }

        throw new PaymentAuthorizationException(
            PaymentErrorMessages.Forbidden,
            PaymentErrorCodes.Forbidden);
    }

    private static void EnsureManager(PropertyCaller caller)
    {
        if (!caller.IsManager)
        {
            throw new PaymentAuthorizationException(
                PaymentErrorMessages.Forbidden,
                PaymentErrorCodes.Forbidden);
        }
    }

    private async Task<string?> ResolveOwnerUserIdAsync(int invoiceId, CancellationToken cancellationToken) =>
        await _context.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Id == invoiceId)
            .Join(
                _context.Contracts,
                invoice => invoice.ContractId,
                contract => contract.ContractId,
                (invoice, contract) => contract.RoomId)
            .Join(
                _context.Rooms.IgnoreQueryFilters(),
                roomId => roomId,
                room => room.Id,
                (_, room) => room.PropertyId)
            .Join(
                _context.Properties.IgnoreQueryFilters(),
                propertyId => propertyId,
                property => property.Id,
                (_, property) => property.OwnerId)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<string?> ResolveTenantUserIdAsync(int invoiceId, CancellationToken cancellationToken) =>
        await _context.Invoices
            .AsNoTracking()
            .Where(invoice => invoice.Id == invoiceId)
            .Join(
                _context.ContractMembers,
                invoice => invoice.ContractId,
                member => member.ContractId,
                (invoice, member) => member.TenantId)
            .Join(
                _context.Tenants.IgnoreQueryFilters(),
                tenantId => tenantId,
                tenant => tenant.Id,
                (_, tenant) => tenant.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<bool> HasConfirmedPaymentAsync(int invoiceId, CancellationToken cancellationToken) =>
        _context.Payments.AnyAsync(
            payment => payment.InvoiceId == invoiceId && payment.Status == PaymentStatus.Confirmed,
            cancellationToken);

    private Task<Payment?> FindConfirmedPaymentAsync(int invoiceId, CancellationToken cancellationToken) =>
        _context.Payments
            .AsNoTracking()
            .Include(payment => payment.Proofs)
            .Where(payment => payment.InvoiceId == invoiceId && payment.Status == PaymentStatus.Confirmed)
            .OrderByDescending(payment => payment.ReviewedAt)
            .ThenByDescending(payment => payment.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static Guid[] NormalizeProofIds(IReadOnlyList<Guid>? proofFileIds) =>
        proofFileIds is null
            ? []
            : proofFileIds.Where(id => id != Guid.Empty).Distinct().ToArray();

    private static string? NormalizeTransactionCode(string? transactionCode) =>
        string.IsNullOrWhiteSpace(transactionCode) ? null : transactionCode.Trim();

    private static bool Matches(
        Payment payment,
        decimal amount,
        DateTime transferredAt,
        string? transactionCode,
        IReadOnlyList<Guid> proofIds)
    {
        if (payment.Amount != amount
            || payment.TransferredAt != transferredAt
            || !string.Equals(payment.TransactionCode, transactionCode, StringComparison.Ordinal))
        {
            return false;
        }

        var existingProofIds = payment.Proofs.Select(proof => proof.FileId).ToHashSet();
        return existingProofIds.Count == proofIds.Count
            && proofIds.All(existingProofIds.Contains);
    }

    /// <summary>
    /// Minh chứng phải là tệp PaymentProof do chính khách gửi, gắn với hóa đơn (RelatedEntityId = invoiceId),
    /// chưa bị xóa và chưa thuộc lần gửi thanh toán khác.
    /// </summary>
    private async Task<bool> EnsureProofsBelongToInvoiceAsync(
        int invoiceId,
        IReadOnlyList<Guid> proofIds,
        string userId,
        CancellationToken cancellationToken)
    {
        var invoiceKey = invoiceId.ToString(CultureInfo.InvariantCulture);
        var files = await _context.StoredFiles
            .AsNoTracking()
            .Where(file => proofIds.Contains(file.Id))
            .Select(file => new
            {
                file.Id,
                file.FileCategory,
                file.RelatedEntityType,
                file.RelatedEntityId,
                file.UploadedByUserId,
                file.IsDeleted,
            })
            .ToListAsync(cancellationToken);

        if (files.Count != proofIds.Count)
        {
            throw InvalidProofException();
        }

        foreach (var file in files)
        {
            if (file.FileCategory != FileCategory.PaymentProof
                || file.RelatedEntityType != RelatedEntityType.Payment
                || file.RelatedEntityId != invoiceKey
                || file.UploadedByUserId != userId
                || file.IsDeleted)
            {
                throw InvalidProofException();
            }
        }

        // Trả về true khi tệp đã thuộc một lần gửi khác; caller quyết định idempotent hay báo lỗi
        // để tránh đua giữa hai lần gửi đồng thời.
        return await _context.PaymentProofs
            .AsNoTracking()
            .AnyAsync(proof => proofIds.Contains(proof.FileId), cancellationToken);
    }

    private Task<Payment?> FindPendingPaymentAsync(
        int invoiceId,
        CancellationToken cancellationToken) =>
        _context.Payments
            .AsNoTracking()
            .Include(payment => payment.Proofs)
            .SingleOrDefaultAsync(
                payment => payment.InvoiceId == invoiceId && payment.Status == PaymentStatus.Pending,
                cancellationToken);

    private static PaymentValidationException InvalidProofException() =>
        new(
            PaymentErrorMessages.InvalidProof,
            PaymentErrorCodes.InvalidProof,
            "ProofFileIds");
}
