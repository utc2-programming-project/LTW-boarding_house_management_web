using Microsoft.AspNetCore.Identity;
using Web.Features.Accounts;

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
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Chua cau hinh DevAccounts:Password nen bo qua gieo 4 user mau (role van duoc gieo).");
            return;
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
    }
}
