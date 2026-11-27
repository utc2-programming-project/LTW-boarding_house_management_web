using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.Entities;

/// <summary>Khách thuê báo đã chuyển tiền cọc; chủ trọ xác nhận hoặc từ chối.</summary>
public sealed class ContractDepositPayment
{
    public int Id { get; set; }

    public Guid ContractId { get; set; }

    public decimal Amount { get; set; }

    public DateTime TransferredAt { get; set; }

    public string? TransactionCode { get; set; }

    public DepositPaymentStatus Status { get; set; } = DepositPaymentStatus.Pending;

    public DateTime RequestedAt { get; set; }

    public string RequestedByUserId { get; set; } = string.Empty;

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedByUserId { get; set; }

    public string? RejectionReason { get; set; }

    /// <summary>JSON danh sách file minh chứng đã upload (RelatedEntityType.ContractDeposit).</summary>
    public string? ProofFileIdsJson { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Contract Contract { get; set; } = null!;
}
