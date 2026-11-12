namespace Web.Infrastructure.Security;

/// <summary>
/// PLAN-027: header bảo mật nền tảng + chặn POST cross-site khi trình duyệt gửi
/// Origin/Sec-Fetch-Site. Antiforgery vẫn là lớp chính.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next, ILogger<SecurityHeadersMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = context.Request.Path.StartsWithSegments("/invite")
            || context.Request.Path.StartsWithSegments("/Account/ResetPassword")
            ? "no-referrer"
            : "strict-origin-when-cross-origin";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        headers["Content-Security-Policy-Report-Only"] =
            "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
            "style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self' data:; " +
            "connect-src 'self' ws: wss:; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";

        if (IsNoStorePath(context.Request.Path))
        {
            headers["Cache-Control"] = "no-store";
        }

        if (!IsSafeMethod(context.Request.Method) && !IsTrustedSource(context))
        {
            // Không trả 400 rỗng: Chrome/Edge thay 400 không body bằng trang lỗi của trình duyệt.
            // Ghi log lý do rồi nạp lại chính trang đó bằng GET để người dùng thấy giao diện + token mới.
            logger.LogWarning(
                "Chặn {Method} {Path}: nguồn không tin cậy (Sec-Fetch-Site='{FetchSite}', Origin='{Origin}').",
                context.Request.Method,
                context.Request.Path,
                context.Request.Headers["Sec-Fetch-Site"].ToString(),
                context.Request.Headers.Origin.ToString());

            context.Response.StatusCode = StatusCodes.Status303SeeOther;
            context.Response.Headers.Location =
                $"{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
            return;
        }

        await next(context);
    }

    private static bool IsSafeMethod(string method) =>
        HttpMethods.IsGet(method)
        || HttpMethods.IsHead(method)
        || HttpMethods.IsOptions(method)
        || HttpMethods.IsTrace(method);

    private static bool IsTrustedSource(HttpContext context)
    {
        var fetchSite = context.Request.Headers["Sec-Fetch-Site"].ToString();
        if (string.Equals(fetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var origin = context.Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(origin))
        {
            return true;
        }

        // Chromium gửi "Origin: null" cho POST từ trang có Referrer-Policy: no-referrer
        // (/invite, /Account/ResetPassword). Đó là same-origin, không phải cross-site:
        // Sec-Fetch-Site ở trên và antiforgery vẫn là lớp chặn thật.
        if (string.Equals(origin, "null", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // So host thay vì scheme để không chặn nhầm khi IIS/reverse proxy đổi scheme.
        return Uri.TryCreate(origin, UriKind.Absolute, out var originUri)
            && string.Equals(
                originUri.Host,
                context.Request.Host.Host,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNoStorePath(PathString path) =>
        path.StartsWithSegments("/login")
        || path.StartsWithSegments("/Account/Login")
        || path.StartsWithSegments("/Account/VerifyOtp")
        || path.StartsWithSegments("/Account/RegisterOwner")
        || path.StartsWithSegments("/Account/ForgotPassword")
        || path.StartsWithSegments("/Account/ResetPassword")
        || path.StartsWithSegments("/invite")
        || path.StartsWithSegments("/admin");
}
