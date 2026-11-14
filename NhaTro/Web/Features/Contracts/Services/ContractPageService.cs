using Web.Common.Models;
using Web.Features.Contracts.DTOs;
using Web.Features.Properties.Services;

namespace Web.Features.Contracts.Services;

/// <summary>Resolves the authenticated server-side caller before every contract operation.</summary>
public sealed class ContractPageService(
    P2CallerProvider callerProvider,
    IContractService contracts,
    IContractSigningService signing)
{
    public async Task<ContractCreateOptionsDto> GetCreateOptionsAsync(CancellationToken cancellationToken = default) =>
        await contracts.GetCreateOptionsAsync(await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<IReadOnlyList<ContractListItemDto>> ListAsync(
        string? keyword = null,
        CancellationToken cancellationToken = default) =>
        await contracts.ListAsync(keyword, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractListPageDto> GetListPageAsync(
        ContractListFilter filter, CancellationToken cancellationToken = default) =>
        await contracts.GetListPageAsync(filter, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<IReadOnlyList<string>> GetPropertyNamesAsync(
        CancellationToken cancellationToken = default) =>
        await contracts.GetPropertyNamesAsync(await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractHomeSnapshotDto> GetHomeSnapshotAsync(
        DateOnly today, CancellationToken cancellationToken = default) =>
        await contracts.GetHomeSnapshotAsync(await callerProvider.GetCallerAsync(), today, cancellationToken: cancellationToken);

    public async Task<ContractDetailDto> GetAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        await contracts.GetAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractResponseDto> CreateAsync(
        CreateContractRequest request,
        CancellationToken cancellationToken = default) =>
        await contracts.CreateContractAsync(request, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<TenantContractReviewDto> GetTenantReviewAsync(
        Guid contractId, CancellationToken cancellationToken = default) =>
        await signing.GetForTenantAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task IssueAcceptanceOtpAsync(
        Guid contractId, CancellationToken cancellationToken = default) =>
        await signing.IssueAcceptanceOtpAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> SendToTenantAsync(
        Guid contractId, CancellationToken cancellationToken = default) =>
        await signing.SendToTenantAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> UpdateDraftAsync(
        Guid contractId, UpdateContractDraftRequest request, CancellationToken cancellationToken = default) =>
        await signing.UpdateDraftAsync(contractId, request, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> RequestContractChangesAsync(
        Guid contractId, string message, CancellationToken cancellationToken = default) =>
        await signing.RequestChangesAsync(contractId, message, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> AcceptContractAsync(
        Guid contractId, string otpCode, CancellationToken cancellationToken = default) =>
        await signing.AcceptAsync(contractId, otpCode, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> WithdrawContractAsync(
        Guid contractId, CancellationToken cancellationToken = default) =>
        await signing.WithdrawAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<IReadOnlyList<ContractChangeRequestDto>> ListChangeRequestsAsync(
        Guid contractId, CancellationToken cancellationToken = default) =>
        await signing.ListChangeRequestsAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<IReadOnlyList<PendingRoomAssignmentDto>> ListPendingAssignmentsAsync(
        CancellationToken cancellationToken = default) =>
        await signing.ListPendingAssignmentsAsync(await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractResponseDto> DecideAsync(
        Guid contractId,
        ApproveContractRequestDto request,
        CancellationToken cancellationToken = default) =>
        await contracts.ApproveContractAsync(
            contractId,
            request,
            await callerProvider.GetCallerAsync(),
            cancellationToken);

    public async Task<ContractActionResultDto> RenewAsync(
        Guid contractId,
        RenewContractRequestDto request,
        CancellationToken cancellationToken = default) =>
        await contracts.RenewContractAsync(
            contractId,
            request,
            await callerProvider.GetCallerAsync(),
            cancellationToken);

    public async Task<ContractActionResultDto> LiquidateAsync(
        Guid contractId,
        CancellationToken cancellationToken = default) =>
        await contracts.LiquidateContractAsync(
            contractId,
            await callerProvider.GetCallerAsync(),
            cancellationToken);

    public async Task<ContractActionResultDto> UpdateDepositAsync(Guid contractId, decimal depositAmount, CancellationToken cancellationToken = default) =>
        await contracts.UpdateDepositAsync(contractId, depositAmount, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> RecordDepositPaymentAsync(
        Guid contractId, CancellationToken cancellationToken = default) =>
        await contracts.RecordDepositPaymentAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> DeleteAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        await contracts.DeleteAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> RestoreAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        await contracts.RestoreAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<IReadOnlyList<ContractTrashItemDto>> ListDeletedAsync(CancellationToken cancellationToken = default) =>
        await contracts.ListDeletedAsync(await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractActionResultDto> PurgeAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        await contracts.PurgeAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<TrashPurgeSummary> PurgeAllAsync(CancellationToken cancellationToken = default) =>
        await contracts.PurgeAllAsync(await callerProvider.GetCallerAsync(), cancellationToken);
}
