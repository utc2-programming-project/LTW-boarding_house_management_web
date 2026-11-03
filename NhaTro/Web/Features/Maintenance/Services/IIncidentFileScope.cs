using Web.Features.Storage;

namespace Web.Features.Maintenance.Services;

public enum IncidentFileAccess { None = 0, Read = 1, Manage = 2 }

public interface IIncidentFileScope
{
    Task<IncidentFileAccess> ResolveAsync(int incidentId, StorageCaller caller, CancellationToken ct = default);
}
