using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Data;
using Web.Infrastructure.Auditing;

namespace NhaTro.IntegrationTests.Infrastructure;

/// <summary>
/// SQL Server thật cho integration test: dùng server trong NHATRO_TEST_SQLSERVER nếu được đặt,
/// nếu không thì dùng SQL Server LocalDB.
/// Tech-stack quy định: dùng SQL Server database riêng, KHÔNG dùng EF InMemory (không chứng minh
/// được rowversion/filtered index) và KHÔNG Docker/Testcontainers.
/// Mỗi lần chạy tạo một database riêng bằng EnsureCreated, xóa khi xong.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime, IDbContextFactory<ApplicationDbContext>
{
    private const string LocalDbMaster =
        @"Server=(localdb)\MSSQLLocalDB;Database=master;Trusted_Connection=True;Connect Timeout=15";

    private const string TestSqlServerEnvironmentVariable = "NHATRO_TEST_SQLSERVER";

    private readonly string _databaseName = $"NhaTro_It_{Guid.NewGuid():N}";

    /// <summary>VHT-104: mỗi fixture một thư mục audit tạm riêng để test song song không ghi lẫn.</summary>
    public AuditLogFileStore AuditStore { get; } = new(
        Path.Combine(Path.GetTempPath(), "NhaTro_It_Audit", Guid.NewGuid().ToString("N")),
        retentionDays: 3);

    /// <summary>False khi SQL Server được cấu hình hoặc LocalDB mặc định không truy cập được; test sẽ Skip.</summary>
    public static bool Available { get; } = Probe();

    public string ConnectionString => new SqlConnectionStringBuilder(MasterConnectionString)
    {
        InitialCatalog = _databaseName,
        MultipleActiveResultSets = true,
    }.ConnectionString;

    private static string MasterConnectionString
    {
        get
        {
            var configured = Environment.GetEnvironmentVariable(TestSqlServerEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(configured)) return LocalDbMaster;

            var builder = new SqlConnectionStringBuilder(configured)
            {
                InitialCatalog = "master",
            };
            return builder.ConnectionString;
        }
    }

    private static bool Probe()
    {
        try
        {
            using var connection = new SqlConnection(MasterConnectionString);
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
            if (Directory.Exists(AuditStore.RootDirectory))
            {
                Directory.Delete(AuditStore.RootDirectory, recursive: true);
            }

            SqlConnection.ClearAllPools();
            await using var connection = new SqlConnection(MasterConnectionString);
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
            // VHT-104: nhật ký thao tác ghi file, không ghi DB.
            .AddInterceptors(new AuditLogSaveInterceptor(AuditStore, NullLogger<AuditLogSaveInterceptor>.Instance))
            .Options);

    public ApplicationDbContext CreateDbContext() => CreateContext();

    public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(CreateContext());
    }

    /// <summary>Xóa dữ liệu giữa các test, theo thứ tự khóa ngoại.</summary>
    public async Task ResetAsync()
    {
        // VHT-104: log là file; mỗi test bắt đầu với thư mục audit sạch.
        if (Directory.Exists(AuditStore.RootDirectory))
        {
            Directory.Delete(AuditStore.RootDirectory, recursive: true);
        }

        Directory.CreateDirectory(AuditStore.RootDirectory);

        await using var db = CreateContext();
        // StoredFiles co FK toi AspNetUsers: xoa truoc Users, sau Rooms/Properties/Tenants.
        await db.IncidentPhotoAttachments.ExecuteDeleteAsync();
        // VHT-54: audit/proof/payment tham chiếu Payments, Invoices va StoredFiles bang Restrict.
        await db.PaymentAuditLogs.ExecuteDeleteAsync();
        await db.PaymentProofs.ExecuteDeleteAsync();
        await db.Payments.ExecuteDeleteAsync();
        await db.StoredFiles.ExecuteDeleteAsync();
        // VHT-51: xóa snapshot tường minh trước kỳ để test không phụ thuộc cascade.
        await db.UtilityServicePeriodSnapshots.ExecuteDeleteAsync();
        // VHT-51: kỳ ghi chỉ số tham chiếu Hóa đơn/Hợp đồng/Phòng: xóa trước các bảng đó.
        await db.UtilityReadingPeriods.ExecuteDeleteAsync();
        // Hóa đơn (VHT-35) tham chiếu Contracts bằng Restrict: xóa dòng hóa đơn rồi hóa đơn trước hợp đồng.
        await db.InvoiceItems.ExecuteDeleteAsync();
        await db.Invoices.ExecuteDeleteAsync();
        // Đăng ký dịch vụ (yêu cầu + hiệu lực theo tháng): xóa trước Contracts.
        await db.ContractServiceRegistrations.ExecuteDeleteAsync();
        await db.ServiceRegistrationRequests.ExecuteDeleteAsync();
        // P5 (D10): lịch sử sửa chỉ số xóa trước chỉ số; chỉ số tham chiếu Rooms.
        await db.UtilityReadingAudits.ExecuteDeleteAsync();
        await db.UtilityReadings.ExecuteDeleteAsync();
        // D16: nhật ký thao tác độc lập, xóa trước Users/Properties.
        await db.AuditLogEntries.ExecuteDeleteAsync();
        // Hợp đồng tham chiếu Tenants/Rooms bằng Restrict: xóa trước hai bảng đó.
        // VHT-48 3A: yêu cầu gia hạn giữ lịch sử (Restrict) nên phải xóa tường minh trước Contracts.
        await db.ContractRenewalRequests.ExecuteDeleteAsync();
        await db.ContractMembers.ExecuteDeleteAsync();
        await db.Contracts.ExecuteDeleteAsync();
        await db.UtilityBillingCostLineSnapshots.ExecuteDeleteAsync();
        await db.UtilityBillingPeriodSnapshots.ExecuteDeleteAsync();
        await db.UtilityTariffTiers.Where(tier => tier.UtilityTariffId > 5).ExecuteDeleteAsync();
        await db.UtilityTariffs.Where(tariff => tariff.Id > 5).ExecuteDeleteAsync();
        // VHT-96: hang doi duyet co FK toi Properties va AspNetUsers: xoa truoc ca hai.
        await db.ManagerApprovalRequests.ExecuteDeleteAsync();
        await db.MaintenanceIncidentHistories.ExecuteDeleteAsync();
        await db.MaintenanceIncidents.ExecuteDeleteAsync();
        // VHT-24: PropertyManagers co FK toi Properties va AspNetUsers: xoa truoc ca hai.
        await db.PropertyManagers.ExecuteDeleteAsync();
        // Cong tac (VHT-94) tro tới AspNetUsers qua UpdatedByUserId: gỡ tham chiếu trước khi xóa user.
        await db.ManagerToggles.ExecuteUpdateAsync(s => s.SetProperty(t => t.UpdatedByUserId, (string?)null));
        await db.FileAccessAuditLogs.ExecuteDeleteAsync();
        await db.StoredFiles.IgnoreQueryFilters().ExecuteDeleteAsync();
        // Lịch sử giá có query filter theo ServiceCatalog xóa mềm: bỏ filter để xóa cả dòng
        // của danh mục đã xóa mềm, nếu không xóa ServiceCatalogs sẽ vướng FK.
        await db.ServicePriceHistories.IgnoreQueryFilters().ExecuteDeleteAsync();
        // Xóa mềm dịch vụ vẫn còn dòng trong bảng: bỏ qua query filter để dọn sạch.
        await db.ServiceCatalogs.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Rooms.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Properties.IgnoreQueryFilters().ExecuteDeleteAsync();
        // VHT-48 3A: liên kết khách ↔ chủ trọ giữ lịch sử (Restrict) nên xóa trước Tenants.
        // Filter đồng bộ với Tenant xóa mềm cũng phải bỏ để không sót liên kết.
        await db.TenantOwnerLinks.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.Tenants.IgnoreQueryFilters().ExecuteDeleteAsync();
        await db.UserRoles.ExecuteDeleteAsync();
        await db.RoleClaims.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
        await db.Roles.ExecuteDeleteAsync();
    }

    /// <summary>Seed tài khoản kèm vai trò (OwnerId/UserId là FK thật tới AspNetUsers).</summary>
    public static async Task SeedAccountAsync(ApplicationDbContext db, string userId, string roleName)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role is null)
        {
            role = new IdentityRole(roleName)
            {
                // UserManager.GetUsersInRoleAsync tra theo NormalizedName; seed thang vao Db
                // phai tu dien cot nay, neu khong RoleService khong thay Admin cuoi cung.
                NormalizedName = roleName.ToUpperInvariant(),
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }
        else if (role.NormalizedName is null)
        {
            role.NormalizedName = roleName.ToUpperInvariant();
            await db.SaveChangesAsync();
        }

        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            // Identity (.NET 10) tu choi UpdateAsync khi Email rong: seed email hop le de
            // UserManager (duyet/khoa) chay duoc nhu tai khoan that.
            Email = $"{userId}@test.local",
            NormalizedEmail = $"{userId}@test.local".ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            EmailConfirmed = true,
            // VHT-95: tai khoan gieo cho test coi nhu da duyet de dang nhap duoc.
            ApprovedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = role.Id });
        await db.SaveChangesAsync();
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
