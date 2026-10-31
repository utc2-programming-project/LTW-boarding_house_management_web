using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants.Entities;

namespace NhaTro.IntegrationTests.Features.Accounts;

/// <summary>
/// VHT-95 (2B) — vòng đời tài khoản trên SQL Server thật: tạo theo phạm vi tòa nhà,
/// Chờ duyệt khi Quản lý tạo, duyệt, khóa Admin cuối cùng và lọc danh sách theo phạm vi.
/// </summary>
public sealed class AccountLifecycleTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [SqlServerFact]
    public async Task QuanLy_tao_khach_trong_toa_duoc_giao_thi_cho_duyet_va_gan_ho_so()
    {
        await fixture.ResetAsync();
        var data = await SeedScopeAsync();
        var service = NewAccountService();

        var result = await service.CreateAsync(new CreateAccountRequest
        {
            Email = "khach.moi@test.local",
            FullName = "Khách mới",
            PhoneNumber = "0903333333",
            Password = "TestPass123!",
            Role = Roles.KhachThue,
            TenantId = data.TenantInAId,
        }, data.ManagerId, Roles.QuanLy);

        Assert.True(result.Succeeded, result.ErrorCode);

        await using var db = fixture.CreateContext();
        var created = await db.Users.SingleAsync(u => u.Email == "khach.moi@test.local");
        Assert.Null(created.ApprovedAt);

        var tenant = await db.Tenants.SingleAsync(t => t.Id == data.TenantInAId);
        Assert.Equal(created.Id, tenant.UserId);
    }

    [SqlServerFact]
    public async Task QuanLy_tao_khach_ngoai_toa_duoc_giao_bi_tu_choi()
    {
        await fixture.ResetAsync();
        var data = await SeedScopeAsync();
        var service = NewAccountService();

        var result = await service.CreateAsync(new CreateAccountRequest
        {
            Email = "khach.ngoai@test.local",
            FullName = "Khách ngoài",
            Password = "TestPass123!",
            Role = Roles.KhachThue,
            TenantId = data.TenantInBId,
        }, data.ManagerId, Roles.QuanLy);

        Assert.False(result.Succeeded);
        Assert.Equal(AccountErrorCodes.OutOfScope, result.ErrorCode);

        await using var db = fixture.CreateContext();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "khach.ngoai@test.local"));
        Assert.Null((await db.Tenants.SingleAsync(t => t.Id == data.TenantInBId)).UserId);
    }

    [SqlServerFact]
    public async Task ChuTro_tao_quan_ly_hoat_dong_ngay_va_khong_tao_duoc_Admin()
    {
        await fixture.ResetAsync();
        var data = await SeedScopeAsync();
        var service = NewAccountService();

        var created = await service.CreateAsync(new CreateAccountRequest
        {
            Email = "quanly.moi@test.local",
            FullName = "Quản lý mới",
            Password = "TestPass123!",
            Role = Roles.QuanLy,
        }, data.OwnerAId, Roles.ChuTro);

        Assert.True(created.Succeeded, created.ErrorCode);

        await using var db = fixture.CreateContext();
        var manager = await db.Users.SingleAsync(u => u.Email == "quanly.moi@test.local");
        Assert.NotNull(manager.ApprovedAt);

        var forbidden = await service.CreateAsync(new CreateAccountRequest
        {
            Email = "admin.moi@test.local",
            FullName = "Admin mới",
            Password = "TestPass123!",
            Role = Roles.Admin,
        }, data.OwnerAId, Roles.ChuTro);

        Assert.False(forbidden.Succeeded);
        Assert.Equal(AccountErrorCodes.RoleNotAllowed, forbidden.ErrorCode);
    }

    [SqlServerFact]
    public async Task Danh_sach_chi_hien_tai_khoan_trong_pham_va_khong_lo_vai_tro_khac()
    {
        await fixture.ResetAsync();
        var data = await SeedScopeAsync();
        var service = NewAccountService();

        var asOwnerA = await service.ListAsync(null, null, 1, 50, data.OwnerAId, Roles.ChuTro);
        var ownerAIds = asOwnerA.Items.Select(item => item.Id).ToHashSet();
        Assert.Contains(data.OwnerAId, ownerAIds);
        Assert.Contains(data.ManagerId, ownerAIds);
        Assert.Contains(data.KhachAId, ownerAIds);
        Assert.DoesNotContain(data.ManagerBId, ownerAIds);
        Assert.DoesNotContain(data.KhachBId, ownerAIds);
        Assert.DoesNotContain(data.AdminId, ownerAIds);

        // Bo loc vai tro ngoai pham vi khong được nới rộng kết quả.
        var filterAdmin = await service.ListAsync(null, Roles.Admin, 1, 50, data.OwnerAId, Roles.ChuTro);
        Assert.Empty(filterAdmin.Items);

        var asManager = await service.ListAsync(null, null, 1, 50, data.ManagerId, Roles.QuanLy);
        var managerIds = asManager.Items.Select(item => item.Id).ToHashSet();
        Assert.Equal(2, managerIds.Count);
        Assert.Contains(data.ManagerId, managerIds);
        Assert.Contains(data.KhachAId, managerIds);

        var asAdmin = await service.ListAsync(null, null, 1, 50, data.AdminId, Roles.Admin);
        Assert.Equal(7, asAdmin.TotalCount);
    }

    [SqlServerFact]
    public async Task Duyet_tai_khoan_cho_duyet_theo_pham_vi()
    {
        await fixture.ResetAsync();
        var data = await SeedScopeAsync();
        var service = NewAccountService();

        var first = await service.CreateAsync(new CreateAccountRequest
        {
            Email = "khach.duyet@test.local",
            FullName = "Khách chờ duyệt",
            Password = "TestPass123!",
            Role = Roles.KhachThue,
            TenantId = data.TenantInAId,
        }, data.ManagerId, Roles.QuanLy);
        Assert.True(first.Succeeded, first.ErrorCode);

        await using (var db = fixture.CreateContext())
        {
            var pending = await db.Users.SingleAsync(u => u.Email == "khach.duyet@test.local");
            Assert.Null(pending.ApprovedAt);
        }

        var pendingId = await PendingUserIdAsync("khach.duyet@test.local");

        // Quản lý không được tự duyệt.
        var managerApprove = await service.ApproveAsync(pendingId, data.ManagerId, Roles.QuanLy);
        Assert.Equal(AccountErrorCodes.Forbidden, managerApprove.ErrorCode);

        var approved = await service.ApproveAsync(pendingId, data.OwnerAId, Roles.ChuTro);
        Assert.True(approved.Succeeded, approved.ErrorCode);

        await using var verifyDb = fixture.CreateContext();
        Assert.NotNull((await verifyDb.Users.SingleAsync(u => u.Id == pendingId)).ApprovedAt);
    }

    [SqlServerFact]
    public async Task Khong_khoa_duoc_Admin_cuoi_cung()
    {
        await fixture.ResetAsync();
        var data = await SeedScopeAsync();
        var logger = new CaptureLogger();
        var service = NewAccountService(logger);

        var blocked = await service.LockAsync(data.AdminId, "admin-2", Roles.Admin);
        Assert.Equal(AccountErrorCodes.CannotLockLastAdmin, blocked.ErrorCode);

        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, "admin-2", Roles.Admin);
        }

        var allowed = await service.LockAsync(data.AdminId, "admin-2", Roles.Admin);
        Assert.True(allowed.Succeeded, string.Join(" | ", logger.Messages));

        await using var verifyDb = fixture.CreateContext();
        var lockedAdmin = await verifyDb.Users.SingleAsync(u => u.Id == data.AdminId);
        Assert.NotNull(lockedAdmin.LockoutEnd);
    }

    private async Task<string> PendingUserIdAsync(string email)
    {
        await using var db = fixture.CreateContext();
        return (await db.Users.SingleAsync(u => u.Email == email)).Id;
    }

    private AccountService NewAccountService(ILogger<AccountService>? logger = null)
    {
        // Context song theo UserManager: AddToRoleAsync/GetRolesAsync can role store.
        var db = fixture.CreateContext();
        var identityOptions = new IdentityOptions
        {
            Password =
            {
                RequiredLength = 8,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireNonAlphanumeric = false,
            },
            User = { RequireUniqueEmail = true },
        };

        var userManager = new UserManager<ApplicationUser>(
            new UserStore<ApplicationUser, IdentityRole, ApplicationDbContext>(db),
            Options.Create(identityOptions),
            new PasswordHasher<ApplicationUser>(),
            [new UserValidator<ApplicationUser>()],
            [new PasswordValidator<ApplicationUser>()],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);

        return new AccountService(userManager, fixture, logger ?? NullLogger<AccountService>.Instance);
    }

    /// <summary>
    /// Gieo 7 tai khoan, 2 toa cua 2 chu tro, phan cong QuanLy va ho so khach:
    /// moi toa co 1 ho so da gan tai khoan (de test pham vi danh sach) va 1 ho so chua gan
    /// (de test tao tai khoan). Khong them tai khoan nao khac de tong so Admin thay = 7.
    /// </summary>
    private async Task<ScopeData> SeedScopeAsync()
    {
        await using var db = fixture.CreateContext();

        await SqlServerFixture.SeedAccountAsync(db, "admin-1", Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "owner-b", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "manager-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "manager-2", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-a", Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, "khach-b", Roles.KhachThue);

        var propertyA = new Property { Name = "Nhà A", OwnerId = "owner-a" };
        var propertyB = new Property { Name = "Nhà B", OwnerId = "owner-b" };
        db.Properties.AddRange(propertyA, propertyB);
        await db.SaveChangesAsync();

        db.PropertyManagers.AddRange(
            new PropertyManager
            {
                PropertyId = propertyA.Id,
                ManagerUserId = "manager-1",
                AssignedByUserId = "owner-a",
            },
            new PropertyManager
            {
                PropertyId = propertyB.Id,
                ManagerUserId = "manager-2",
                AssignedByUserId = "owner-b",
            });
        await db.SaveChangesAsync();

        var roomA = new Room
        {
            PropertyId = propertyA.Id,
            Code = "A-101",
            StandardPrice = 1_000_000m,
            Status = RoomStatus.Available,
        };
        var roomB = new Room
        {
            PropertyId = propertyB.Id,
            Code = "B-101",
            StandardPrice = 1_000_000m,
            Status = RoomStatus.Available,
        };
        db.Rooms.AddRange(roomA, roomB);
        await db.SaveChangesAsync();

        var khachA = new Tenant { FullName = "Khách A", Phone = "0901111111", UserId = "khach-a" };
        var khachB = new Tenant { FullName = "Khách B", Phone = "0902222222", UserId = "khach-b" };
        var tenantInA = new Tenant { FullName = "Hồ sơ A chưa gắn", Phone = "0903333333" };
        var tenantInB = new Tenant { FullName = "Hồ sơ B chưa gắn", Phone = "0904444444" };
        db.Tenants.AddRange(khachA, khachB, tenantInA, tenantInB);
        await db.SaveChangesAsync();

        await AddContractAsync(db, khachA.Id, roomA.Id, "HD-A");
        await AddContractAsync(db, tenantInA.Id, roomA.Id, "HD-A-NEW");
        await AddContractAsync(db, khachB.Id, roomB.Id, "HD-B");
        await AddContractAsync(db, tenantInB.Id, roomB.Id, "HD-B-NEW");

        return new ScopeData(
            AdminId: "admin-1",
            OwnerAId: "owner-a",
            OwnerBId: "owner-b",
            ManagerId: "manager-1",
            ManagerBId: "manager-2",
            KhachAId: "khach-a",
            KhachBId: "khach-b",
            TenantInAId: tenantInA.Id,
            TenantInBId: tenantInB.Id,
            PropertyAId: propertyA.Id,
            PropertyBId: propertyB.Id);
    }

    private static async Task AddContractAsync(
        ApplicationDbContext db, int tenantId, int roomId, string contractNumber)
    {
        var contract = new Contract
        {
            ContractNumber = contractNumber,
            RoomId = roomId,
            StartDate = Today.AddDays(-10),
            EndDate = Today.AddDays(20),
            SnapshotPrice = 1_000_000m,
            DepositAmount = 200_000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = tenantId,
        });
        await db.SaveChangesAsync();
    }

    private sealed class CaptureLogger : ILogger<AccountService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception) + (exception is null ? string.Empty : $" | {exception}"));
    }

    private sealed record ScopeData(
        string AdminId,
        string OwnerAId,
        string OwnerBId,
        string ManagerId,
        string ManagerBId,
        string KhachAId,
        string KhachBId,
        int TenantInAId,
        int TenantInBId,
        int PropertyAId,
        int PropertyBId);
}

/// <summary>VHT-95: tài khoản Chờ duyệt không đăng nhập được cho tới khi được duyệt.</summary>
public sealed class PendingAccountLoginTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [WebAppFact]
    public async Task Tai_khoan_cho_duyet_bi_chan_dang_nhap_cho_toi_khi_duyet()
    {
        const string email = "pending.login@test.local";
        const string password = "TestPass123!";

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                ApprovedAt = null,
            };

            var created = await userManager.CreateAsync(user, password);
            Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(e => e.Description)));

            var added = await userManager.AddToRoleAsync(user, Roles.KhachThue);
            Assert.True(added.Succeeded, string.Join(", ", added.Errors.Select(e => e.Description)));
        }

        using (var client = fixture.CreateClient(allowAutoRedirect: false))
        {
            var blocked = await LoginFormClient.PostFreshAsync(client, email, password);
            Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
        }

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            user.ApprovedAt = DateTime.UtcNow;
            var updated = await userManager.UpdateAsync(user);
            Assert.True(updated.Succeeded, string.Join(", ", updated.Errors.Select(e => e.Description)));
        }

        using (var client = fixture.CreateClient(allowAutoRedirect: false))
        {
            var allowed = await LoginFormClient.PostFreshAsync(client, email, password);
            Assert.Equal(HttpStatusCode.Redirect, allowed.StatusCode);
        }
    }
}
