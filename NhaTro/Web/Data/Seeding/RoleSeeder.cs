using Microsoft.AspNetCore.Identity;
using Web.Features.Accounts;

namespace Web.Data.Seeding;

// Gieo 4 vai tro VHT-6 (3a). Chi tao role neu chua co, khong xoa/sua role co san.
public static class RoleSeeder
{
    public static async Task EnsureAsync(IServiceProvider services)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Khong tao duoc role '{role}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }
        }
    }
}
