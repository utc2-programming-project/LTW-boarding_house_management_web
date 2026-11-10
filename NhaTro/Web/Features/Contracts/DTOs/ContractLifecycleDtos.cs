using System.ComponentModel.DataAnnotations;

namespace Web.Features.Contracts.DTOs;

/// <summary>Yêu cầu gia hạn hợp đồng: ngày kết thúc mới phải sau ngày kết thúc hiện tại.</summary>
public sealed class RenewContractRequestDto
{
    [Required]
    public DateTime EndDate { get; set; }
}

/// <summary>Kết quả thao tác gia hạn / thanh lý hợp đồng (VHT-9: Applied hoặc PendingApproval).</summary>
public enum ContractActionOutcome
{
    Applied = 0,
    PendingApproval = 1,
}

public sealed record ContractActionResultDto(
    ContractActionOutcome Outcome,
    Guid? ContractId,
    Guid? ApprovalRequestId);

/// <summary>Payload SW-07 lưu trong hàng đợi duyệt để applier áp dụng lại.</summary>
public sealed record ContractApprovalPayload(
    string Action,
    DateTime? EndDate,
    decimal? DepositAmount = null);

public static class ContractApprovalActions
{
    public const string Renew = nameof(Renew);
    public const string Liquidate = nameof(Liquidate);

    /// <summary>SW-08: Quản lý ghi nhận tiền cọc, chờ Chủ trọ duyệt.</summary>
    public const string UpdateDeposit = nameof(UpdateDeposit);
}
