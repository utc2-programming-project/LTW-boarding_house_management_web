using Microsoft.AspNetCore.Antiforgery;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;
using Web.Features.Storage.Services;
using Web.Infrastructure.Storage;

namespace Web.Endpoints.Files;

/// <summary>
/// Endpoint tệp dùng chung (VHT-12 mục 8): upload, tải, liệt kê và xóa mềm — mọi thao tác đều
/// kiểm quyền trong <see cref="FileService"/> và chặn Admin ở policy "Business".
/// </summary>
public static class FileEndpoints
{
    public static IEndpointRouteBuilder MapFileEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/files")
            .RequireAuthorization("Business");

        // Tự xác thực antiforgery trong handler để endpoint không phụ thuộc metadata form của framework.
        group.MapPost("/", UploadAsync).DisableAntiforgery();
        group.MapGet("/{id:guid}", DownloadAsync);
        group.MapGet("/", ListAsync);
        group.MapDelete("/{id:guid}", DeleteAsync).DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> UploadAsync(
        HttpContext http,
        FileService service,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return InvalidRequest();
        }

        if (!http.Request.HasFormContentType)
        {
            return InvalidRequest();
        }

        IFormCollection form;
        try
        {
            form = await http.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            // Vượt MultipartBodyLengthLimit: cùng một lỗi 413 với kiểm tra trong service.
            return TooLarge(FileTypeRulesMaxBytes());
        }

        var file = form.Files["file"] ?? form.Files.FirstOrDefault();
        if (file is null || !TryParseCategory(form["fileCategory"], out var category))
        {
            return InvalidRequest();
        }

        if (!TryParseEntityType(form["relatedEntityType"], out var entityType))
        {
            return InvalidRequest();
        }

        var request = new UploadFileRequest
        {
            File = file,
            FileCategory = category,
            RelatedEntityId = form["relatedEntityId"].ToString(),
            RelatedEntityType = entityType,
            Description = form["description"].ToString(),
        };

        var result = await service.UploadAsync(request, http.User, cancellationToken);
        return result.Succeeded ? Results.Json(result) : ToFailure(result);
    }

    private static async Task<IResult> DownloadAsync(
        HttpContext http,
        Guid id,
        FileService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await service.DownloadAsync(id, http.User, cancellationToken);
            if (response is null)
            {
                return FileNotFound();
            }

            // Results.Stream tự dọn luồng sau khi trả xong.
            return Results.Stream(
                response.FileStream,
                response.ContentType,
                response.FileName,
                enableRangeProcessing: true);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbidden();
        }
        catch (StoragePathViolationException)
        {
            return FileNotFound();
        }
    }

    private static async Task<IResult> ListAsync(
        HttpContext http,
        FileService service,
        string? relatedEntityType,
        string? relatedEntityId,
        CancellationToken cancellationToken)
    {
        // Tham số nullable để thiếu tham số trả cùng lỗi InvalidRequest, không ném exception của framework.
        if (!TryParseEntityType(relatedEntityType, out var entityType) || string.IsNullOrWhiteSpace(relatedEntityId))
        {
            return InvalidRequest();
        }

        try
        {
            var files = await service.ListAsync(entityType, relatedEntityId.Trim(), http.User, cancellationToken);
            return Results.Json(files);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbidden();
        }
    }

    private static async Task<IResult> DeleteAsync(
        HttpContext http,
        Guid id,
        FileService service,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return InvalidRequest();
        }

        try
        {
            var deleted = await service.DeleteAsync(id, http.User, cancellationToken);
            return deleted ? Results.NoContent() : FileNotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbidden();
        }
        catch (StoragePathViolationException)
        {
            return FileNotFound();
        }
    }

    private static long FileTypeRulesMaxBytes() =>
        FileTypeRules.MaxDefaultBytes();

    private static IResult ToFailure(UploadResult result) => result.ErrorCode switch
    {
        StorageErrorCodes.FileSizeExceeded => Results.Json(result, statusCode: StatusCodes.Status413PayloadTooLarge),
        StorageErrorCodes.Forbidden => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
        StorageErrorCodes.Unauthorized => Results.Json(result, statusCode: StatusCodes.Status401Unauthorized),
        StorageErrorCodes.InvalidFileType
            or StorageErrorCodes.InvalidRequest
            or StorageErrorCodes.EmptyFile
            or StorageErrorCodes.RelatedEntityNotFound
            or StorageErrorCodes.FileCountExceeded => Results.Json(result, statusCode: StatusCodes.Status400BadRequest),
        _ => Results.Json(result, statusCode: StatusCodes.Status500InternalServerError),
    };

    private static IResult InvalidRequest() =>
        Results.Json(
            UploadResult.Failure(StorageErrorCodes.InvalidRequest, "Dữ liệu gửi lên không hợp lệ."),
            statusCode: StatusCodes.Status400BadRequest);

    private static IResult Forbidden() =>
        Results.Json(
            UploadResult.Failure(StorageErrorCodes.Forbidden, "Bạn không có quyền thực hiện thao tác này"),
            statusCode: StatusCodes.Status403Forbidden);

    private static IResult FileNotFound() =>
        Results.Json(
            UploadResult.Failure(StorageErrorCodes.FileNotFound, "File không tồn tại hoặc đã bị xóa"),
            statusCode: StatusCodes.Status404NotFound);

    private static IResult TooLarge(long maxBytes) =>
        Results.Json(
            UploadResult.Failure(
                StorageErrorCodes.FileSizeExceeded,
                $"File vượt quá dung lượng cho phép (tối đa {FileTypeRules.DescribeMegabytes(maxBytes)} MB)"),
            statusCode: StatusCodes.Status413PayloadTooLarge);

    private static async Task<bool> IsAntiforgeryValidAsync(HttpContext http, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(http);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static bool TryParseCategory(string? value, out FileCategory category) =>
        Enum.TryParse(value, ignoreCase: true, out category) && Enum.IsDefined(category);

    private static bool TryParseEntityType(string? value, out RelatedEntityType entityType) =>
        Enum.TryParse(value, ignoreCase: true, out entityType) && Enum.IsDefined(entityType);
}
