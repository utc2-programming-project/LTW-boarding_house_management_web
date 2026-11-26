using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Web.Features.Accounts.External;

/// <summary>
/// PLAN-012: dòng liên kết đăng nhập bên ngoài trong tab Bảo mật. Provider chỉ hiện khi server
/// cấu hình scheme, hoặc khi tài khoản còn liên kết — để người dùng còn gỡ được sau khi rút cấu hình.
/// </summary>
public sealed record ExternalLoginRow(bool Visible, bool Linked, int LinkedCount, string? ProviderLabel)
{
    public static ExternalLoginRow Resolve(
        IReadOnlyList<AuthenticationScheme> schemes,
        IReadOnlyList<UserLoginInfo> logins)
    {
        if (schemes.Count == 0 && logins.Count == 0)
        {
            return new(false, false, 0, null);
        }

        // Một provider thì gọi thẳng tên nó; nhiều provider thì dùng nhãn chung của khu vực.
        string? label = schemes.Count switch
        {
            1 => schemes[0].DisplayName ?? schemes[0].Name,
            0 when logins.Count == 1 => logins[0].ProviderDisplayName,
            _ => null,
        };

        return new(true, logins.Count > 0, logins.Count, label);
    }
}
