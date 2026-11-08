using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Entities;

namespace Web.Features.Invoices.Mapping;

/// <summary>Chuyển entity thanh toán sang DTO; chỉ cộng tiền ở trạng thái Confirmed.</summary>
public static class PaymentMapping
{
    public static PaymentDto ToDto(Payment payment, IReadOnlyList<Guid>? proofFileIds = null) =>
        new()
        {
            Id = payment.Id,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount,
            TransferredAt = ToUtcOffset(payment.TransferredAt),
            TransactionCode = payment.TransactionCode,
            Status = payment.Status,
            ProofFileIds = proofFileIds
                ?? payment.Proofs.Select(proof => proof.FileId).ToArray(),
            RequestedAt = ToUtcOffset(payment.RequestedAt),
            RequestedByUserId = payment.RequestedByUserId,
            ReviewedAt = payment.ReviewedAt is { } reviewedAt ? ToUtcOffset(reviewedAt) : null,
            ReviewedByUserId = payment.ReviewedByUserId,
            RejectionReason = payment.RejectionReason,
        };

    public static DateTimeOffset ToUtcOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
