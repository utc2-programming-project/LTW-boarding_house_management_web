using Web.Features.Contracts.DTOs;
using Web.Features.Properties.Services;

namespace Web.Features.Contracts.Services;

/// <summary>Resolves the authenticated Chủ trọ / Quản lý caller before touching the renewal queue.</summary>
public sealed class ContractRenewalPageService(P2CallerProvider callerProvider, IContractRenewalService renewals)
{
    public async Task<RenewalRequestListResult> ListPendingAsync(CancellationToken cancellationToken = default) =>
        await renewals.ListPendingAsync(await callerProvider.GetCallerAsync(), cancellationToken);

    public async Task<ContractRenewalResult> DecideAsync(
        Guid requestId,
        RenewalDecisionRequest decision,
        CancellationToken cancellationToken = default) =>
        await renewals.DecideAsync(
            requestId,
            decision,
            await callerProvider.GetCallerAsync(),
            cancellationToken);
}
