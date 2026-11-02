using Microsoft.EntityFrameworkCore;
using Web.Data;

namespace Web.Features.Tenants.Documents;

/// <summary>
/// Phạm vi giấy tờ P3:
/// <list type="bullet">
/// <item>Admin: luôn <see cref="TenantDocumentAccessLevel.None"/>.</item>
/// <item>Khách thuê: chỉ hồ sơ có <c>UserId</c> của chính mình.</item>
/// <item>Chủ trọ: hồ sơ có hợp đồng tại tòa mình sở hữu.</item>
/// <item>Quản lý: chỉ khi <see cref="IManagerTenantScope"/> xác nhận tòa nhà được giao (VHT-24).</item>
/// </list>
/// Chủ trọ lẫn Quản lý đều cần chuỗi hợp đồng: kho hồ sơ chưa gắn không mở giấy tờ,
/// tránh lộ ảnh CCCD chéo chủ (VHT-119 Q1-A/Q2-A).
/// </summary>
public sealed class TenantDocumentScopeService(
    ApplicationDbContext db,
    IManagerTenantScope managerTenantScope) : ITenantDocumentScopeService
{
    public async Task<TenantDocumentAccessLevel> ResolveAsync(
        int tenantId,
        TenantCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (caller.IsAdmin)
        {
            return TenantDocumentAccessLevel.None;
        }

        var tenant = await db.Tenants.AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.UserId })
            .FirstOrDefaultAsync(cancellationToken);

        if (tenant is null)
        {
            return TenantDocumentAccessLevel.None;
        }

        if (caller.IsChuTro)
        {
            if (string.IsNullOrWhiteSpace(caller.UserId))
            {
                return TenantDocumentAccessLevel.None;
            }

            var belongsToOwner = await (
                from member in db.ContractMembers.AsNoTracking()
                join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                where member.TenantId == tenantId && property.OwnerId == caller.UserId
                select property.Id
            ).AnyAsync(cancellationToken);

            return belongsToOwner ? TenantDocumentAccessLevel.Owner : TenantDocumentAccessLevel.None;
        }

        if (caller.IsTenant)
        {
            return tenant.UserId is not null && tenant.UserId == caller.UserId
                ? TenantDocumentAccessLevel.Own
                : TenantDocumentAccessLevel.None;
        }

        if (caller.IsQuanLy
            && await managerTenantScope.IsTenantInScopeAsync(caller.UserId, tenantId, cancellationToken))
        {
            return TenantDocumentAccessLevel.Manager;
        }

        return TenantDocumentAccessLevel.None;
    }
}
