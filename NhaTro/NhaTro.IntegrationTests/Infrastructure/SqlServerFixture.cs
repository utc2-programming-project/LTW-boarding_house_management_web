using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Data;

namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>
/// SQL Server LocalDB thật cho integration test.
/// Tech-stack quy định: dùng SQL Server database riêng, KHÔNG dùng EF InMemory (không chứng minh
/// được rowversion/filtered index) và KHÔNG Docker/Testcontainers.
/// Mỗi lần chạy tạo một database riêng, áp migration, xóa khi xong.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string LocalDbMaster =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;Connect Timeout=15";

    private readonly string _databaseName = $"NhaTro_It_{Guid.NewGuid():N}";

    /// <summary>False khi máy/CI không có SQL Server LocalDB: mọi test đánh dấu Skip.</summary>
    public static bool Available { get; } = Probe();

    public string ConnectionString =>
        $@"Server=(localdb)\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;Connect Timeout=15";

    private static bool Probe()
    {
        try
        {
            using var connection = new SqlConnection(LocalDbMaster);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task InitializeAsync()
    {
        if (!Available)
        {
            return;
        }

        // Tạo schema theo model hiện tại. Dùng EnsureCreated thay Migrate vì DbContext "trần"
        // (không qua AddIdentityCore) thiếu phần Identity SchemaVersion=Version3 (bảng passkey),
        // khiến EF 9 báo PendingModelChangesWarning khi Migrate. Migration thật vẫn được kiểm
        // bằng `dotnet ef migrations has-pending-model-changes`; ở đây chỉ cần đúng schema Tenant.
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (!Available)
        {
            return;
        }

        try
        {
            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(LocalDbMaster);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                $"IF DB_ID('{_databaseName}') IS NOT NULL " +
                $"ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"IF DB_ID('{_databaseName}') IS NOT NULL DROP DATABASE [{_databaseName}];";
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Dọn dẹp best-effort: database có thể chưa được tạo nếu InitializeAsync lỗi.
        }
    }

    public ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options);

    /// <summary>Xóa dữ liệu giữa các test, theo thứ tự khóa ngoại.</summary>
    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Tenants.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.UserRoles.ExecuteDeleteAsync();
        await db.RoleClaims.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Roles.ExecuteDeleteAsync();
    }
}

/// <summary>
/// [Fact] chỉ chạy khi có SQL Server LocalDB; nếu không thì Skip để CI không đỏ oan.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!SqlServerFixture.Available)
        {
            Skip = "Không có SQL Server LocalDB trên máy này (bỏ qua integration test).";
        }
    }
}
