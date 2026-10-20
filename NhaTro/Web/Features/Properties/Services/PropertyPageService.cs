using Web.Common.Models;
using Web.Features.Properties.DTOs;

namespace Web.Features.Properties.Services;

/// <summary>Thin UI adapter: resolves the authenticated caller and delegates to the P2 property service.</summary>
public sealed class PropertyPageService(P2CallerProvider callerProvider, IPropertyService properties)
{
    public async Task<string> GetCurrentRoleAsync() => (await callerProvider.GetCallerAsync()).Role;

    public async Task<PagedResult<PropertyListItem>> ListAsync(
        PropertyFilter filter, CancellationToken ct = default) =>
        await properties.ListAsync(filter, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyResult> GetAsync(int id, CancellationToken ct = default) =>
        await properties.GetAsync(id, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyResult> CreateAsync(PropertySave request, CancellationToken ct = default) =>
        await properties.CreateAsync(request, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyResult> UpdateAsync(int id, PropertySave request, CancellationToken ct = default) =>
        await properties.UpdateAsync(id, request, await callerProvider.GetCallerAsync(), ct);
}
