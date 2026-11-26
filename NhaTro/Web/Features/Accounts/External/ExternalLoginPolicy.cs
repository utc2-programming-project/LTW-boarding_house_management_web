using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Web.Data;

namespace Web.Features.Accounts.External;

public enum ExternalLoginOutcome
{
    Linked,
    NoAccount,
    EmailNotVerified,
    LinkFailed,
}

public sealed record ExternalLoginResult(ExternalLoginOutcome Outcome, ApplicationUser? User = null);

/// <summary>
/// PLAN-012: luật liên kết provider ngoài của dự án — chỉ liên kết tài khoản đã có,
/// tự liên kết khi email provider đã xác thực và trùng email tài khoản. Không tạo tài khoản mới.
/// </summary>
public sealed class ExternalLoginPolicy(UserManager<ApplicationUser> users, ILogger<ExternalLoginPolicy> log)
{
    public async Task<ExternalLoginResult> ResolveAsync(ExternalLoginInfo info)
    {
        var principal = info.Principal;
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email");
        var verified = string.Equals(
            principal.FindFirstValue("email_verified"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(email) || !verified)
        {
            log.LogWarning("{Provider} login rejected: email missing or not verified.", info.LoginProvider);
            return new(ExternalLoginOutcome.EmailNotVerified);
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            log.LogInformation("{Provider} login for unknown email.", info.LoginProvider);
            return new(ExternalLoginOutcome.NoAccount);
        }

        var link = await users.AddLoginAsync(user, info);
        if (!link.Succeeded)
        {
            log.LogWarning(
                "{Provider} link failed: {Errors}",
                info.LoginProvider,
                string.Join(",", link.Errors.Select(error => error.Code)));
            return new(ExternalLoginOutcome.LinkFailed);
        }

        return new(ExternalLoginOutcome.Linked, user);
    }
}
