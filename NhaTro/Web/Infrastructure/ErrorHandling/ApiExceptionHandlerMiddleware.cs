using Web.Features.Storage;
using Web.Features.Storage.DTOs;

namespace Web.Infrastructure.ErrorHandling;

/// <summary>
/// Lỗi ngoài dự kiến ở endpoint <c>/api</c> trả JSON an toàn (không lộ chi tiết/stack trace)
/// giữ cấu trúc lỗi per-feature của storage. Chỉ áp cho nhánh <c>/api</c> ở môi trường không phải Development.
/// </summary>
public sealed class ApiExceptionHandlerMiddleware(RequestDelegate next, ILogger<ApiExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                // Không ghi lại được body; để tầng xử lý lỗi chung tiếp tục.
                throw;
            }

            logger.LogError(ex, "Lỗi không lường trước ở endpoint API.");

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(
                UploadResult.Failure(StorageErrorCodes.UploadFailed, "Không thể xử lý yêu cầu, vui lòng thử lại."),
                context.RequestAborted);
        }
    }
}
