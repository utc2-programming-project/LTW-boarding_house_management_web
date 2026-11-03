using System.Security.Claims;
using Web.Features.Accounts;
using Web.Features.Maintenance;

namespace NhaTro.UnitTests.Features.Maintenance;

public sealed class IncidentAccessTests
{
    [Theory]
    [InlineData(Roles.ChuTro)]
    [InlineData(Roles.QuanLy)]
    [InlineData(Roles.KhachThue)]
    public void AuthenticatedBusinessRoleCanEnterButAdminMixedRoleCannot(string role)
    {
        var identity = new ClaimsIdentity([new(ClaimTypes.NameIdentifier, "user"), new(ClaimTypes.Role, role)], "test");
        var user = new ClaimsPrincipal(identity);
        Assert.True(IncidentAccess.IsAllowed(user));
        identity.AddClaim(new(ClaimTypes.Role, Roles.Admin));
        Assert.False(IncidentAccess.IsAllowed(user));
    }

    [Fact]
    public void AnonymousAndMissingUserIdCannotEnter()
    {
        Assert.False(IncidentAccess.IsAllowed(new()));
        Assert.False(IncidentAccess.IsAllowed(new(new ClaimsIdentity([new(ClaimTypes.Role, Roles.ChuTro)], "test"))));
    }
}
