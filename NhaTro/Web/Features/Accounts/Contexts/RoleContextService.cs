using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts.Owners;

namespace Web.Features.Accounts.Contexts;

/// <summary>
/// Liệt kê ngữ cảnh một tài khoản được phép hoạt động. Ngoài role thật,
/// hồ sơ khách thuê (Tenant.UserId) cũng mở ngữ cảnh Khách thuê để chủ trọ/quản lý
/// đi thuê nơi khác mà không phải cấp thêm role.
/// </summary>
public sealed class RoleContextService(
    UserManager<ApplicationUser> users,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    OwnerAccessGuard? ownerAccess = null)
{
    private static readonly string[] Priority =
        [Roles.Admin, Roles.ChuTro, Roles.QuanLy, Roles.KhachThue];

    public async Task<IReadOnlyList<RoleContext>> GetAvailableContextsAsync(
        string userId, CancellationToken ct = default)
    {
        var user = await users.FindByIdAsync(userId);
        if (user is null || user.IsDeleted)
        {
            return [];
        }

        var roles = await users.GetRolesAsync(user);
        var available = new HashSet<string>(roles, StringComparer.Ordinal);
        var fromTenantProfile = false;

        if (!available.Contains(Roles.KhachThue))
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            fromTenantProfile = await db.Tenants.AsNoTracking()
                .AnyAsync(tenant => tenant.UserId == userId && !tenant.IsDeleted, ct);
            if (fromTenantProfile)
            {
                available.Add(Roles.KhachThue);
            }
        }

        if (ownerAccess is not null)
        {
            foreach (var role in new[] { Roles.QuanLy, Roles.KhachThue })
            {
                if (available.Contains(role)
                    && await ownerAccess.IsRoleBlockedAsync(userId, role, ct))
                {
                    available.Remove(role);
                }
            }
        }

        return Priority
            .Where(available.Contains)
            .Select(role => new RoleContext(role, fromTenantProfile && role == Roles.KhachThue))
            .ToArray();
    }

    public async Task<IReadOnlyList<string>> GetAvailableRolesAsync(
        string userId, CancellationToken ct = default)
    {
        var contexts = await GetAvailableContextsAsync(userId, ct);
        return contexts.Select(context => context.Role).ToArray();
    }
}
