using Web.Features.Contracts.DTOs;
using Web.Features.Properties.Services;

namespace Web.Features.Contracts.Services;

/// <summary>Resolves the authenticated server-side caller before every contract operation.</summary>
public sealed class ContractPageService(P2CallerProvider callerProvider, IContractService contracts)
{
    public async Task<ContractCreateOptionsDto> GetCreateOptionsAsync(CancellationToken cancellationToken = default) =>
        await contracts.GetCreateOptionsAsync(await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<IReadOnlyList<ContractListItemDto>> ListAsync(
        string? keyword = null,
        CancellationToken cancellationToken = default) =>
        await contracts.ListAsync(keyword, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractDetailDto> GetAsync(Guid contractId, CancellationToken cancellationToken = default) =>
        await contracts.GetAsync(contractId, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractResponseDto> CreateAsync(
        CreateContractRequest request,
        CancellationToken cancellationToken = default) =>
        await contracts.CreateContractAsync(request, await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractResponseDto> DecideAsync(
        Guid contractId,
        ApproveContractRequestDto request,
        CancellationToken cancellationToken = default) =>
        await contracts.ApproveContractAsync(
            contractId,
            request,
            await callerProvider.GetCallerAsync(),
            cancellationToken);
}
