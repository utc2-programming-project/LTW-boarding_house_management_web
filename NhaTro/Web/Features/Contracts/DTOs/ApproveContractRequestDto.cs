namespace Web.Features.Contracts.DTOs;

public sealed class ApproveContractRequestDto
{
    public bool IsApproved { get; set; }

    public string? RejectReason { get; set; }
}
