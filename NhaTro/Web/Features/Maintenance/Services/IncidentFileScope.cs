using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Storage;

namespace Web.Features.Maintenance.Services;

public sealed class IncidentFileScope(IDbContextFactory<ApplicationDbContext> factory, TimeProvider clock)
    : IIncidentFileScope
{
    public async Task<IncidentFileAccess> ResolveAsync(int incidentId, StorageCaller caller, CancellationToken ct = default)
    {
        if (incidentId <= 0 || string.IsNullOrWhiteSpace(caller.UserId) || caller.IsAdmin) return IncidentFileAccess.None;
        await using var db = await factory.CreateDbContextAsync(ct);
        var incidentRoom = from incident in db.MaintenanceIncidents.AsNoTracking()
                           join room in db.Rooms.AsNoTracking() on incident.RoomId equals room.Id
                           where incident.Id == incidentId && room.IsActive && !room.IsDeleted
                           select new { incident.Id, RoomId = room.Id, room.PropertyId };

        if (caller.IsChuTro || caller.IsQuanLy)
        {
            var properties = PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(),
                new PropertyCaller(caller.UserId, caller.Role));
            var scopedRoomIds = from room in db.Rooms.AsNoTracking()
                                join property in properties on room.PropertyId equals property.Id
                                select room.Id;
            return await incidentRoom.AnyAsync(row => scopedRoomIds.Contains(row.RoomId), ct)
                ? IncidentFileAccess.Manage : IncidentFileAccess.None;
        }

        if (!caller.IsKhachThue) return IncidentFileAccess.None;
        var ownTenantIds = db.Tenants.AsNoTracking().Where(tenant => tenant.UserId == caller.UserId).Select(tenant => tenant.Id);
        var today = clock.GetUtcNow().UtcDateTime.Date;
        var activeTenantIds = from member in db.ContractMembers.AsNoTracking()
                              join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                              where contract.Status == ContractStatus.Approved
                                  && contract.StartDate <= today && contract.EndDate >= today
                              select member.TenantId;
        var tenantRooms = from member in db.ContractMembers.AsNoTracking()
                          join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                          where ownTenantIds.Contains(member.TenantId)
                              && activeTenantIds.Contains(member.TenantId)
                              && contract.Status == ContractStatus.Approved
                              && contract.StartDate <= today && contract.EndDate >= today
                          select contract.RoomId;
        return await incidentRoom.AnyAsync(row => tenantRooms.Contains(row.RoomId), ct)
            ? IncidentFileAccess.Read : IncidentFileAccess.None;
    }
}
