using Web.Common.Models;
using Web.Features.Properties.DTOs;

namespace Web.Features.Properties.Services;

/// <summary>Thin UI adapter: resolves the authenticated caller and delegates to the P2 property service.</summary>
public sealed class PropertyPageService(
    P2CallerProvider callerProvider,
    IPropertyService properties,
    IPropertyManagerService propertyManagers)
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

    public async Task<PropertyManagerListResult> ListManagersAsync(
        int propertyId, bool includeHistory = false, CancellationToken ct = default) =>
        await propertyManagers.ListAssignmentsAsync(
            propertyId, await callerProvider.GetCallerAsync(), includeHistory, ct);

    public async Task<PropertyManagerCandidateListResult> ListManagerCandidatesAsync(
        int propertyId, CancellationToken ct = default) =>
        await propertyManagers.ListCandidatesAsync(
            propertyId, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyManagerResult> AssignManagerAsync(
        int propertyId, string managerUserId, CancellationToken ct = default) =>
        await propertyManagers.AssignAsync(
            propertyId, managerUserId, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyManagerResult> RevokeManagerAsync(
        int propertyId, string managerUserId, CancellationToken ct = default) =>
        await propertyManagers.RevokeAsync(
            propertyId, managerUserId, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyManagerListResult> ListManagerAssignmentsAsync(
        string managerUserId, bool includeHistory = false, CancellationToken ct = default) =>
        await propertyManagers.ListAssignmentsForManagerAsync(
            managerUserId, await callerProvider.GetCallerAsync(), includeHistory, ct);

    public async Task<PropertyManagerPropertyListResult> ListAssignablePropertiesAsync(
        string managerUserId, CancellationToken ct = default) =>
        await propertyManagers.ListAssignablePropertiesAsync(
            managerUserId, await callerProvider.GetCallerAsync(), ct);

    public async Task<ManagerOwnerResult> GetManagerOwnerAsync(CancellationToken ct = default) =>
        await propertyManagers.GetOwnerForManagerAsync(
            await callerProvider.GetCallerAsync(), ct);
}
