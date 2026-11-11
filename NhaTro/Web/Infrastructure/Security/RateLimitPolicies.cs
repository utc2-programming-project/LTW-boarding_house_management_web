using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using System.Net;
using System.Threading.RateLimiting;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;

namespace Web.Infrastructure.Security;

/// <summary>
/// Tên policy và phản hồi dùng chung cho rate limiting.
/// Global limiter chỉ áp cho POST đăng nhập; <see cref="FileUpload"/> và <see cref="CultureSet"/>
/// áp riêng theo endpoint qua <c>RequireRateLimiting</c>.
/// </summary>
public static class RateLimitPolicies
{
    public const string FileUpload = "file-upload";

    public const string CultureSet = "culture-set";

    /// <summary>Khoá phân vùng theo IP; TestServer không có IP nên dùng khoá chung.</summary>
    public static string ClientKey(HttpContext http)
    {
        // Sau IIS/reverse proxy chạy cùng máy, RemoteIpAddress là loopback; IP thật nằm ở
        // X-Forwarded-For. Chỉ tin header khi peer là loopback để client ngoài không tự gửi giả.
        if (http.Connection.RemoteIpAddress is { } remote && IPAddress.IsLoopback(remote))
        {
            var forwarded = http.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                // Proxy nối IP client vào cuối danh sách; lấy phần tử cuối để không tin giá trị client tự khai.
                var candidate = forwarded
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .LastOrDefault();
                if (IPAddress.TryParse(candidate, out var clientIp))
                {
                    return clientIp.ToString();
                }
            }
        }

        return http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    // Các đường dẫn nhận thông tin đăng nhập: mật khẩu, mã 2FA và mã khôi phục.
    private static readonly string[] LoginPaths =
    [
        "/login",
        "/account/login",
        "/account/loginwith2fa",
        "/account/loginwithrecoverycode",
    ];

    /// <summary>Chỉ POST tới trang đăng nhập mới bị giới hạn (chống dò mật khẩu/mã 2FA).</summary>
    public static bool IsLoginRequest(HttpContext http)
    {
        if (!HttpMethods.IsPost(http.Request.Method))
        {
            return false;
        }

        return LoginPaths.Contains(NormalizePath(http.Request.Path), StringComparer.OrdinalIgnoreCase);
    }

    // PLAN-008: chặn spam đăng ký chủ trọ từ một IP.
    private static readonly string[] RegisterPaths = ["/account/registerowner"];

    public static bool IsRegisterRequest(HttpContext http) =>
        HttpMethods.IsPost(http.Request.Method)
        && RegisterPaths.Contains(NormalizePath(http.Request.Path), StringComparer.OrdinalIgnoreCase);

    // PLAN-022: giới hạn POST xác thực/gửi lại OTP.
    private static readonly string[] OtpPaths = ["/account/verifyotp", "/verify-otp"];

    public static bool IsOtpRequest(HttpContext http) =>
        HttpMethods.IsPost(http.Request.Method)
        && OtpPaths.Contains(NormalizePath(http.Request.Path), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Bỏ '/' cuối để "/Account/Login/" không lách được giới hạn; "/" giữ nguyên.
    /// </summary>
    private static string NormalizePath(PathString path)
    {
        var value = (path.Value ?? string.Empty).TrimEnd('/');
        return value.Length == 0 ? "/" : value;
    }

    /// <summary>
    /// Phản hồi khi vượt giới hạn: JSON theo cấu trúc storage cho <c>/api</c>; POST đăng nhập thì
    /// chuyển về form đăng nhập kèm thông báo đã bản địa hoá; còn lại là 429 text.
    /// Không lộ chi tiết nội bộ.
    /// </summary>
    public static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        var response = http.Response;
        response.Headers.CacheControl = "no-store";

        if (http.Request.Path.StartsWithSegments("/api"))
        {
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            await response.WriteAsJsonAsync(
                UploadResult.Failure(
                    StorageErrorCodes.UploadFailed,
                    "Bạn thao tác quá nhanh, vui lòng thử lại sau."),
                cancellationToken);
            return;
        }

        // POST đăng nhập: trả về form thật kèm thông báo, tránh trình duyệt hiện trang trắng
        // chỉ có một dòng text thô (lỗi UI khi bị giới hạn tần suất).
        if (IsLoginRequest(http))
        {
            var target = "/login?tooManyRequests=1";
            var returnUrl = http.Request.Query["ReturnUrl"].ToString();
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                target += "&ReturnUrl=" + Uri.EscapeDataString(returnUrl);
            }

            response.StatusCode = StatusCodes.Status303SeeOther;
            response.Headers.Location = target;
            return;
        }

        response.StatusCode = StatusCodes.Status429TooManyRequests;
        var localizer = http.RequestServices.GetService<IStringLocalizer<Shared>>();
        var message = localizer?["TooManyRequests"].Value ?? "Bạn thao tác quá nhanh, vui lòng thử lại sau.";
        await response.WriteAsync(message, cancellationToken);
    }
}
