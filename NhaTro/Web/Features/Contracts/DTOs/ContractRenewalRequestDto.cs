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

    public bool IsPending => Status == RenewalRequestStatus.Pending;
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

    /// <summary>Thành công kèm yêu cầu mới/đang chờ; <c>null</c> khi hợp đồng chưa có yêu cầu nào đang chờ.</summary>
    public static ContractRenewalResult Success(ContractRenewalRequestDto? request) =>
        new() { Succeeded = true, Request = request };

    public static ContractRenewalResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
