using System.Security.Claims;
using Web.Features.Accounts;

namespace Web.Features.Maintenance;

public static class IncidentAccess
{
    public const string Policy = "Maintenance";
    public static bool IsAllowed(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        && !user.IsInRole(Roles.Admin)
        && (user.IsInRole(Roles.ChuTro) || user.IsInRole(Roles.QuanLy) || user.IsInRole(Roles.KhachThue))
        && !string.IsNullOrWhiteSpace(user.FindFirstValue(ClaimTypes.NameIdentifier));
}
