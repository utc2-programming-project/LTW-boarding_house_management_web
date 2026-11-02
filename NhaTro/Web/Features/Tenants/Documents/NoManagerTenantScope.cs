namespace Web.Features.Tenants.Documents;

/// <summary>
/// Fake deny-all giữ cho test (unit + integration) không phụ thuộc bảng phân công.
/// Production dùng <see cref="ContractManagerTenantScope"/> (VHT-119).
/// </summary>
public sealed class NoManagerTenantScope : IManagerTenantScope
{
    public Task<bool> IsTenantInScopeAsync(
        string managerUserId,
        int tenantId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
