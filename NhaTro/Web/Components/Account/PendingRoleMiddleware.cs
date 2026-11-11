namespace Web.Components.Account;

/// <summary>
/// Tài khoản có nhiều ngữ cảnh nhưng chưa chọn phải dừng ở /select-role.
/// </summary>
public sealed class PendingRoleMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && context.User.HasClaim(NhaTroClaimsPrincipalFactory.PendingRoleClaim, "1")
            && !IsAllowed(context.Request.Path)
            && !StaticAssetRequest.IsStaticAsset(context.Request)
            && HttpMethods.IsGet(context.Request.Method))
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect(
                $"/select-role?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        await next(context);
    }

    // Chỉ liệt kê trang được phép; tài nguyên tĩnh (css/js/font/_content/_framework...)
    // do StaticAssetRequest lo nên không chuyển hướng.
    private static bool IsAllowed(PathString path) =>
        path.StartsWithSegments("/select-role")
        || path.StartsWithSegments("/Account/Logout")
        || path.StartsWithSegments("/Account/Login")
        || path.StartsWithSegments("/login")
        || path.StartsWithSegments("/culture")
        || path.StartsWithSegments("/health");
}
