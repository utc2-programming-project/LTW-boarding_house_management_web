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

        // VHT-119: dùng chung thứ tự vai trò với P3/storage (Chủ trọ thắng khi dữ liệu lệch).
        var role = Roles.ResolvePrimaryRole(principal);

        if (string.IsNullOrEmpty(role))
        {
            throw new UnauthorizedAccessException("A supported P2 role is required.");
        }

        return new PropertyCaller(userId, role);
    }
}
