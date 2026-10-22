using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Web.Data;

namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>
/// VHT-14: fixture HTTP/auth/UI. Mỗi class test có một database SQL riêng, seed 4 vai trò,
/// 2 nhà trọ của 2 chủ trọ khác nhau và tài khoản test.
/// </summary>
public sealed class WebAppFixture : IAsyncLifetime
{
    private readonly SqlServerFixture _database = new();

    public NhaTroWebApplicationFactory Factory { get; private set; } = null!;

    public TestDataSeeder.SeedIds Ids { get; private set; } = null!;

    public bool Available => SqlServerFixture.Available;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        if (!Available)
        {
            return;
        }

        Factory = new NhaTroWebApplicationFactory(_database.ConnectionString);

        // Factory.Services khởi động app (RoleSeeder/AdminSeeder chạy trên DB test), sau đó gieo dữ liệu nền.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Ids = await TestDataSeeder.SeedAsync(db);
    }

    public HttpClient CreateClient(bool allowAutoRedirect = true) =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = allowAutoRedirect,
            BaseAddress = new Uri("http://localhost"),
        });

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        await _database.DisposeAsync();
    }
}
