using Web.Common.Models;
using Web.Features.Contracts.DTOs;
using Web.Features.Properties;

namespace Web.Features.Contracts.Services;

/// <summary>Luồng gửi hợp đồng cho khách xem, khách đồng ý / yêu cầu sửa và lưu bằng chứng.</summary>
public interface IContractSigningService
{
    Task<TenantContractReviewDto> GetForTenantAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task IssueAcceptanceOtpAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractActionResultDto> SendToTenantAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractActionResultDto> UpdateDraftAsync(
        Guid contractId,
        UpdateContractDraftRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractActionResultDto> RequestChangesAsync(
        Guid contractId,
        string message,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractActionResultDto> AcceptAsync(
        Guid contractId,
        string otpCode,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<ContractActionResultDto> WithdrawAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ContractChangeRequestDto>> ListChangeRequestsAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingRoomAssignmentDto>> ListPendingAssignmentsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default);
}
