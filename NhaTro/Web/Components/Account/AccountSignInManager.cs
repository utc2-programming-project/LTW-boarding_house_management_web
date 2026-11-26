using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.Owners;
using Web.Features.Accounts.Security;
using Web.Features.Accounts.Sessions;

namespace Web.Components.Account;

/// <summary>
/// VHT-95: chan dang nhap tai khoan Chờ duyệt (ApprovedAt == null).
/// Dat o <see cref="SignInManager{TUser}.CanSignInAsync"/> de ap cho moi luong dang nhap
/// (mat khau, 2FA, external), khong chi rieng trang Login.
/// PLAN-001 #10/#11: mỗi lần đăng nhập tạo phiên (cookie nhatro.sid) và cảnh báo thiết bị mới.
/// </summary>
public sealed class AccountSignInManager : SignInManager<ApplicationUser>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly OwnerAccessGuard _ownerAccess;
    private readonly IHttpContextAccessor _contextAccessor;
    private readonly UserSessionStore _sessions;
    private readonly SecurityNotifier _notifier;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SignInManager<ApplicationUser>> _log;

    public AccountSignInManager(
        UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation,
        OwnerAccessGuard ownerAccess,
        UserSessionStore sessions,
        SecurityNotifier notifier,
        IServiceScopeFactory scopeFactory)
        : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        _userManager = userManager;
        _ownerAccess = ownerAccess;
        _contextAccessor = contextAccessor;
        _sessions = sessions;
        _notifier = notifier;
        _scopeFactory = scopeFactory;
        _log = logger;
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

    public override async Task SignInWithClaimsAsync(
        ApplicationUser user,
        AuthenticationProperties? authenticationProperties,
        IEnumerable<Claim> additionalClaims)
    {
        await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
        await StartSessionAsync(user);
    }

    public override async Task SignOutAsync()
    {
        var http = _contextAccessor.HttpContext;
        var sessionId = http?.Request.Cookies[UserSessionCookies.SessionId];
        var userId = http?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(sessionId) && !string.IsNullOrWhiteSpace(userId))
        {
            try
            {
                await _sessions.RevokeAsync(userId, sessionId);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Không thu hồi được phiên {SessionId} khi đăng xuất.", sessionId);
            }
        }

        http?.Response.Cookies.Delete(UserSessionCookies.SessionId);
        await base.SignOutAsync();
    }

    private async Task StartSessionAsync(ApplicationUser user)
    {
        var http = _contextAccessor.HttpContext;
        if (http is null)
        {
            return;
        }

        try
        {
            var ip = http.Connection.RemoteIpAddress?.ToString();
            var userAgent = http.Request.Headers.UserAgent.ToString();
            var existing = http.Request.Cookies[UserSessionCookies.SessionId];

            var (sessionId, isNewDevice) = await _sessions.CreateOrReuseAsync(
                user.Id, existing, ip, userAgent);

            http.Response.Cookies.Append(UserSessionCookies.SessionId, sessionId, new CookieOptions
            {
                HttpOnly = true,
                Secure = http.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(30),
            });

            if (!isNewDevice)
            {
                return;
            }

            await _notifier.NotifyNewLoginInAppAsync(user.Id, ip, userAgent);
            QueueNewLoginEmail(user.Id, ip, userAgent);
        }
        catch (Exception ex)
        {
            // Phiên là tiện ích; lỗi ghi phiên không được chặn đăng nhập.
            _log.LogError(ex, "Không tạo được phiên đăng nhập cho {UserId}.", user.Id);
        }
    }

    private void QueueNewLoginEmail(string userId, string? ip, string? userAgent)
    {
        var factory = _scopeFactory;
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = factory.CreateAsyncScope();
                var notifier = scope.ServiceProvider.GetRequiredService<SecurityNotifier>();
                await notifier.SendNewLoginEmailAsync(userId, ip, userAgent);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Không gửi được email đăng nhập mới cho {UserId}.", userId);
            }
        });
    }
}
