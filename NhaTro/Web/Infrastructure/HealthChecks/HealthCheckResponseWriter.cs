using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Web.Infrastructure.HealthChecks;

/// <summary>
/// Chỉ trả trạng thái tổng quát cho <c>/health/live</c> và <c>/health/ready</c>.
/// Không lộ tên/chi tiết từng health check, exception hay connection string.
/// </summary>
public static class HealthCheckResponseWriter
{
    public static Task WriteMinimalAsync(HttpContext context, HealthReport report)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(report.Status.ToString());
    }
}
