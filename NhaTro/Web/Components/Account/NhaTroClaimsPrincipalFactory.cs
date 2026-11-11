using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.Contexts;

namespace Web.Components.Account;

/// <summary>
/// Phát đúng một role claim cho ngữ cảnh đang chọn, kèm danh sách ngữ cảnh khả dụng.
/// Nhờ vậy mọi policy/service dùng IsInRole vẫn giữ nguyên mà không trộn phạm vi.
/// </summary>
public sealed class NhaTroClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> options,
    RoleContextService contexts,
    ActiveRoleCookie activeRole,
    IHttpContextAccessor httpContextAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, options)
{
    public const string PendingVerificationClaim = "nhatro.pending_verification";
    public const string PendingApprovalClaim = "nhatro.pending_approval";
    public const string PendingRoleClaim = "nhatro.pending_role";
    public const string AvailableRolesClaim = "nhatro.available_roles";

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Bỏ role claim mặc định của Identity; chỉ phát role của ngữ cảnh đang chọn.
        foreach (var claim in identity.FindAll(identity.RoleClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }

        var available = await contexts.GetAvailableRolesAsync(user.Id);
        var requested = activeRole.Get(httpContextAccessor.HttpContext);
        var active = !string.IsNullOrWhiteSpace(requested)
                     && available.Contains(requested, StringComparer.Ordinal)
            ? requested
            : available.Count == 1
                ? available[0]
                : null;

        if (active is not null)
        {
            identity.AddClaim(new Claim(identity.RoleClaimType, active));
            identity.AddClaim(new Claim(Roles.ActiveRoleClaim, active));
        }

        identity.AddClaim(new Claim(AvailableRolesClaim, string.Join(",", available)));
        if (active is null)
        {
            identity.AddClaim(new Claim(PendingRoleClaim, "1"));
        }

        if (!user.EmailConfirmed)
        {
            identity.AddClaim(new Claim(PendingVerificationClaim, "1"));
        }

        if (user.ApprovedAt is null)
        {
            identity.AddClaim(new Claim(PendingApprovalClaim, "1"));
        }

        return identity;
    }
}
