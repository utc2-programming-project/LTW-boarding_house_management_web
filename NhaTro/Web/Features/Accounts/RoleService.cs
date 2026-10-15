using Microsoft.AspNetCore.Identity;
using Web.Data;

namespace Web.Features.Accounts;

// Doi vai tro: chi Admin, cam tu doi, cam go Admin cuoi cung (VHT-6 3c + D1).
public sealed class RoleService(UserManager<ApplicationUser> users)
{
    public async Task ChangeRoleAsync(string callerUserId, bool callerIsAdmin, string targetUserId, string newRole)
    {
        if (!callerIsAdmin)
        {
            throw new UnauthorizedAccessException("Chi Admin duoc doi vai tro.");
        }

        if (callerUserId == targetUserId)
        {
            throw new UnauthorizedAccessException("Khong duoc tu doi vai tro cua chinh minh.");
        }

        if (!Roles.All.Contains(newRole))
        {
            throw new ArgumentException("Vai tro moi khong hop le.", nameof(newRole));
        }

        var target = await users.FindByIdAsync(targetUserId)
            ?? throw new KeyNotFoundException("Khong tim thay user.");
        var currentRoles = await users.GetRolesAsync(target);
        var admins = await users.GetUsersInRoleAsync(Roles.Admin);

        var reason = RoleChangeGuard.Check(
            callerIsAdmin: true, callerUserId, targetUserId,
            currentRoles.Contains(Roles.Admin), admins.Count == 1, newRole);
        if (reason is not null)
        {
            throw new UnauthorizedAccessException(reason);
        }

        var remove = await users.RemoveFromRolesAsync(target, currentRoles);
        if (!remove.Succeeded)
        {
            throw new InvalidOperationException("Go role cu that bai: " +
                string.Join(", ", remove.Errors.Select(e => e.Description)));
        }

        var add = await users.AddToRoleAsync(target, newRole);
        if (!add.Succeeded)
        {
            throw new InvalidOperationException("Gan role moi that bai: " +
                string.Join(", ", add.Errors.Select(e => e.Description)));
        }
    }
}
