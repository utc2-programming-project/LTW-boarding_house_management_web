using Microsoft.AspNetCore.Antiforgery;
using Web.Features.Accounts;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;

namespace Web.Endpoints.Tenants;

/// <summary>
/// Endpoint giấy tờ P3 (VHT-26): danh sách metadata, upload, tải, thay và xóa mềm.
/// Mọi thao tác đi qua <see cref="ITenantDocumentService"/> để kiểm phạm vi hồ sơ và ghi audit.
/// Policy "Business" chặn Admin ngay từ endpoint; service kiểm lại lần hai.
/// </summary>
public static class TenantDocumentEndpoints
{
    public static IEndpointRouteBuilder MapTenantDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/tenants/{tenantId:int}/documents")
            .RequireAuthorization("Business");

        group.MapGet("/", ListAsync);
        group.MapGet("/{fileId:guid}", DownloadAsync);
        group.MapPost("/", UploadAsync).DisableAntiforgery();
        group.MapPut("/{fileId:guid}", ReplaceAsync).DisableAntiforgery();
        group.MapDelete("/{fileId:guid}", DeleteAsync).DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        int tenantId,
        HttpContext http,
        ITenantDocumentService service,
        CancellationToken cancellationToken)
    {
        var list = await service.ListAsync(tenantId, CallerOf(http), cancellationToken);
        return list is null ? FileNotFound() : Results.Json(list);
    }

    private static async Task<IResult> UploadAsync(
        int tenantId,
        HttpContext http,
        ITenantDocumentService service,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return InvalidRequest();
        }

        var request = await ReadUploadRequestAsync(http, tenantId, cancellationToken);
        if (request is null)
        {
            return InvalidRequest();
        }

        var result = await service.UploadAsync(tenantId, request, CallerOf(http), cancellationToken);
        return result.Succeeded ? Results.Json(result) : ToFailure(result);
    }

    private static async Task<IResult> ReplaceAsync(
        int tenantId,
        Guid fileId,
        HttpContext http,
        ITenantDocumentService service,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return InvalidRequest();
        }

        var request = await ReadUploadRequestAsync(http, tenantId, cancellationToken);
        if (request is null)
        {
            return InvalidRequest();
        }

        var result = await service.ReplaceAsync(tenantId, fileId, request, CallerOf(http), cancellationToken);
        return result.Succeeded ? Results.Json(result) : ToFailure(result);
    }

    private static async Task<IResult> DownloadAsync(
        int tenantId,
        Guid fileId,
        HttpContext http,
        ITenantDocumentService service,
        CancellationToken cancellationToken)
    {
        var response = await service.DownloadAsync(tenantId, fileId, CallerOf(http), cancellationToken);
        if (response is null)
        {
            return FileNotFound();
        }

        http.Response.Headers["X-Content-Type-Options"] = "nosniff";

        return Results.Stream(
            response.FileStream,
            response.ContentType,
            response.FileName,
            enableRangeProcessing: true);
    }

    private static async Task<IResult> DeleteAsync(
        int tenantId,
        Guid fileId,
        HttpContext http,
        ITenantDocumentService service,
        IAntiforgery antiforgery,
        CancellationToken cancellationToken)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return InvalidRequest();
        }

        var deleted = await service.DeleteAsync(tenantId, fileId, CallerOf(http), cancellationToken);
        return deleted ? Results.NoContent() : FileNotFound();
    }

    private static async Task<UploadFileRequest?> ReadUploadRequestAsync(
        HttpContext http,
        int tenantId,
        CancellationToken cancellationToken)
    {
        if (!http.Request.HasFormContentType)
        {
            return null;
        }

        // InvalidDataException do vượt MultipartBodyLengthLimit được
        // FileUploadExceptionMiddleware map về 413 FileSizeExceeded thống nhất.
        var form = await http.Request.ReadFormAsync(cancellationToken);

        var file = form.Files["file"] ?? form.Files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        return new UploadFileRequest
        {
            File = file,
            // Endpoint chuyên P3: client không được chọn loại/đối tượng khác để vượt phạm vi.
            FileCategory = FileCategory.IdentityDocument,
            RelatedEntityType = RelatedEntityType.Tenant,
            RelatedEntityId = tenantId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            DocumentType = form["documentType"].ToString(),
            Description = form["description"].ToString(),
        };
    }

    private static TenantCaller CallerOf(HttpContext http) =>
        new(http.User.UserId() ?? string.Empty, RoleOf(http.User));

    private static string RoleOf(System.Security.Principal.IPrincipal user) =>
        Web.Features.Accounts.Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;

    private static IResult ToFailure(UploadResult result) => result.ErrorCode switch
    {
        StorageErrorCodes.FileSizeExceeded => Results.Json(result, statusCode: StatusCodes.Status413PayloadTooLarge),
        StorageErrorCodes.Forbidden => Results.Json(result, statusCode: StatusCodes.Status403Forbidden),
        StorageErrorCodes.Unauthorized => Results.Json(result, statusCode: StatusCodes.Status401Unauthorized),
        StorageErrorCodes.FileNotFound => Results.Json(result, statusCode: StatusCodes.Status404NotFound),
        StorageErrorCodes.DuplicateDocumentType => Results.Json(result, statusCode: StatusCodes.Status409Conflict),
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

    private static IResult FileNotFound() =>
        Results.Json(
            UploadResult.Failure(StorageErrorCodes.FileNotFound, "File không tồn tại hoặc đã bị xóa"),
            statusCode: StatusCodes.Status404NotFound);

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
}
