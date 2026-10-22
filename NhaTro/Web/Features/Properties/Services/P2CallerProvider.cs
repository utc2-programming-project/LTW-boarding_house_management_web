using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;
using Web.Features.Accounts;

namespace Web.Features.Properties.Services;

/// <summary>Builds the trusted P2 caller context from the authenticated server-side principal.</summary>
public sealed class P2CallerProvider(AuthenticationStateProvider authenticationStateProvider)
{
    public async Task<PropertyCaller> GetCallerAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        var principal = state.User;
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("Authentication is required for P2 operations.");
        }

        // Prefer the more restrictive role if an account has inconsistent multiple roles.
        var role = new[] { Roles.Admin, Roles.KhachThue, Roles.QuanLy, Roles.ChuTro }
            .FirstOrDefault(principal.IsInRole);

        if (role is null)
        {
            throw new UnauthorizedAccessException("A supported P2 role is required.");
        }

        return new PropertyCaller(userId, role);
    }
}
