using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.DTOs;

public sealed record ContractDepositPaymentDto(
    int Id,
    Guid ContractId,
    decimal Amount,
    DateTime TransferredAt,
    string? TransactionCode,
    DepositPaymentStatus Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    string? RejectionReason,
    IReadOnlyList<Guid>? ProofFileIds = null);
