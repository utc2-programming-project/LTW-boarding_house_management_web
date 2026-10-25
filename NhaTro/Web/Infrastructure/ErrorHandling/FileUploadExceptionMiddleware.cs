using System.Runtime.ExceptionServices;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Rules;

namespace Web.Infrastructure.ErrorHandling;

/// <summary>
/// Chuẩn hoá lỗi khi tầng server chặn request upload quá lớn (VHT-12 mục 2): Kestrel trả
/// <see cref="BadHttpRequestException"/> 413 và multipart trả <see cref="InvalidDataException"/>;
/// cả hai được map về cùng lỗi JSON <c>FileSizeExceeded</c> với HTTP 413 như khi service phát hiện.
/// </summary>
public sealed class FileUploadExceptionMiddleware(RequestDelegate next, ILogger<FileUploadExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            await WriteTooLargeAsync(context, ex);
        }
        catch (InvalidDataException ex) when (IsFileUploadRequest(context))
        {
            await WriteTooLargeAsync(context, ex);
        }
    }

    private static bool IsFileUploadRequest(HttpContext context) =>
        (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method))
        && (context.Request.Path.StartsWithSegments("/api/files")
            || context.Request.Path.StartsWithSegments("/api/tenants"));

    private async Task WriteTooLargeAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
        {
            // Không thể ghi lại body; để exception đi tiếp cho tầng xử lý lỗi chung.
            ExceptionDispatchInfo.Capture(ex).Throw();
            return;
        }

        logger.LogWarning(ex, "Request upload vượt giới hạn ở tầng server.");

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
        await context.Response.WriteAsJsonAsync(TooLargeResult());
    }

    private static UploadResult TooLargeResult() =>
        UploadResult.Failure(
            StorageErrorCodes.FileSizeExceeded,
            $"File vượt quá dung lượng cho phép (tối đa {FileTypeRules.DescribeMegabytes(FileTypeRules.MaxDefaultBytes())} MB)");
}
