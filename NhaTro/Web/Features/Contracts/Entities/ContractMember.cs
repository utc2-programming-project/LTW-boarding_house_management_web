namespace Web.Features.Contracts.Entities;

/// <summary>Liên kết một khách thuê với hợp đồng.</summary>
public sealed class ContractMember
{
    public Guid ContractId { get; set; }

    public int TenantId { get; set; }

    /// <summary>Quan hệ với người đại diện (Bản thân, Vợ/Chồng, Con, Bạn cùng phòng...).</summary>
    public string? Relationship { get; set; }

    /// <summary>Người đại diện đứng tên hợp đồng.</summary>
    public bool IsRepresentative { get; set; }
}
