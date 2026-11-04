using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.DTOs;

public sealed record ContractListItemDto(
    Guid ContractId,
    string ContractNumber,
    string PropertyName,
    string RoomCode,
    DateTime StartDate,
    DateTime EndDate,
    decimal SnapshotPrice,
    decimal DepositAmount,
    ContractStatus Status,
    IReadOnlyList<string> TenantNames);

public sealed record ContractDetailDto(
    Guid ContractId,
    string ContractNumber,
    string PropertyName,
    string RoomCode,
    DateTime StartDate,
    DateTime EndDate,
    decimal SnapshotPrice,
    decimal DepositAmount,
    ContractStatus Status,
    string? RejectReason,
    IReadOnlyList<ContractMemberListItemDto> Members,
    bool CanRenew,
    bool CanLiquidate);

public sealed record ContractMemberListItemDto(int TenantId, string FullName, string Phone);
