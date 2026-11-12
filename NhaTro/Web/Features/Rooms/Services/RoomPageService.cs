using Web.Common.Models;
using Web.Features.Properties.DTOs;
using Web.Features.Properties;
using Web.Features.Properties.Services;
using Web.Features.Rooms.DTOs;

namespace Web.Features.Rooms.Services;

/// <summary>Thin UI adapter for room operations and the property choices used by room forms.</summary>
public sealed class RoomPageService(
    P2CallerProvider callerProvider,
    IRoomService rooms,
    IPropertyService properties)
{
    public async Task<string> GetCurrentRoleAsync() => (await callerProvider.GetCallerAsync()).Role;

    public async Task<RoomSummary> GetSummaryAsync(int? propertyId = null, CancellationToken ct = default) =>
        await rooms.GetSummaryForPropertyAsync(await callerProvider.GetCallerAsync(), propertyId, ct);

    public async Task<PagedResult<RoomListItem>> ListAsync(
        RoomFilter filter, CancellationToken ct = default) =>
        await rooms.ListAsync(filter, await callerProvider.GetCallerAsync(), ct);

    public async Task<RoomResult> GetAsync(int id, CancellationToken ct = default) =>
        await rooms.GetAsync(id, await callerProvider.GetCallerAsync(), ct);

    public async Task<RoomResult> CreateAsync(RoomSave request, CancellationToken ct = default) =>
        await rooms.CreateAsync(request, await callerProvider.GetCallerAsync(), ct);

    public async Task<RoomResult> UpdateAsync(int id, RoomSave request, CancellationToken ct = default) =>
        await rooms.UpdateAsync(id, request, await callerProvider.GetCallerAsync(), ct);

    public async Task<RoomResult> DeleteAsync(int id, CancellationToken ct = default) =>
        await rooms.DeleteAsync(id, await callerProvider.GetCallerAsync(), ct);

    public async Task<PagedResult<RoomTrashItem>> ListDeletedAsync(
        RoomFilter filter, CancellationToken ct = default) =>
        await rooms.ListDeletedAsync(filter, await callerProvider.GetCallerAsync(), ct);

    public async Task<RoomResult> RestoreAsync(int id, CancellationToken ct = default) =>
        await rooms.RestoreAsync(id, await callerProvider.GetCallerAsync(), ct);

    public async Task<RoomResult> PurgeAsync(int id, CancellationToken ct = default) =>
        await rooms.PurgeAsync(id, await callerProvider.GetCallerAsync(), ct);

    public async Task<TrashPurgeSummary> PurgeAllAsync(CancellationToken ct = default) =>
        await rooms.PurgeAllAsync(await callerProvider.GetCallerAsync(), ct);

    public async Task<IReadOnlyList<PropertyListItem>> GetPropertyOptionsAsync(CancellationToken ct = default)
    {
        const int pageSize = 100;
        var caller = await callerProvider.GetCallerAsync();
        var firstPage = await properties.ListAsync(new PropertyFilter { Page = 1, PageSize = pageSize }, caller, ct);
        var results = firstPage.Items.ToList();
        var pageCount = (int)Math.Ceiling(firstPage.TotalCount / (double)pageSize);

        for (var page = 2; page <= pageCount; page++)
        {
            var next = await properties.ListAsync(new PropertyFilter { Page = page, PageSize = pageSize }, caller, ct);
            results.AddRange(next.Items);
        }

        return results;
    }
}
