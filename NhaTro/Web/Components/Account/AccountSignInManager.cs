using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.Owners;

namespace Web.Components.Account;

/// <summary>
/// VHT-95: chan dang nhap tai khoan Chờ duyệt (ApprovedAt == null).
/// Dat o <see cref="SignInManager{TUser}.CanSignInAsync"/> de ap cho moi luong dang nhap
/// (mat khau, 2FA, external), khong chi rieng trang Login.
/// </summary>
public sealed class AccountSignInManager : SignInManager<ApplicationUser>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly OwnerAccessGuard _ownerAccess;

    public AccountSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation,
        OwnerAccessGuard ownerAccess)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        _userManager = userManager;
        _ownerAccess = ownerAccess;
    }

    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        if (!await base.CanSignInAsync(user))
        {
            return false;
        }

        // Tài khoản trong thùng rác không được đăng nhập.
        if (user.IsDeleted)
        {
            return false;
        }

        // PLAN-022: tài khoản chưa xác thực Gmail được đăng nhập nhưng chỉ vào trang OTP
        // (middleware + claim pending_verification giới hạn route).
        if (!user.EmailConfirmed)
        {
            return true;
        }

        if (user.ApprovedAt is null)
        {
            return false;
        }

        // PLAN-025: Quản lý/khách thuê chỉ đăng nhập nếu còn ít nhất một ngữ cảnh
        // không thuộc chủ trọ đang bị khoá.
        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains(Roles.Admin) || roles.Contains(Roles.ChuTro))
        {
            return true;
        }

        if (roles.Contains(Roles.QuanLy) || roles.Contains(Roles.KhachThue))
        {
            return await _ownerAccess.HasAnyAccessibleContextAsync(user.Id, roles);
        }

        return true;
    }
}
