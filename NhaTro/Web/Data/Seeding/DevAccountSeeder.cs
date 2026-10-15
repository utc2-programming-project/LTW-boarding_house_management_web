using Microsoft.AspNetCore.Identity;
using Web.Features.Accounts;

namespace Web.Data.Seeding;

// Tao 4 user mau cho team test (VHT-6 3a). Chi chay o Development.
// Mat khau KHONG hardcode: doc tu User Secrets / bien moi truong.
//   dotnet user-secrets set "DevAccounts:Password" "<mat-khau-manh>" --project NhaTro/Web
// Thieu mat khau thi bo qua (chi gieo role), ghi log canh bao.
public static class DevAccountSeeder
{
    private static readonly (string Email, string Role)[] Accounts =
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
                user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    logger.LogWarning("Khong tao duoc user mau {Email}: {Errors}", email,
                        string.Join(", ", created.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}
