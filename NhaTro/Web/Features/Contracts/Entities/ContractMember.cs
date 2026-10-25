namespace Web.Features.Contracts.Entities;

/// <summary>Liên kết một khách thuê với hợp đồng.</summary>
public sealed class ContractMember
{
    public Guid ContractId { get; set; }

    public int TenantId { get; set; }
}
