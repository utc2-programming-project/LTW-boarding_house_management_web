using System.ComponentModel.DataAnnotations;

namespace Web.Features.Contracts.DTOs;

public sealed class ApproveContractRequestDto
{
    public const int MaxRejectReasonLength = 500;

    public bool IsApproved { get; set; }

    [MaxLength(MaxRejectReasonLength)]
    public string? RejectReason { get; set; }
}
