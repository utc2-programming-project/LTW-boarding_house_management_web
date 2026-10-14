using Microsoft.AspNetCore.Identity;
using Web.Features.Accounts;

namespace Web.Data.Seeding;

// Gieo Admin dau tien (VHT-6 3b). Khong hardcode, khong commit secret.
// Luong: co Admin nao trong role Admin thi bo qua; chua co thi doc AdminSeed tu
// cau hinh (User Secrets khi dev, bien moi truong khi prod) de tao.
// Thieu cau hinh thi chi ghi log canh bao, app van chay.
public static class AdminSeeder
{
    public static async Task EnsureAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roleManager.RoleExistsAsync(Roles.Admin))
        {
            var roleResult = await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
            if (!roleResult.Succeeded)
            {
                logger.LogError("Khong tao duoc role Admin: {Errors}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                return;
            }
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var existingAdmins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        if (existingAdmins.Count != 0)
        {
            return;
        }

        var email = config["AdminSeed:Email"];
        var password = config["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Chua cau hinh AdminSeed nen chua co Admin. Cau hinh bang User Secrets (dev) hoac bien moi truong (prod).");
            return;
        }

        var user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogError("Khong tao duoc Admin dau tien: {Errors}",
                string.Join(", ", created.Errors.Select(e => e.Description)));
            return;
        }

        var added = await userManager.AddToRoleAsync(user, Roles.Admin);
        if (!added.Succeeded)
        {
            logger.LogError("Tao user Admin xong nhung gan role that bai: {Errors}",
                string.Join(", ", added.Errors.Select(e => e.Description)));
            return;
        }

        logger.LogInformation("Da tao Admin dau tien {Email}.", email);
    }
}
