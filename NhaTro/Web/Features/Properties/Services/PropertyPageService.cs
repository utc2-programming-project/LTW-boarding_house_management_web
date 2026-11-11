using Web.Common.Models;
using Web.Features.Properties.DTOs;

namespace Web.Features.Properties.Services;

/// <summary>Tổng hợp cho dải thẻ KPI ở trang danh sách nhà trọ (không phụ thuộc bộ lọc).</summary>
public sealed record PropertyListSummary(int Total, int Active, int Inactive, int Rooms);

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

    // Tổng hợp toàn bộ nhà trọ trong phạm vi người xem (list phân trang nên đọc lần lượt từng lô).
    public async Task<PropertyListSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var caller = await callerProvider.GetCallerAsync();
        var total = 0;
        var active = 0;
        var rooms = 0;
        for (var page = 1; ; page++)
        {
            var batch = await properties.ListAsync(new PropertyFilter { Page = page, PageSize = 100 }, caller, ct);
            total += batch.Items.Count;
            active += batch.Items.Count(item => item.IsActive);
            rooms += batch.Items.Sum(item => item.RoomCount);
            if (page * 100L >= batch.TotalCount) break;
        }
        return new PropertyListSummary(total, active, total - active, rooms);
    }

    public async Task<PropertyResult> GetAsync(int id, CancellationToken ct = default) =>
        await properties.GetAsync(id, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyResult> CreateAsync(PropertySave request, CancellationToken ct = default) =>
        await properties.CreateAsync(request, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyResult> UpdateAsync(int id, PropertySave request, CancellationToken ct = default) =>
        await properties.UpdateAsync(id, request, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyResult> DeleteAsync(int id, CancellationToken ct = default) =>
        await properties.DeleteAsync(id, await callerProvider.GetCallerAsync(), ct);

    public async Task<PagedResult<PropertyTrashItem>> ListDeletedAsync(
        PropertyFilter filter, CancellationToken ct = default) =>
        await properties.ListDeletedAsync(filter, await callerProvider.GetCallerAsync(), ct);

    public async Task<PropertyResult> RestoreAsync(int id, CancellationToken ct = default) =>
        await properties.RestoreAsync(id, await callerProvider.GetCallerAsync(), ct);

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

    public async Task<PropertyManagerListResult> GetManagerAssignmentsAsync(
        int propertyId, CancellationToken ct = default) =>
        await propertyManagers.ListAssignmentsAsync(propertyId, await callerProvider.GetCallerAsync(), ct: ct);

    public async Task<PropertyManagerCandidateResult> GetAssignableManagersAsync(
        int propertyId, CancellationToken ct = default) =>
        await propertyManagers.ListAssignableManagersAsync(propertyId, await callerProvider.GetCallerAsync(), ct);
}
