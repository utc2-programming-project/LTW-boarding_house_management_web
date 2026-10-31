using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Web.Data;

namespace Web.Components.Account;

/// <summary>
/// VHT-95: chan dang nhap tai khoan Chờ duyệt (ApprovedAt == null).
/// Dat o <see cref="SignInManager{TUser}.CanSignInAsync"/> de ap cho moi luong dang nhap
/// (mat khau, 2FA, external), khong chi rieng trang Login.
/// </summary>
public sealed class AccountSignInManager : SignInManager<ApplicationUser>
{
    public AccountSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
    }

    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        if (!await base.CanSignInAsync(user))
        {
            return false;
        }

        return user.ApprovedAt is not null;
    }
}
