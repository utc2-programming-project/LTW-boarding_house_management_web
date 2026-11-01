using Web.Features.Approvals.Enums;

namespace Web.Features.Approvals.DTOs;

/// <summary>Dữ liệu caller của mã SW gửi vào hàng đợi (VHT-96).</summary>
public sealed class CreateApprovalRequest
{
    public string OperationCode { get; init; } = string.Empty;

    public ApprovalTargetType TargetType { get; init; }

    public string TargetKey { get; init; } = string.Empty;

    public int? PropertyId { get; init; }

    /// <summary>JSON dữ liệu đề xuất; applier của mã SW tự đọc theo schema của nó.</summary>
    public string PayloadJson { get; init; } = string.Empty;

    /// <summary>Rowversion của đối tượng lúc tạo yêu cầu; null với thao tác tạo mới.</summary>
    public byte[]? ExpectedToken { get; init; }
}

/// <summary>Một yêu cầu duyệt trả cho UI (VHT-97) và cho test.</summary>
public sealed record ApprovalRequestDto(
    Guid Id,
    string OperationCode,
    ApprovalTargetType TargetType,
    string TargetKey,
    int? PropertyId,
    string PayloadJson,
    ApprovalRequestStatus Status,
    string RequestedByUserId,
    DateTime RequestedAt,
    string? DecidedByUserId,
    DateTime? DecidedAt,
    string? DecisionReason);

/// <summary>Quyết định từ chối: lý do bắt buộc, tối đa 500 ký tự.</summary>
public sealed class RejectApprovalRequest
{
    public const int MaxReasonLength = 500;

    public string Reason { get; init; } = string.Empty;
}

/// <summary>Kết quả một thao tác trên hàng đợi duyệt.</summary>
public sealed class ApprovalResult
{
    private ApprovalResult()
    {
    }

    public required bool Succeeded { get; init; }

    /// <summary>null khi thất bại.</summary>
    public ApprovalRequestDto? Request { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static ApprovalResult Success(ApprovalRequestDto request) =>
        new() { Succeeded = true, Request = request };

    public static ApprovalResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Kết quả đọc danh sách yêu cầu.</summary>
public sealed class ApprovalListResult
{
    private ApprovalListResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<ApprovalRequestDto> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static ApprovalListResult Success(IReadOnlyList<ApprovalRequestDto> items) =>
        new() { Succeeded = true, Items = items };

    public static ApprovalListResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
