// PLAN-012 — chính sách liên kết Google trên SQL Server thật.
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts.External;

namespace NhaTro.IntegrationTests.Features.Accounts;

/// <summary>
/// Chỉ liên kết tài khoản đã có, tự liên kết khi email provider đã xác thực; không tạo tài khoản mới.
/// </summary>
public sealed class ExternalLoginPolicyTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private const string Provider = "Google";

    [SqlServerFact]
    public async Task Email_da_xac_thuc_trung_tai_khoan_thi_tao_lien_ket()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var users = NewUserManager(db);
        var policy = new ExternalLoginPolicy(users, NullLogger<ExternalLoginPolicy>.Instance);
        var user = await CreateUserAsync(users, "owner@test.local");

        var result = await policy.ResolveAsync(Info("owner@test.local", verified: true, key: "google-key-1"));

        Assert.Equal(ExternalLoginOutcome.Linked, result.Outcome);
        Assert.Equal(user.Id, result.User?.Id);
        var logins = await users.GetLoginsAsync(user);
        Assert.Contains(logins, login => login.LoginProvider == Provider && login.ProviderKey == "google-key-1");
    }

    [SqlServerFact]
    public async Task Email_chua_co_tai_khoan_thi_khong_tao_moi()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var users = NewUserManager(db);
        var policy = new ExternalLoginPolicy(users, NullLogger<ExternalLoginPolicy>.Instance);
        var before = db.Users.Count();

        var result = await policy.ResolveAsync(Info("la@test.local", verified: true, key: "google-key-2"));

        Assert.Equal(ExternalLoginOutcome.NoAccount, result.Outcome);
        Assert.Equal(before, db.Users.Count());
    }

    [SqlServerFact]
    public async Task Email_chua_xac_thuc_thi_tu_choi_va_khong_lien_ket()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var users = NewUserManager(db);
        var policy = new ExternalLoginPolicy(users, NullLogger<ExternalLoginPolicy>.Instance);
        var user = await CreateUserAsync(users, "chua-xac-thuc@test.local");

        var result = await policy.ResolveAsync(Info("chua-xac-thuc@test.local", verified: false, key: "google-key-3"));

        Assert.Equal(ExternalLoginOutcome.EmailNotVerified, result.Outcome);
        Assert.Empty(await users.GetLoginsAsync(user));
    }

    [SqlServerFact]
    public async Task Google_key_da_thuoc_tai_khoan_khac_thi_bao_loi_lien_ket()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var users = NewUserManager(db);
        var policy = new ExternalLoginPolicy(users, NullLogger<ExternalLoginPolicy>.Instance);
        await CreateUserAsync(users, "nguoi-a@test.local");
        await CreateUserAsync(users, "nguoi-b@test.local");

        var first = await policy.ResolveAsync(Info("nguoi-a@test.local", verified: true, key: "google-key-4"));
        var second = await policy.ResolveAsync(Info("nguoi-b@test.local", verified: true, key: "google-key-4"));

        Assert.Equal(ExternalLoginOutcome.Linked, first.Outcome);
        Assert.Equal(ExternalLoginOutcome.LinkFailed, second.Outcome);
    }

    private static async Task<ApplicationUser> CreateUserAsync(UserManager<ApplicationUser> users, string email)
    {
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            ApprovedAt = DateTime.UtcNow,
        };
        var created = await users.CreateAsync(user);
        Assert.True(created.Succeeded, string.Join(",", created.Errors.Select(error => error.Code)));
        return user;
    }

    private static ExternalLoginInfo Info(string email, bool verified, string key)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Email, email),
            new Claim("email_verified", verified ? "true" : "false"),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: Provider));
        return new ExternalLoginInfo(principal, Provider, key, Provider);
    }

    private static UserManager<ApplicationUser> NewUserManager(ApplicationDbContext db) =>
        new(
            new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(db),
            Options.Create(new IdentityOptions { User = { RequireUniqueEmail = true } }),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
}
