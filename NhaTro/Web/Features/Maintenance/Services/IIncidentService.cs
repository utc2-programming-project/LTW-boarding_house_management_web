using Web.Features.Maintenance.DTOs;
using Web.Features.Properties;

namespace Web.Features.Maintenance.Services;

public interface IIncidentService
{
    Task<IncidentResult> CreateAsync(
        IncidentCreateRequest request, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IncidentListItemDto>> ListAsync(
        PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IncidentResult> GetAsync(
        int incidentId, PropertyCaller caller, CancellationToken cancellationToken = default);

    Task<IncidentResult> UpdateAsync(
        int incidentId, IncidentUpdateRequest request, PropertyCaller caller, CancellationToken cancellationToken = default);
}
