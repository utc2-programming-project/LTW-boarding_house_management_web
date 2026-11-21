using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Accounts;

namespace Web.Components.Account;

/// <summary>
/// Production: Admin/Chủ trọ phải bật 2FA trước khi dùng giao diện nghiệp vụ.
/// Development/Testing bỏ qua để seed/demo và integration test không bị chặn.
/// </summary>
public sealed class RequireTwoFactorMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> users,
        IOptions<SecurityOptions> options,
        IHostEnvironment environment)
    {
        if (!options.Value.RequireTwoFactorForPrivileged
            || environment.IsDevelopment()
            || environment.IsEnvironment("Testing"))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true
            || StaticAssetRequest.IsStaticAsset(context.Request)
            || IsAllowed(context.Request.Path))
        {
            await next(context);
            return;
        }

        var role = context.User.FindFirst(Roles.ActiveRoleClaim)?.Value;
        if (role is not (Roles.Admin or Roles.ChuTro))
        {
            await next(context);
            return;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is not null && await users.GetTwoFactorEnabledAsync(user))
        {
            await next(context);
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method))
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect(
                $"/Account/Manage/EnableAuthenticator?required=1&returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
    }

    private static bool IsAllowed(PathString path) =>
        path.StartsWithSegments("/Account/Manage/EnableAuthenticator")
        || path.StartsWithSegments("/Account/Manage/TwoFactorAuthentication")
        || path.StartsWithSegments("/Account/Manage/GenerateRecoveryCodes")
        || path.StartsWithSegments("/Account/Logout")
        || path.StartsWithSegments("/Account/Login")
        || path.StartsWithSegments("/login")
        || path.StartsWithSegments("/select-role")
        || path.StartsWithSegments("/culture")
        || path.StartsWithSegments("/health");
}
