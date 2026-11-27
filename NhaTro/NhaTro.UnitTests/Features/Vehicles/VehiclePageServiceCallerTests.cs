using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.Owners;
using Web.Features.Vehicles.Services;

namespace NhaTro.UnitTests.Features.Vehicles;

/// <summary>
/// Bug 4: VehiclePageService.CallerAsync phải nạp danh sách chủ trọ bị khoá cho Quản lý/Khách thuê
/// (giống API), để trang Blazor không hiển thị dữ liệu của chủ trọ đang bị khoá.
/// </summary>
public sealed class VehiclePageServiceCallerTests
{
    [Fact]
    public async Task CallerAsync_LoadsBlockedOwnerIds_ForManager()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("vehicle-page-caller-" + Guid.NewGuid())
            .Options;

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Users.Add(new ApplicationUser
            {
                Id = "owner-locked",
                UserName = "locked@test.local",
                Email = "locked@test.local",
                FullName = "Chủ trọ bị khoá",
                LockoutEnd = DateTimeOffset.MaxValue,
                IsDeleted = false,
            });
            seed.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole
            {
                Id = "role-chutro",
                Name = Roles.ChuTro,
                NormalizedName = Roles.ChuTro.ToUpperInvariant(),
            });
            seed.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
            {
                UserId = "owner-locked",
                RoleId = "role-chutro",
            });
            await seed.SaveChangesAsync();
        }

        var ownerAccess = new OwnerAccessGuard(
            new InMemoryFactory(options),
            null!,
            NullLogger<OwnerAccessGuard>.Instance);

        using var page = new VehiclePageService(
            new Authentication(Roles.QuanLy, "manager-1"),
            null!, null!, null!, null!, null!, null!, null!, null!,
            ownerAccess,
            null!);

        var caller = await page.CallerAsync();

        Assert.Equal(Roles.QuanLy, caller.Role);
        Assert.Contains("owner-locked", caller.BlockedOwnerIds);
    }

    [Fact]
    public async Task CallerAsync_DoesNotLoadBlockedOwners_ForOwner()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("vehicle-page-caller-owner-" + Guid.NewGuid())
            .Options;

        await using (var seed = new ApplicationDbContext(options))
        {
            seed.Users.Add(new ApplicationUser
            {
                Id = "owner-locked",
                UserName = "locked@test.local",
                Email = "locked@test.local",
                LockoutEnd = DateTimeOffset.MaxValue,
            });
            seed.Roles.Add(new Microsoft.AspNetCore.Identity.IdentityRole
            {
                Id = "role-chutro",
                Name = Roles.ChuTro,
                NormalizedName = Roles.ChuTro.ToUpperInvariant(),
            });
            seed.UserRoles.Add(new Microsoft.AspNetCore.Identity.IdentityUserRole<string>
            {
                UserId = "owner-locked",
                RoleId = "role-chutro",
            });
            await seed.SaveChangesAsync();
        }

        var ownerAccess = new OwnerAccessGuard(
            new InMemoryFactory(options),
            null!,
            NullLogger<OwnerAccessGuard>.Instance);

        using var page = new VehiclePageService(
            new Authentication(Roles.ChuTro, "owner-1"),
            null!, null!, null!, null!, null!, null!, null!, null!,
            ownerAccess,
            null!);

        var caller = await page.CallerAsync();

        Assert.Empty(caller.BlockedOwnerIds);
    }

    private sealed class Authentication(string role, string userId) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "test"))));
    }

    private sealed class InMemoryFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}