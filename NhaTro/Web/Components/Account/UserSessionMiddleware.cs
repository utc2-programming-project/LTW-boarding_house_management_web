using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Web.Features.Accounts.Sessions;

namespace Web.Components.Account;

/// <summary>
/// PLAN-001 #10: phiên đăng nhập đã bị thu hồi (đổi mật khẩu hộ, force logout, "đăng xuất thiết bị")
/// thì request tiếp theo bị đăng xuất ngay, không đợi SecurityStampValidator 2 phút.
/// </summary>
public sealed class UserSessionMiddleware(RequestDelegate next)
{
    private static readonly ConcurrentDictionary<string, long> LastTouch = new(StringComparer.Ordinal);
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(5);

    public async Task InvokeAsync(
        HttpContext context,
        UserSessionStore sessions,
        ILogger<UserSessionMiddleware> logger)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || StaticAssetRequest.IsStaticAsset(context.Request))
        {
            await next(context);
            return;
        }

        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var sessionId = context.Request.Cookies[UserSessionCookies.SessionId];
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId))
        {
            await next(context);
            return;
        }

        if (!await sessions.IsActiveAsync(userId, sessionId))
        {
            logger.LogInformation("Phiên {SessionId} của {UserId} đã bị thu hồi; đăng xuất.", sessionId, userId);
            await context.SignOutAsync(IdentityConstants.ApplicationScheme);
            context.Response.Cookies.Delete(UserSessionCookies.SessionId);

            if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
            {
                context.Response.Redirect("/login?stale=1");
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (ShouldTouch(sessionId))
        {
            await sessions.TouchAsync(userId, sessionId);
        }

        await next(context);
    }

    private static bool ShouldTouch(string sessionId)
    {
        var now = Environment.TickCount64;
        var last = LastTouch.GetOrAdd(sessionId, now);
        if (now - last < (long)TouchInterval.TotalMilliseconds)
        {
            return false;
        }

        LastTouch[sessionId] = now;
        return true;
    }
}
