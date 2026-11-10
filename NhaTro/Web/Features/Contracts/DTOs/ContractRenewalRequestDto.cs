using Web.Features.Contracts.Enums;

namespace Web.Features.Contracts.DTOs;

/// <summary>Một yêu cầu gia hạn hợp đồng, dùng cho UI "Phòng đang thuê".</summary>
public sealed class ContractRenewalRequestDto
{
    public required Guid Id { get; init; }

    public required Guid ContractId { get; init; }

    public required int RequestedMonths { get; init; }

    public string? Note { get; init; }

    public required RenewalRequestStatus Status { get; init; }

    public required DateTime RequestedAt { get; init; }

    /// <summary>UTC thời điểm BQL quyết định; <c>null</c> khi còn chờ xử lý.</summary>
    public DateTime? DecidedAt { get; init; }

    /// <summary>Lý do từ chối khách thuê nhìn thấy; <c>null</c> khi chưa bị từ chối.</summary>
    public string? RejectReason { get; init; }

    public bool IsPending => Status == RenewalRequestStatus.Pending;
}

/// <summary>Phiên bản một yêu cầu gia hạn dành cho bảng duyệt của Chủ trọ / Quản lý.</summary>
public sealed class RenewalRequestItemDto
{
    public required Guid Id { get; init; }

    public required Guid ContractId { get; init; }

    public required string ContractNumber { get; init; }

    public required string RoomCode { get; init; }

    public required string PropertyName { get; init; }

    /// <summary>Họ tên khách thuê đã gửi yêu cầu.</summary>
    public required string RequesterName { get; init; }

    public required int RequestedMonths { get; init; }

    public string? Note { get; init; }

    public required DateTime RequestedAt { get; init; }

    /// <summary>Ngày kết thúc của hợp đồng tại thời điểm khách gửi yêu cầu.</summary>
    public required DateTime CurrentEndDate { get; init; }

    /// <summary>Ngày kết thúc đề xuất nếu chấp thuận: <see cref="CurrentEndDate"/> cộng thêm số tháng khách xin.</summary>
    public DateTime ProposedEndDate => CurrentEndDate.AddMonths(RequestedMonths);
}

/// <summary>Quyết định của Chủ trọ / Quản lý cho một yêu cầu gia hạn.</summary>
public sealed class RenewalDecisionRequest
{
    public required bool IsApproved { get; init; }

    /// <summary>Bắt buộc khi từ chối; bản Trim không được trống và tối đa 500 ký tự.</summary>
    public string? Reason { get; init; }
}

/// <summary>Kết quả đọc danh sách yêu cầu đang chờ của một Chủ trọ / Quản lý.</summary>
public sealed class RenewalRequestListResult
{
    private RenewalRequestListResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<RenewalRequestItemDto> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static RenewalRequestListResult Success(IReadOnlyList<RenewalRequestItemDto> items) =>
        new() { Succeeded = true, Items = items };

    public static RenewalRequestListResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Kết quả đọc lịch sử yêu cầu gia hạn của một hợp đồng phía khách thuê.</summary>
public sealed class RenewalHistoryResult
{
    private RenewalHistoryResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<ContractRenewalRequestDto> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static RenewalHistoryResult Success(IReadOnlyList<ContractRenewalRequestDto> items) =>
        new() { Succeeded = true, Items = items };

    public static RenewalHistoryResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Kết quả ghi/đọc yêu cầu gia hạn.</summary>
public sealed class ContractRenewalResult
{
    private ContractRenewalResult()
    {
    }

    public required bool Succeeded { get; init; }

    public ContractRenewalRequestDto? Request { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    /// <summary>Quản lý đã chấp thuận nhưng công tắc SW-07 bắt Chủ trọ duyệt: yêu cầu vẫn còn chờ.</summary>
    public bool AwaitingOwnerApproval { get; init; }

    /// <summary>Thành công kèm yêu cầu mới/đang chờ; <c>null</c> khi hợp đồng chưa có yêu cầu nào đang chờ.</summary>
    public static ContractRenewalResult Success(ContractRenewalRequestDto? request) =>
        new() { Succeeded = true, Request = request };

    public static ContractRenewalResult PendingOwnerApproval(ContractRenewalRequestDto request) =>
        new() { Succeeded = true, Request = request, AwaitingOwnerApproval = true };

    public static ContractRenewalResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
