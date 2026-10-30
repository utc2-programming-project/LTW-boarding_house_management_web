using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants.Entities;

namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>
/// VHT-14 (đợt 1A): dữ liệu nền dùng chung cho integration/HTTP test.
/// Tạo đủ 4 vai trò, 2 nhà trọ thuộc 2 chủ trọ khác nhau và dữ liệu tiền mẫu P6.
/// Chỉ chạy trên database test riêng do <see cref="SqlServerFixture"/> tạo; không chạm DB demo.
/// </summary>
public static class TestDataSeeder
{
    public const string Password = "TestPass123!";

    public const string AdminEmail = "admin@test.local";
    public const string ChuTroAEmail = "chutro.a@test.local";
    public const string ChuTroBEmail = "chutro.b@test.local";
    public const string QuanLyEmail = "quanly@test.local";
    public const string KhachThueEmail = "khachthue@test.local";

    public const string PropertyAName = "Nhà trọ A";
    public const string PropertyBName = "Nhà trọ B";
    public const string RoomACode = "A101";
    public const string RoomBCode = "B201";

    // Dữ liệu tiền mẫu P6 (VHT-9/VHT-34 sẽ chốt lại khi có DTO thật).
    public const decimal RoomAStandardPrice = 3_000_000m;
    public const decimal RoomBStandardPrice = 4_000_000m;

    public sealed record SeedIds(
        string AdminId,
        string ChuTroAId,
        string ChuTroBId,
        string QuanLyId,
        string KhachThueId,
        int PropertyAId,
        int PropertyBId,
        int RoomAId,
        int RoomBId,
        int TenantAId);

    /// <summary>
    /// Gieo dữ liệu nền. Có thể gọi sau <see cref="SqlServerFixture.ResetAsync"/> hoặc sau khi
    /// WebApplicationFactory đã chạy RoleSeeder.
    /// </summary>
    public static async Task<SeedIds> SeedAsync(ApplicationDbContext db)
    {
        var roleIds = await EnsureRolesAsync(db);

        var adminId = await EnsureUserAsync(db, roleIds, AdminEmail, Roles.Admin);
        var chuTroAId = await EnsureUserAsync(db, roleIds, ChuTroAEmail, Roles.ChuTro);
        var chuTroBId = await EnsureUserAsync(db, roleIds, ChuTroBEmail, Roles.ChuTro);
        var quanLyId = await EnsureUserAsync(db, roleIds, QuanLyEmail, Roles.QuanLy);
        var khachThueId = await EnsureUserAsync(db, roleIds, KhachThueEmail, Roles.KhachThue);

        var propertyA = await db.Properties.FirstOrDefaultAsync(p => p.Name == PropertyAName);
        if (propertyA is null)
        {
            propertyA = new Property
            {
                Name = PropertyAName,
                Address = "Số 1, đường A",
                OwnerId = chuTroAId,
                IsActive = true,
            };
            db.Properties.Add(propertyA);
            await db.SaveChangesAsync();
        }

        var propertyB = await db.Properties.FirstOrDefaultAsync(p => p.Name == PropertyBName);
        if (propertyB is null)
        {
            propertyB = new Property
            {
                Name = PropertyBName,
                Address = "Số 2, đường B",
                OwnerId = chuTroBId,
                IsActive = true,
            };
            db.Properties.Add(propertyB);
            await db.SaveChangesAsync();
        }

        // VHT-24: Quản lý mẫu được phân công tòa A để các test công tắc P2 giữ đúng hành vi cũ.
        var assignment = await db.PropertyManagers.FirstOrDefaultAsync(
            a => a.PropertyId == propertyA.Id && a.ManagerUserId == quanLyId && a.RevokedAt == null);
        if (assignment is null)
        {
            db.PropertyManagers.Add(new PropertyManager
            {
                PropertyId = propertyA.Id,
                ManagerUserId = quanLyId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = chuTroAId,
            });
            await db.SaveChangesAsync();
        }

        var roomA = await db.Rooms.FirstOrDefaultAsync(r => r.PropertyId == propertyA.Id && r.Code == RoomACode);
        if (roomA is null)
        {
            roomA = new Room
            {
                PropertyId = propertyA.Id,
                Code = RoomACode,
                Floor = 1,
                AreaM2 = 20m,
                StandardPrice = RoomAStandardPrice,
                Status = RoomStatus.Available,
                IsActive = true,
            };
            db.Rooms.Add(roomA);
            await db.SaveChangesAsync();
        }

        var roomB = await db.Rooms.FirstOrDefaultAsync(r => r.PropertyId == propertyB.Id && r.Code == RoomBCode);
        if (roomB is null)
        {
            roomB = new Room
            {
                PropertyId = propertyB.Id,
                Code = RoomBCode,
                Floor = 2,
                AreaM2 = 25m,
                StandardPrice = RoomBStandardPrice,
                Status = RoomStatus.Available,
                IsActive = true,
            };
            db.Rooms.Add(roomB);
            await db.SaveChangesAsync();
        }

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.CitizenId == "079201000001");
        if (tenant is null)
        {
            tenant = new Tenant
            {
                FullName = "Khách thuê A",
                Phone = "0901000001",
                Email = KhachThueEmail,
                CitizenId = "079201000001",
                PermanentAddress = "Số 1, đường A",
                UserId = khachThueId,
                IsActive = true,
            };
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync();
        }

        return new SeedIds(
            adminId,
            chuTroAId,
            chuTroBId,
            quanLyId,
            khachThueId,
            propertyA.Id,
            propertyB.Id,
            roomA.Id,
            roomB.Id,
            tenant.Id);
    }

    private static async Task<Dictionary<string, string>> EnsureRolesAsync(ApplicationDbContext db)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var roleName in Roles.All)
        {
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
            if (role is null)
            {
                role = new IdentityRole(roleName)
                {
                    NormalizedName = roleName.ToUpperInvariant(),
                };
                db.Roles.Add(role);
                await db.SaveChangesAsync();
            }

            result[roleName] = role.Id;
        }

        return result;
    }

    private static async Task<string> EnsureUserAsync(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, string> roleIds,
        string email,
        string roleName)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                // VHT-95: tai khoan nen cua test da duoc duyet.
                ApprovedAt = DateTime.UtcNow,
            };
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, Password);
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        else if (user.ApprovedAt is null)
        {
            user.ApprovedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var roleId = roleIds[roleName];
        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == roleId))
        {
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });
            await db.SaveChangesAsync();
        }

        return user.Id;
    }
}
