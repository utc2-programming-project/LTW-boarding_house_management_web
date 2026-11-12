using Web.Common.SoftDelete;
using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.Entities;

/// <summary>Snapshot dữ liệu chính của hợp đồng thuê.</summary>
public sealed class Contract : ISoftDeletable
{
    public Guid ContractId { get; set; } = Guid.NewGuid();

    public string ContractNumber { get; set; } = string.Empty;

    public int RoomId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal SnapshotPrice { get; set; }

    public decimal DepositAmount { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Pending;

    public string? RejectReason { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }

    public ICollection<ContractMember> Members { get; set; } = new List<ContractMember>();
}
