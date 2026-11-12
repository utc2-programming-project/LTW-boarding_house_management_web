using Web.Common.Models;
using Web.Features.Maintenance.DTOs;
using Web.Features.Properties;

namespace Web.Features.Maintenance.Services;

public interface IIncidentService
{
    Task<IReadOnlyList<IncidentRoomDto>> RoomsAsync(PropertyCaller caller, CancellationToken cancellationToken = default);
    Task<IncidentResult> CreateAsync(
        IncidentCreateRequest request, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentListItemDto>> ListAsync(
        PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IncidentResult> GetAsync(
        int incidentId, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IncidentResult> UpdateAsync(
        int incidentId, IncidentUpdateRequest request, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IncidentResult> DeleteAsync(
        int incidentId, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IncidentResult> RestoreAsync(
        int incidentId, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentTrashItemDto>> ListDeletedAsync(
        PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IncidentResult> PurgeAsync(
        int incidentId, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<TrashPurgeSummary> PurgeAllAsync(
        PropertyCaller caller, CancellationToken cancellationToken = default);
}
