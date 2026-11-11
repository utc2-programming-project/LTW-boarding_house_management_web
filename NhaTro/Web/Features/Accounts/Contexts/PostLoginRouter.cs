using Web.Data;

namespace Web.Features.Accounts.Contexts;

/// <summary>
/// Sau khi đăng nhập: nếu chỉ có một ngữ cảnh thì vào thẳng; nhiều ngữ cảnh thì
/// dừng ở /select-role để tránh trộn phạm vi chủ trọ/quản lý/khách thuê.
/// </summary>
public sealed class PostLoginRouter(
    RoleContextService contexts,
    ActiveRoleCookie activeRole,
    IHttpContextAccessor httpContextAccessor)
{
    public async Task<string> GetRedirectAsync(
        ApplicationUser user, string? returnUrl, CancellationToken ct = default)
    {
        var roles = await contexts.GetAvailableRolesAsync(user.Id, ct);
        var target = SafeReturnUrl(returnUrl);

        if (roles.Count == 1)
        {
            activeRole.Set(httpContextAccessor.HttpContext, roles[0]);
            return target;
        }

        return $"/select-role?returnUrl={Uri.EscapeDataString(target)}";
    }

    public static string SafeReturnUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "/";
        }

        if (value.StartsWith("//", StringComparison.Ordinal)
            || Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            return "/";
        }

        return value.StartsWith('/') ? value : "/" + value;
    }
}
