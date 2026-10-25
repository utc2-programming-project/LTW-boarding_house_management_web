using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Web.Infrastructure.HealthChecks;

namespace Web.Endpoints.Health;

/// <summary>
/// Endpoint health dùng chung: ẩn danh, chỉ trả trạng thái, không lộ chi tiết.
/// </summary>
public static class HealthEndpoints
{
    /// <summary>Tag đánh dấu các health check cần cho readiness (kết nối DB).</summary>
    public const string ReadyTag = "ready";

    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Liveness: chỉ xác nhận tiến trình còn sống, không phụ thuộc dịch vụ ngoài.
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthCheckResponseWriter.WriteMinimalAsync,
        }).AllowAnonymous();

        // Readiness: kiểm kết nối DB; chỉ trả Healthy/Unhealthy.
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = HealthCheckResponseWriter.WriteMinimalAsync,
        }).AllowAnonymous();

        return endpoints;
    }
}
