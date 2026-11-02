using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Properties.Entities;

namespace Web.Features.Tenants.Documents;

/// <summary>
/// Phạm vi Quản lý theo tòa nhà cho giấy tờ P3: chỉ trả <c>true</c> khi Quản lý đang có
/// phân công hiệu lực (<see cref="PropertyManager.RevokedAt"/> == null)
/// cho nhà chứa hợp đồng mà khách thuê là thành viên.
/// </summary>
public sealed class PropertyManagerTenantScope(ApplicationDbContext db) : IManagerTenantScope
{
    public Task<bool> IsTenantInScopeAsync(
        string managerUserId,
        int tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(managerUserId))
        {
            return Task.FromResult(false);
        }

        return (
            from member in db.ContractMembers.AsNoTracking()
            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
            join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            join assignment in db.PropertyManagers.AsNoTracking() on property.Id equals assignment.PropertyId
            where member.TenantId == tenantId
                && assignment.ManagerUserId == managerUserId
                && assignment.RevokedAt == null
            select property.Id)
            .AnyAsync(cancellationToken);
    }
}
