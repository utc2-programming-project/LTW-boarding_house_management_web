using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Web.Features.Accounts.Avatar;
using Web.Infrastructure.Security;

namespace Web.Endpoints.Avatars;

/// <summary>
/// Ảnh đại diện cá nhân: chỉ đọc/nạp/gỡ ảnh của chính tài khoản đang đăng nhập.
/// đường theo <c>/{userId}</c> để giao diện render được nhiều avatar mà không lộ ảnh người khác.
/// </summary>
public static class AvatarEndpoints
{
    public static IEndpointRouteBuilder MapAvatarEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/avatar")
            .RequireAuthorization()
            .RequireRateLimiting(RateLimitPolicies.FileUpload);

        group.MapGet("/{userId}", GetAsync);
        // Ảnh đi theo JSON (data URL đã nén ở trình duyệt) nên antiforgery xác thực tay như /api/files.
        group.MapPost("/{userId}", SaveAsync).DisableAntiforgery();
        group.MapDelete("/{userId}", DeleteAsync).DisableAntiforgery();

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        HttpContext http, AvatarService avatars, string userId, CancellationToken cancellationToken)
    {
        if (!IsSelf(http, userId))
        {
            // Cùng 404 như "chưa có ảnh" để không cho dò xem user_id nào tồn tại.
            return Results.NotFound();
        }

        var avatar = await avatars.FindAsync(userId, cancellationToken);
        if (avatar is null)
        {
            return Results.NotFound();
        }

        // Loại nội dung do server nhận diện, không do client gửi.
        http.Response.Headers.XContentTypeOptions = "nosniff";
        http.Response.Headers.CacheControl = "private, no-store";

        return Results.Bytes(avatar.Data, avatar.ContentType);
    }

    private static async Task<IResult> SaveAsync(
        HttpContext http,
        AvatarService avatars,
        IAntiforgery antiforgery,
        string userId,
        [FromBody] AvatarUploadRequest? request,
        CancellationToken cancellationToken)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return Results.BadRequest();
        }

        if (!IsSelf(http, userId))
        {
            return Results.Forbid();
        }

        var outcome = TryDecode(request?.Image, out var data);
        if (outcome == AvatarPayloadOutcome.TooLarge)
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        if (outcome == AvatarPayloadOutcome.Invalid || data is null)
        {
            return Results.BadRequest();
        }

        var error = await avatars.SaveAsync(userId, data, cancellationToken);
        return error switch
        {
            null => Results.Json(new { ok = true }),
            AvatarError.TooLarge => Results.StatusCode(StatusCodes.Status413PayloadTooLarge),
            AvatarError.InvalidType => Results.BadRequest(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError),
        };
    }

    private static async Task<IResult> DeleteAsync(
        HttpContext http, AvatarService avatars, IAntiforgery antiforgery, string userId,
        CancellationToken cancellationToken)
    {
        if (!await IsAntiforgeryValidAsync(http, antiforgery))
        {
            return Results.BadRequest();
        }

        if (!IsSelf(http, userId))
        {
            return Results.Forbid();
        }

        await avatars.RemoveAsync(userId, cancellationToken);
        return Results.Ok();
    }

    private static bool IsSelf(HttpContext http, string userId) =>
        http.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } callerId
        && string.Equals(callerId, userId, StringComparison.Ordinal);

    /// <summary>
    /// Giải mã data URL đã base64. Chuỗi vượt trần ký tự thì chặn trước khi giải mã,
    /// tránh nạp cả megabyte vào bộ nhớ chỉ để báo quá cỡ.
    /// </summary>
    public static AvatarPayloadOutcome TryDecode(string? dataUrl, out byte[]? data)
    {
        data = null;

        if (string.IsNullOrWhiteSpace(dataUrl)
            || !dataUrl.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            return AvatarPayloadOutcome.Invalid;
        }

        var comma = dataUrl.IndexOf(',');
        if (comma < 0 || !dataUrl.AsSpan(0, comma).Contains("base64", StringComparison.OrdinalIgnoreCase))
        {
            return AvatarPayloadOutcome.Invalid;
        }

        if (dataUrl.Length - comma - 1 > MaxEncodedLength)
        {
            return AvatarPayloadOutcome.TooLarge;
        }

        try
        {
            data = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            return AvatarPayloadOutcome.Ok;
        }
        catch (FormatException)
        {
            return AvatarPayloadOutcome.Invalid;
        }
    }

    // 4 ký tự base64 cho 3 byte, chẵn nhóm để không cắt giữa ký tự.
    private static readonly int MaxEncodedLength =
        AvatarService.MaxBytes / 3 * 4 + 4;

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

public enum AvatarPayloadOutcome
{
    Ok,
    Invalid,
    TooLarge,
}

public sealed record AvatarUploadRequest(string? Image);
