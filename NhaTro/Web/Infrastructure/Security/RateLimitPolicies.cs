using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
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
    public static string ClientKey(HttpContext http) =>
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    /// <summary>Chỉ POST tới trang đăng nhập mới bị giới hạn (chống dò mật khẩu).</summary>
    public static bool IsLoginRequest(HttpContext http) =>
        HttpMethods.IsPost(http.Request.Method)
        && (http.Request.Path.Equals("/login", StringComparison.OrdinalIgnoreCase)
            || http.Request.Path.Equals("/Account/Login", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Phản hồi 429 an toàn: JSON theo cấu trúc storage cho <c>/api</c>, còn lại là thông báo
    /// đã bản địa hoá. Không lộ chi tiết nội bộ.
    /// </summary>
    public static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.CacheControl = "no-store";

        if (context.HttpContext.Request.Path.StartsWithSegments("/api"))
        {
            await response.WriteAsJsonAsync(
                UploadResult.Failure(
                    StorageErrorCodes.UploadFailed,
                    "Bạn thao tác quá nhanh, vui lòng thử lại sau."),
                cancellationToken);
            return;
        }

        var localizer = context.HttpContext.RequestServices.GetService<IStringLocalizer<Shared>>();
        var message = localizer?["TooManyRequests"].Value ?? "Bạn thao tác quá nhanh, vui lòng thử lại sau.";
        await response.WriteAsync(message, cancellationToken);
    }
}
