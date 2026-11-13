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
    int PropertyId,
    string PropertyName,
    string? PropertyAddress,
    string RoomCode,
    int? RoomFloor,
    decimal? RoomAreaM2,
    DateTime StartDate,
    DateTime EndDate,
    decimal SnapshotPrice,
    decimal DepositAmount,
    ContractStatus Status,
    string? RejectReason,
    int PaymentCycleMonths,
    int? MeterReadingDay,
    string? Terms,
    IReadOnlyList<ContractMemberListItemDto> Members,
    IReadOnlyList<ContractServiceLineDto> Services,
    bool CanRenew,
    bool CanLiquidate,
    bool CanEditDeposit,
    ContractPendingApproval PendingApproval,
    ContractLinkDto? Successor,
    ContractLinkDto? Previous,
    int? VersionNo = null,
    string? ContentHash = null,
    DateTimeOffset? SentAt = null,
    DateTimeOffset? AcceptedAt = null,
    decimal? DepositPaidAmount = null,
    DateTimeOffset? DepositPaidAt = null,
    DateTimeOffset? DepositDueAt = null,
    decimal? DepositRefundAmount = null,
    DateTimeOffset? DepositRefundedAt = null);

public sealed class ContractListFilter
{
    public string? Keyword { get; set; }
    public string StatusKey { get; set; } = "all";
    public string? PropertyName { get; set; }
    public DateTime? TermFrom { get; set; }
    public DateTime? TermTo { get; set; }
    public decimal? MinRent { get; set; }
    public decimal? MaxRent { get; set; }
    public string Sort { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed record ContractListStatsDto(
    int Total, int Active, int ExpiringSoon, int Pending, int PendingDeposit);

public sealed record ContractListPageDto(
    IReadOnlyList<ContractListItemDto> Items,
    int TotalCount,
    ContractListStatsDto Stats);

/// <summary>Dữ liệu gọn cho Home: tối đa vài hợp đồng sắp hết hạn + hợp đồng đang thuê của khách.</summary>
public sealed record ContractHomeSnapshotDto(
    IReadOnlyList<ContractListItemDto> Expiring,
    int ExpiringCount,
    IReadOnlyList<ContractListItemDto> ActiveForTenant);

public sealed record ContractMemberListItemDto(
    int TenantId,
    string FullName,
    string Phone,
    string? Relationship = null,
    bool IsRepresentative = false);

/// <summary>Một dòng dịch vụ áp cho hợp đồng, giá hiệu lực tại thời điểm xem.</summary>
public sealed record ContractServiceLineDto(
    int ServiceCatalogId,
    string Name,
    string Unit,
    string CalculationMethod,
    decimal UnitPrice,
    bool IsRegistered);
