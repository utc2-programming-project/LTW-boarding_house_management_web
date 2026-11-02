using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;

namespace Web.Features.Tenants.Documents;

/// <summary>
/// VHT-119: Quản lý thấy giấy tờ của hồ sơ có hợp đồng tại tòa mình được phân công còn hiệu lực.
/// Dùng chung chuỗi truy vấn với <see cref="TenantVisibility"/> để phạm vi không lệch giữa P3 và P2.
/// Kho hồ sơ chưa gắn hợp đồng không mở giấy tờ (tránh lộ ảnh CCCD chéo chủ).
/// </summary>
public sealed class ContractManagerTenantScope(ApplicationDbContext db) : IManagerTenantScope
{
    public Task<bool> IsTenantInScopeAsync(
        string managerUserId,
        int tenantId,
        CancellationToken cancellationToken = default) =>
        TenantVisibility.ContractLinkedTenantIds(db, managerUserId, Roles.QuanLy)
            .AnyAsync(id => id == tenantId, cancellationToken);
}
