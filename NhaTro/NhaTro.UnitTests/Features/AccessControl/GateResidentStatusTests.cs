using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Web.Data;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.AccessControl.Services;
using Web.Features.Accounts;

namespace NhaTro.UnitTests.Features.AccessControl;

/// <summary>Trạng thái trong/ngoài của khách thuê lấy từ lượt GateLog thành công mới nhất.</summary>
public sealed class GateResidentStatusTests
{
    private const string TenantUserId = "tenant-1";
    private static readonly DateTimeOffset BaseTime = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private readonly DbContextOptions<ApplicationDbContext> _options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

    [Fact]
    public async Task ResidentStatusAsync_chua_co_luot_tra_ve_chua_co()
    {
        var status = await Service().ResidentStatusAsync();

        Assert.False(status.Inside);
        Assert.Null(status.LastAt);
        Assert.Null(status.LastDirection);
    }

    [Fact]
    public async Task ResidentStatusAsync_luot_vao_gan_nhat_thi_dang_trong_nha()
    {
        await SeedAsync(
            Log(GateDirection.Out, BaseTime.AddHours(-2)),
            Log(GateDirection.In, BaseTime.AddHours(-1)),
            Log(GateDirection.Out, BaseTime.AddMinutes(-30), success: false));

        var status = await Service().ResidentStatusAsync();

        Assert.True(status.Inside);
        Assert.Equal(GateDirection.In, status.LastDirection);
        Assert.Equal(BaseTime.AddHours(-1), status.LastAt);
    }

    [Fact]
    public async Task ResidentStatusAsync_luot_ra_gan_nhat_thi_da_ra_ngoai_va_bo_qua_nguoi_khac()
    {
        await SeedAsync(
            Log(GateDirection.In, BaseTime.AddHours(-2)),
            Log(GateDirection.Out, BaseTime.AddHours(-1)),
            Log(GateDirection.In, BaseTime.AddMinutes(-10), user: "tenant-2"));

        var status = await Service().ResidentStatusAsync();

        Assert.False(status.Inside);
        Assert.Equal(GateDirection.Out, status.LastDirection);
        Assert.Equal(BaseTime.AddHours(-1), status.LastAt);
    }

    private async Task SeedAsync(params GateLog[] logs)
    {
        await using var db = new ApplicationDbContext(_options);
        db.GateLogs.AddRange(logs);
        await db.SaveChangesAsync();
    }

    private static GateLog Log(GateDirection direction, DateTimeOffset at, bool success = true, string user = TenantUserId) => new()
    {
        PropertyId = 1,
        Direction = direction,
        Method = GateMethod.Qr,
        IsSuccess = success,
        UserId = user,
        OccurredAt = at,
    };

    private GatePageService Service() => new(
        new FakeAuth(TenantUserId),
        null!,
        null!,
        null!,
        null!,
        new TestDbContextFactory(_options),
        null!,
        null!,
        null!,
        null!,
        new ConfigurationBuilder().Build(),
        TimeProvider.System);

    private sealed class FakeAuth : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _user;

        public FakeAuth(string userId) => _user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, Roles.KhachThue)], "test"));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_user));
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
