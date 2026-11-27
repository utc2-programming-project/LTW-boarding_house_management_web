using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts.Owners;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Vehicles.Rules;

namespace Web.Features.Vehicles.Services;

/// <summary>
/// Kiểm tra phạm vi nhà trọ, phòng và quyền quản lý xe theo vai trò (Chủ trọ, Quản lý, Khách thuê).
/// </summary>
public static class VehicleScope
{
    public static IQueryable<Property> ApplyProperties(ApplicationDbContext db, PropertyCaller caller) =>
        PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), caller);

    public static Task<bool> CanAccessPropertyAsync(
        ApplicationDbContext db,
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default) =>
        ApplyProperties(db, caller).AnyAsync(property => property.Id == propertyId, ct);

    public static async Task<bool> IsOwnerAsync(
        ApplicationDbContext db,
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return false;
        }

        return await db.Properties.AsNoTracking()
            .AnyAsync(property => property.Id == propertyId && property.OwnerId == caller.UserId, ct);
    }

    public static async Task<bool> IsAssignedManagerAsync(
        ApplicationDbContext db,
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (!caller.IsQuanLy)
        {
            return false;
        }

        var blockedOwners = OwnerLockQuery.BlockedOwnerIds(db);
        return await (from assignment in db.PropertyManagers.AsNoTracking()
                      join property in db.Properties.AsNoTracking() on assignment.PropertyId equals property.Id
                      where assignment.PropertyId == propertyId
                          && assignment.ManagerUserId == caller.UserId
                          && assignment.RevokedAt == null
                          && !blockedOwners.Contains(property.OwnerId)
                      select assignment.Id)
            .AnyAsync(ct);
    }

    /// <summary>
    /// Kiểm tra người gọi được phép thêm/sửa/ngừng xe hoặc kiểm kê tại nhà trọ này hay không.
    /// Chủ trọ: luôn được phép trên nhà của mình.
    /// Quản lý: được giao nhà VÀ cấu hình AllowManagerManageVehicles = true.
    /// </summary>
    public static async Task<bool> CanManageVehiclesAsync(
        ApplicationDbContext db,
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        if (await IsOwnerAsync(db, propertyId, caller, ct))
        {
            return true;
        }

        if (!await IsAssignedManagerAsync(db, propertyId, caller, ct))
        {
            return false;
        }

        var allowManager = await db.VehiclePropertySettings.AsNoTracking()
            .Where(setting => setting.PropertyId == propertyId)
            .Select(setting => (bool?)setting.AllowManagerManageVehicles)
            .FirstOrDefaultAsync(ct) ?? true;

        return allowManager;
    }

    /// <summary>
    /// Lấy danh sách hợp đồng đang có hiệu lực (Approved và hôm nay nằm trong StartDate..EndDate)
    /// mà Khách thuê hiện tại là thành viên.
    /// </summary>
    public static IQueryable<Contract> TenantActiveContracts(ApplicationDbContext db, PropertyCaller caller, DateOnly today)
    {
        var todayDate = today.ToDateTime(TimeOnly.MinValue);
        var blockedOwners = OwnerLockQuery.BlockedOwnerIds(db);
        var tenantIds = db.Tenants.AsNoTracking()
            .Where(tenant => tenant.UserId == caller.UserId)
            .Select(tenant => tenant.Id);

        var memberContractIds = db.ContractMembers.AsNoTracking()
            .Where(member => tenantIds.Contains(member.TenantId))
            .Select(member => member.ContractId);

        return from contract in db.Contracts.AsNoTracking()
               join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
               join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
               where contract.Status == ContractStatus.Approved
                   && contract.StartDate <= todayDate
                   && contract.EndDate >= todayDate
                   && !blockedOwners.Contains(property.OwnerId)
                   && memberContractIds.Contains(contract.ContractId)
               select contract;
    }

    public static async Task<List<int>> GetTenantPropertyIdsAsync(
        ApplicationDbContext db,
        PropertyCaller caller,
        DateOnly today,
        CancellationToken ct = default)
    {
        return await (from contract in TenantActiveContracts(db, caller, today)
                      join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                      select room.PropertyId)
            .Distinct()
            .ToListAsync(ct);
    }

    public static async Task<List<int>> GetTenantRoomIdsAsync(
        ApplicationDbContext db,
        PropertyCaller caller,
        DateOnly today,
        CancellationToken ct = default)
    {
        return await TenantActiveContracts(db, caller, today)
            .Select(contract => contract.RoomId)
            .Distinct()
            .ToListAsync(ct);
    }
}
