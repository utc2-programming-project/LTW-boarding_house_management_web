// VHT-9 — DTO mẫu P4 (Hợp đồng)
// TODO: đổi namespace cho khớp Rooms/Tenants (VHT-112, VHT-23).
using BoardingHouse.Web.Features.Utilities;

namespace BoardingHouse.Web.Features.Contracts;

/// <summary>Chờ duyệt, Hiệu lực, Sắp hết hạn, Đã thanh lý, Đã hủy, Đã từ chối.</summary>
public enum ContractStatus { PendingApproval, Active, ExpiringSoon, Liquidated, Cancelled, Rejected }

public enum ContractAction { Approve, Reject, Cancel, Renew, Liquidate }

/// <summary>Quản lý có công tắc = Duyệt thì kết quả là PendingApproval (vào hàng đợi VHT-96).</summary>
public enum ActionOutcome { Applied, PendingApproval, Blocked }

public record ActionResult(
    ActionOutcome Outcome,
    int? ApprovalRequestId = null,
    string? Message = null);

public record ContractMemberDto(int TenantId, string FullName, bool IsRepresentative);

public record ContractHistoryDto(DateTime At, int ByUserId, string Event, string? Reason);

public record ContractDto(
    int Id,
    string Code,
    int RoomId,
    int TenantId,
    IReadOnlyList<ContractMemberDto> Members,
    ContractStatus Status,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal RentSnapshot,
    decimal DepositSnapshot,
    IReadOnlyList<ServiceLineDto> ServiceLines,
    decimal InitialElectricIndex,
    decimal InitialWaterIndex,
    int CreatedBy,
    int? ApprovedBy,
    DateTime? ApprovedAt,
    string? RejectReason,
    string? CancelReason,
    IReadOnlyList<ContractHistoryDto> History);

public record ContractCreateRequest(
    int RoomId,
    int TenantId,
    IReadOnlyList<int> MemberTenantIds,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal Rent,
    decimal Deposit,
    IReadOnlyList<ServiceLineDto> Services);

/// <summary>Reason bắt buộc với Reject/Cancel; NewEndDate bắt buộc với Renew; NewRent tùy chọn khi gia hạn.</summary>
public record ContractActionRequest(
    ContractAction Action,
    string? Reason = null,
    DateOnly? NewEndDate = null,
    decimal? NewRent = null);