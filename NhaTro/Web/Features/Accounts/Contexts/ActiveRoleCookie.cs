using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;

namespace Web.Features.Accounts.Contexts;

/// <summary>
/// Cookie ngữ cảnh đang hoạt động. Giá trị được bảo vệ bằng DataProtection,
/// chỉ chứa tên role và luôn được kiểm lại với danh sách ngữ cảnh của tài khoản.
/// </summary>
public sealed class ActiveRoleCookie(IHttpContextAccessor httpContextAccessor, IDataProtectionProvider provider)
{
    public const string CookieName = "nhatro.active_role";
    public const string ItemKey = "nhatro.active_role";

    private readonly IDataProtector _protector = provider.CreateProtector("NhaTro.ActiveRole.v1");

    public string? Get(HttpContext? context)
    {
        context ??= httpContextAccessor.HttpContext;
        if (context is null)
        {
            return null;
        }

        if (context.Items.TryGetValue(ItemKey, out var item) && item is string current)
        {
            return current;
        }

        if (!context.Request.Cookies.TryGetValue(CookieName, out var protectedValue)
            || string.IsNullOrWhiteSpace(protectedValue))
        {
            return null;
        }

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(protectedValue);
            return Encoding.UTF8.GetString(_protector.Unprotect(bytes));
        }
        catch
        {
            return null;
        }
    }

    public void Set(HttpContext? context, string role)
    {
        context ??= httpContextAccessor.HttpContext;
        if (context is null || string.IsNullOrWhiteSpace(role))
        {
            return;
        }

        // Đọc lại trong cùng request (RefreshSignInAsync) phải thấy giá trị mới.
        context.Items[ItemKey] = role;

        var protectedBytes = _protector.Protect(Encoding.UTF8.GetBytes(role));
        context.Response.Cookies.Append(
            CookieName,
            WebEncoders.Base64UrlEncode(protectedBytes),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/",
            });
    }

    public void Clear(HttpContext? context)
    {
        context ??= httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        context.Items.Remove(ItemKey);
        context.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
    }
}
