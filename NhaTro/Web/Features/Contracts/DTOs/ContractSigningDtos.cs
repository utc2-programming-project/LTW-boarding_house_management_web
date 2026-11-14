using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.DTOs;

/// <summary>Dữ liệu Chủ trọ/Quản lý được sửa khi hợp đồng còn ở bản nháp hoặc bị khách yêu cầu chỉnh sửa.</summary>
public sealed class UpdateContractDraftRequest
{
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public decimal? MonthlyRent { get; set; }

    public decimal? DepositAmount { get; set; }

    public int PaymentCycleMonths { get; set; } = 1;

    public int? MeterReadingDay { get; set; }

    public string? Terms { get; set; }

    public int? RepresentativeTenantId { get; set; }

    public List<int> TenantIds { get; set; } = [];

    public IReadOnlyDictionary<int, string>? MemberRelations { get; set; }
}

/// <summary>Khách đã nhận lời mời/QR cho phòng và đang chờ Chủ trọ/Quản lý lập hợp đồng.</summary>
public sealed record PendingRoomAssignmentDto(
    int Id,
    int TenantId,
    string TenantName,
    int RoomId,
    string RoomCode,
    int PropertyId,
    string PropertyName,
    DateTime CreatedAt);

public sealed record ContractChangeRequestDto(
    int Id,
    int VersionNo,
    int TenantId,
    string TenantName,
    string Message,
    ContractChangeRequestStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNote);

/// <summary>Thông tin khách cần để xem và xác nhận hợp đồng.</summary>
public sealed record TenantContractReviewDto(
    ContractDetailDto Contract,
    int VersionNo,
    string ContentHash,
    DateTimeOffset? SentAt,
    bool CanAccept,
    bool CanRequestChanges,
    bool IsAccepted,
    DateTimeOffset? AcceptedAt,
    IReadOnlyList<ContractChangeRequestDto> ChangeRequests);
