using System.Security.Claims;

namespace Web.Components.Account;

/// <summary>
/// Tài khoản chưa xác thực Gmail chỉ được vào trang OTP, đăng xuất và tài nguyên tĩnh.
/// Mọi route khác bị chuyển về /Account/VerifyOtp kèm returnUrl.
/// </summary>
public sealed class PendingVerificationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(NhaTroClaimsPrincipalFactory.PendingVerificationClaim, "1")
            && !IsAllowed(context.Request.Path)
            && !StaticAssetRequest.IsStaticAsset(context.Request)
            && HttpMethods.IsGet(context.Request.Method))
        {
            var email = context.User.FindFirstValue(ClaimTypes.Email)
                ?? context.User.Identity.Name
                ?? string.Empty;
            var returnUrl = context.Request.Path + context.Request.QueryString;
            var target = $"/Account/VerifyOtp?purpose=FirstLogin&email={Uri.EscapeDataString(email)}"
                + $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            context.Response.Redirect(target);
            return;
        }

        await next(context);
    }

    // Chỉ liệt kê trang được phép; tài nguyên tĩnh (css/js/font/_content/_framework...)
    // do StaticAssetRequest lo nên không chuyển hướng.
    private static bool IsAllowed(PathString path) =>
        path.StartsWithSegments("/Account/VerifyOtp")
        || path.StartsWithSegments("/Account/Logout")
        || path.StartsWithSegments("/Account/Login")
        || path.StartsWithSegments("/login")
        || path.StartsWithSegments("/culture")
        || path.StartsWithSegments("/health");
}
