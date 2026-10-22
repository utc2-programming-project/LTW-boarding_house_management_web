using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Web.Data;

namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>
/// VHT-14 (đợt 1A): factory HTTP/UI dùng SQL Server test riêng.
/// Thay toàn bộ đăng ký DbContext của app bằng connection string test, không dùng DB demo.
/// </summary>
public sealed class NhaTroWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" để app bỏ qua DevAccountSeeder; role/test account do TestDataSeeder gieo.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Bỏ DbContext/DbContextFactory cũ (đang trỏ DB demo) trước khi đăng ký lại.
            var dbRegistrations = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(ApplicationDbContext)
                    || descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)
                    || descriptor.ServiceType == typeof(DbContextOptions)
                    || descriptor.ServiceType == typeof(IDbContextFactory<ApplicationDbContext>)
                    || descriptor.ServiceType.FullName?.StartsWith(
                        "Microsoft.EntityFrameworkCore.Infrastructure.IDbContextOptionsConfiguration`1",
                        StringComparison.Ordinal) == true)
                .ToList();

            foreach (var descriptor in dbRegistrations)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
            services.AddDbContextFactory<ApplicationDbContext>(
                options => options.UseSqlServer(connectionString),
                ServiceLifetime.Scoped);
        });
    }
}
