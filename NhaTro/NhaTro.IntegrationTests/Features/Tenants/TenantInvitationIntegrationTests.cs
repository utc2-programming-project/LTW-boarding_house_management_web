using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties.Entities;
using Web.Features.Tenants;
using Web.Features.Tenants.Invitations;
using Web.Infrastructure.Email;

namespace NhaTro.IntegrationTests.Features.Tenants;

/// <summary>
/// PLAN-006/007: mời khách qua email → khách đặt mật khẩu → có hồ sơ thuộc đúng chủ trọ,
/// token chỉ dùng được một lần.
/// </summary>
public sealed class TenantInvitationIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly TenantCaller Owner = new("owner-a", Roles.ChuTro);

    [SqlServerFact]
    public async Task Moi_qua_email_nhan_loi_moi_tao_ho_so_va_chi_dung_mot_lan()
    {
        await fixture.ResetAsync();
        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(db, "manager-a", Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(db, "seed-khach", Roles.KhachThue);
            db.Properties.Add(new Property { Name = "Nhà A", OwnerId = "owner-a" });
            await db.SaveChangesAsync();
            var property = await db.Properties.SingleAsync(p => p.OwnerId == "owner-a");
            db.PropertyManagers.Add(new PropertyManager
            {
                PropertyId = property.Id,
                ManagerUserId = "manager-a",
                AssignedByUserId = "owner-a",
            });
            await db.SaveChangesAsync();
        }

        var mailer = new CapturingOutboundEmailSender();
        var service = NewService(mailer);

        var created = await service.CreateAsync(new CreateTenantInvitationRequest
        {
            FullName = "Khách Mời",
            Email = "khach.moi@test.local",
            PhoneNumber = "0901234567",
        }, Owner, "http://localhost:7260");

        Assert.True(created.Succeeded, created.ErrorMessage);
        Assert.False(string.IsNullOrWhiteSpace(created.RawToken));
        Assert.NotNull(mailer.LastHtml);
        Assert.Contains("/invite/", mailer.LastHtml);

        var token = created.RawToken!;

        // Token sai không dùng được.
        var wrong = await service.AcceptAsync("token-sai", "MatKhau123!");
        Assert.False(wrong.Succeeded);
        Assert.Equal(TenantInvitationErrorCodes.NotFound, wrong.ErrorCode);

        var accepted = await service.AcceptAsync(token, "MatKhau123!");
        Assert.True(accepted.Succeeded, accepted.ErrorMessage);

        await using (var db = fixture.CreateContext())
        {
            var tenant = await db.Tenants.AsNoTracking().SingleAsync(t => t.Email == "khach.moi@test.local");
            Assert.True(await db.TenantOwnerLinks.AsNoTracking()
                .AnyAsync(link => link.TenantId == tenant.Id && link.OwnerId == "owner-a"));
            Assert.NotNull(tenant.UserId);

            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == tenant.UserId);
            Assert.NotNull(user.ApprovedAt);

            var roleName = await (
                from ur in db.UserRoles.AsNoTracking()
                join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                where ur.UserId == tenant.UserId
                select r.Name).SingleAsync();
            Assert.Equal(Roles.KhachThue, roleName);
        }

        // Dùng lại token bị chặn.
        var second = await service.AcceptAsync(token, "MatKhauKhac123!");
        Assert.False(second.Succeeded);
        Assert.Equal(TenantInvitationErrorCodes.AlreadyUsed, second.ErrorCode);
    }

    private TenantInvitationService NewService(IOutboundEmailSender mailer)
    {
        // Cùng cách dựng UserManager như AccountLifecycleTests: cần context sống theo store.
        var db = fixture.CreateContext();
        var identityOptions = new IdentityOptions
        {
            Password =
            {
                RequiredLength = 8,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonAlphanumeric = true,
            },
            User = { RequireUniqueEmail = true },
        };

        var userManager = new UserManager<ApplicationUser>(
            new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(db),
            Options.Create(identityOptions),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);

        return new TenantInvitationService(
            fixture, userManager, mailer, TimeProvider.System, NullLogger<TenantInvitationService>.Instance);
    }

    private sealed class CapturingOutboundEmailSender : IOutboundEmailSender
    {
        public string? LastHtml { get; private set; }

        public Task SendAsync(string toEmail, string subject, string html, CancellationToken ct = default)
        {
            LastHtml = html;
            return Task.CompletedTask;
        }
    }
}
