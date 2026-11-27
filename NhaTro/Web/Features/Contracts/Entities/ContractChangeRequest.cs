namespace Web.Features.Contracts.Entities;

/// <summary>Yêu cầu chỉnh sửa điều khoản do khách thuê gửi cho Chủ trọ/Quản lý.</summary>
public sealed class ContractChangeRequest
{
    public int Id { get; set; }

    public Guid ContractId { get; set; }

    public int ContractVersionId { get; set; }

    public int TenantId { get; set; }

    public string Message { get; set; } = string.Empty;

    public ContractChangeRequestStatus Status { get; set; } = ContractChangeRequestStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }

    public string? ResolvedByUserId { get; set; }

    public string? ResolutionNote { get; set; }
}

public enum ContractChangeRequestStatus
{
    Pending = 0,
    Resolved = 1,
    Dismissed = 2,
}
