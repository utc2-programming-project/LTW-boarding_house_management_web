using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Features.Accounts;
using Web.Features.Properties.Entities;

namespace Web.Data.Seeding;

// Tao 4 user mau cho team test (VHT-6 3a). Chi chay o Development.
// Mat khau KHONG hardcode: doc tu User Secrets / bien moi truong.
//   dotnet user-secrets set "DevAccounts:Password" "<mat-khau-manh>" --project NhaTro/Web
// Thieu mat khau thi bo qua (chi gieo role), ghi log canh bao.
public static class DevAccountSeeder
{
    // Cong khai de bang "dang nhap nhanh" o trang Login (chi Development) hien dung
    // danh sach tai khoan mau ma khong phai chep lai.
    public static readonly (string Email, string Role)[] Accounts =
    [
        ("admin@nhatro.local", Roles.Admin),
        ("chutro@nhatro.local", Roles.ChuTro),
        ("quanly@nhatro.local", Roles.QuanLy),
        ("khachthue@nhatro.local", Roles.KhachThue),
    ];

    public static async Task EnsureAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var password = config["DevAccounts:Password"];
        var env = services.GetRequiredService<Microsoft.Extensions.Hosting.IHostEnvironment>();
        if (string.IsNullOrWhiteSpace(password))
        {
            if (env.IsDevelopment())
            {
                logger.LogWarning("DevAccounts:Password not configured; creating development users with a temporary password. Change via user-secrets or environment variables.");
                password = "DevAccounts!234";
            }
            else
            {
                logger.LogWarning("Chua cau hinh DevAccounts:Password nen bo qua gieo 4 user mau (role van duoc gieo).");
                return;
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        // Neu da co Admin that (tu AdminSeed) thi khong tao them admin mau.
        var existingAdmins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        var skipDevAdmin = existingAdmins.Count != 0;

        foreach (var (email, role) in Accounts)
        {
            if (skipDevAdmin && role == Roles.Admin)
            {
                continue;
            }

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                // VHT-95: tai khoan mau luon duoc duyet de dang nhap duoc.
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    ApprovedAt = DateTime.UtcNow,
                };
                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    logger.LogWarning("Khong tao duoc user mau {Email}: {Errors}", email,
                        string.Join(", ", created.Errors.Select(e => e.Description)));
                    continue;
                }
            }
            else if (!await userManager.CheckPasswordAsync(user, password))
            {
                // The login page shows the configured development password. Keep existing
                // sample users in sync too, otherwise the displayed password can be stale.
                var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
                var reset = await userManager.ResetPasswordAsync(user, resetToken, password);
                if (!reset.Succeeded)
                {
                    logger.LogWarning("Khong dong bo duoc mat khau user mau {Email}: {Errors}", email,
                        string.Join(", ", reset.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            // Clear lockout from previous local testing so the sample accounts remain usable.
            await userManager.ResetAccessFailedCountAsync(user);
            await userManager.SetLockoutEndDateAsync(user, null);

            // VHT-95: tai khoan mau cu (tao truoc migration) cung phai duoc duyet.
            if (user.ApprovedAt is null)
            {
                user.ApprovedAt = DateTime.UtcNow;
                await userManager.UpdateAsync(user);
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        if (env.IsDevelopment())
        {
            await EnsureManagerAssignmentAsync(services, logger);
        }
    }

    /// <summary>
    /// VHT-24: nối sẵn Quản lý mẫu vào nhà trọ đầu tiên của Chủ trọ mẫu để test luồng
    /// "Quản lý thấy Chủ trọ / thấy phòng". Chỉ thêm khi chưa có phân công nào.
    /// </summary>
    private static async Task EnsureManagerAssignmentAsync(IServiceProvider services, ILogger logger)
    {
        var dbFactory = services.GetService<IDbContextFactory<ApplicationDbContext>>();
        if (dbFactory is null)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.PropertyManagers.AnyAsync(assignment => assignment.RevokedAt == null))
        {
            return;
        }

        var ownerEmail = Accounts.First(account => account.Role == Roles.ChuTro).Email;
        var managerEmail = Accounts.First(account => account.Role == Roles.QuanLy).Email;

        var owner = await db.Users.FirstOrDefaultAsync(user => user.Email == ownerEmail);
        var manager = await db.Users.FirstOrDefaultAsync(user => user.Email == managerEmail);
        if (owner is null || manager is null)
        {
            return;
        }

        var property = await db.Properties
            .Where(item => item.OwnerId == owner.Id && !item.IsDeleted)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();
        if (property is null)
        {
            return;
        }

        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = property.Id,
            ManagerUserId = manager.Id,
            AssignedAt = DateTime.UtcNow,
            AssignedByUserId = owner.Id,
        });
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Da noi Quan ly mau {ManagerEmail} vao nha tro {PropertyName} cua {OwnerEmail}.",
            managerEmail, property.Name, ownerEmail);
    }
}
