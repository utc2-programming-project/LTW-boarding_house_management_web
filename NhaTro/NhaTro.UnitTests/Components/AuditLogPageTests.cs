using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Pages;
using Web.Features.Audit.Services;
using Web.Features.Properties.Entities;
using Web.Infrastructure.Auditing;

namespace NhaTro.UnitTests.Components;

public sealed class AuditLogPageTests : BunitContext
{
    private const string OwnerA = "owner-a";
    private readonly string _auditRoot = Path.Combine(
        Path.GetTempPath(), "NhaTro_AuditPageTests", Guid.NewGuid().ToString("N"));

    public AuditLogPageTests()
    {
        Services.AddLogging();
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
    }

    private void Configure(string role, string userId)
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Properties.Add(new Property { Id = 1, Name = "Nhà A", OwnerId = OwnerA });
        db.Users.Add(new ApplicationUser { Id = OwnerA, UserName = OwnerA, FullName = "Chủ trọ A" });
        db.SaveChanges();

        var store = new AuditLogFileStore(_auditRoot);
        store.AppendMany(
        [
            new(Guid.NewGuid(), AuditLogCategory.Account, "OwnerLocked", OwnerA, null, null, "locked", DateTimeOffset.UtcNow),
            new(Guid.NewGuid(), AuditLogCategory.Business, "ContractRenewed", OwnerA, 1, OwnerA, null, DateTimeOffset.UtcNow),
        ]);

        var inner = new ServiceCollection();
        inner.AddSingleton(db);
        inner.AddSingleton(store);
        inner.AddScoped<AuditLogService>();
        var provider = inner.BuildServiceProvider();

        Services.AddSingleton(new AuditLogPageService(new Authentication(role, userId),
            provider.GetRequiredService<IServiceScopeFactory>()));
    }

    private IRenderedComponent<AuditLogView> RenderPage(string role, string userId)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "test"));

        return Render<AuditLogView>(parameters => parameters
            .AddCascadingValue(Task.FromResult(new AuthenticationState(principal))));
    }

    [Fact]
    public void Admin_chi_thay_so_tai_khoan_va_khong_co_cot_toa_nha()
    {
        Configure(Roles.Admin, "admin-1");

        var page = RenderPage(Roles.Admin, "admin-1");

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".table-card tbody tr")));
        Assert.Contains("OwnerLocked", page.Markup);
        Assert.DoesNotContain("ContractRenewed", page.Markup);
        Assert.DoesNotContain("Nhà A", page.Find(".table-card thead").TextContent);
    }

    [Fact]
    public void QuanLy_thay_thong_bao_khong_co_quyen()
    {
        Configure(Roles.QuanLy, "manager-1");

        var page = RenderPage(Roles.QuanLy, "manager-1");

        page.WaitForAssertion(() => Assert.Single(page.FindAll(".form-alert")));
        Assert.Empty(page.FindAll(".table-card"));
    }

    private sealed class Authentication(string role, string userId) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "test"))));
    }
}
