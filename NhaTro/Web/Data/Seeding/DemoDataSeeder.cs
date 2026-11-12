using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Audit.Entities;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Notifications;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Storage.Entities;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Enums;
using Web.Data.Tariffs;
using Web.Features.Accounts;
using Web.Features.Accounts.Otp;

namespace Web.Data.Seeding;

/// <summary>
/// Bộ nạp duy nhất cho dữ liệu demo: đọc <c>Data/Seeding/demo-data.json</c>, kiểm liên kết
/// rồi ghi trong một transaction. File không tồn tại ⇒ không nạp gì (an toàn khi public repo).
/// Chỉ chạy ở Development và database có tên chứa <c>Demo</c>.
/// </summary>
public static class DemoDataSeeder
{
    public const string DataFileName = "demo-data.json";
    public const string CompletedAction = "DemoSeed.Completed";
    public const string DefaultPassword = "Demo@123456";
    private const int SupportedSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string ResolveDataPath(IHostEnvironment environment) =>
        Path.Combine(environment.ContentRootPath, "Data", "Seeding", DataFileName);

    /// <summary>
    /// Chạy <c>--seed-demo</c> (<paramref name="clear"/> = false) hoặc <c>--clear-demo</c>.
    /// Trả false khi môi trường/DB không an toàn; Program dùng để đặt exit code.
    /// </summary>
    public static async Task<bool> RunAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger logger,
        bool clear,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<ApplicationDbContext>();
        var auditStore = provider.GetRequiredService<Web.Infrastructure.Auditing.AuditLogFileStore>();
        var databaseName = db.Database.GetDbConnection().Database;

        if (!environment.IsDevelopment())
        {
            logger.LogError("DemoSeed: chỉ chạy ở Development (hiện tại {Environment}).", environment.EnvironmentName);
            return false;
        }

        if (!databaseName.Contains("Demo", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("DemoSeed: tên database '{Database}' không chứa 'Demo'; từ chối thao tác.", databaseName);
            return false;
        }

        if (clear)
        {
            await db.Database.EnsureDeletedAsync(cancellationToken);
            logger.LogInformation("DemoSeed.Cleared: đã xoá database {Database}.", databaseName);
            return true;
        }

        var path = ResolveDataPath(environment);
        if (!System.IO.File.Exists(path))
        {
            logger.LogInformation("Demo data file not found; skip. ({Path})", path);
            return true;
        }

        await db.Database.MigrateAsync(cancellationToken);
        await RoleSeeder.EnsureAsync(provider);

        var json = await System.IO.File.ReadAllTextAsync(path, cancellationToken);
        var document = JsonSerializer.Deserialize<DemoSeedDocument>(json, JsonOptions)
            ?? throw new InvalidOperationException("DemoSeed: file dữ liệu rỗng hoặc không đúng schema.");

        if (document.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidOperationException(
                $"DemoSeed: schemaVersion {document.SchemaVersion} không hỗ trợ (cần {SupportedSchemaVersion}).");
        }

        if (string.IsNullOrWhiteSpace(document.SeedName))
        {
            throw new InvalidOperationException("DemoSeed: seedName bắt buộc.");
        }

        var marker = $"\"seedName\":\"{document.SeedName}\"";
        // VHT-104: dấu hoàn tất seed nằm trong file nhật ký, không còn ở bảng AuditLogEntries.
        var alreadySeeded = (await auditStore.ReadAsync(
                Web.Features.Audit.Entities.AuditLogCategory.Account, ownerId: null, cancellationToken))
            .Any(entry => entry.Action == CompletedAction
                && entry.Metadata is not null
                && entry.Metadata.Contains(document.SeedName, StringComparison.Ordinal));
        if (alreadySeeded)
        {
            logger.LogInformation("DemoSeed: bộ '{Seed}' đã nạp trước đó; bỏ qua.", document.SeedName);
            return true;
        }

        var occupiedTables = await FindOccupiedTablesAsync(db, cancellationToken);
        if (occupiedTables is not null)
        {
            logger.LogError(
                "DemoSeed: database '{Database}' đã có dữ liệu ở {Tables}. Bộ seed dùng ID cố định nên không nạp chồng được. " +
                "Chạy 'dotnet run --project NhaTro/Web -- --clear-demo' để tạo lại database demo sạch, rồi chạy lại '--seed-demo'.",
                databaseName,
                occupiedTables);
            return false;
        }

        Validate(document);
        logger.LogInformation("DemoSeed: bắt đầu nạp '{Seed}' vào {Database}.", document.SeedName, databaseName);

        var password = configuration["DemoSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            password = DefaultPassword;
        }

        var storageRoot = provider.GetRequiredService<IOptions<StorageOptions>>().Value
            .ResolveRootPath(environment.ContentRootPath);
        if (storageRoot.Length == 0)
        {
            throw new InvalidOperationException("DemoSeed: Storage:RootPath chưa cấu hình.");
        }

        var writtenFiles = new List<string>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await SeedAccountsAsync(provider, document, password, logger);

            WritePhysicalFiles(document, storageRoot, writtenFiles);
            await InsertAsync(db, document.Files.Select(item => item.File), cancellationToken);
            await InsertIdentityAsync(db, "Tenants", document.Tenants, cancellationToken);
            await InsertIdentityAsync(db, "Properties", document.Properties, cancellationToken);
            await InsertIdentityAsync(db, "Rooms", document.Rooms, cancellationToken);
            await InsertIdentityAsync(db, "PropertyManagers", document.PropertyManagers, cancellationToken);
            await InsertAsync(db, document.TenantOwnerLinks, cancellationToken);
            await InsertIdentityAsync(db, "TenantInvitations", document.Invitations, cancellationToken);
            await InsertIdentityAsync(db, "ServiceCatalogs", document.ServiceCatalogs, cancellationToken);
            await InsertIdentityAsync(db, "ServicePriceHistory", document.ServicePriceHistories, cancellationToken);
            await InsertAsync(db, document.Contracts, cancellationToken);
            await InsertAsync(db, document.ContractMembers, cancellationToken);
            await InsertAsync(db, document.RenewalRequests, cancellationToken);
            await InsertIdentityAsync(db, "ContractServiceRegistrations", document.ServiceRegistrations, cancellationToken);
            await InsertIdentityAsync(db, "ServiceRegistrationRequests", document.ServiceRegistrationRequests, cancellationToken);
            await InsertIdentityAsync(db, "UtilityReadings", document.UtilityReadings, cancellationToken);
            await InsertIdentityAsync(db, "UtilityReadingAudits", document.UtilityReadingAudits, cancellationToken);
            await InsertIdentityAsync(db, "UtilityBillingPeriodSnapshots", document.UtilityBillingPeriods, cancellationToken);
            await InsertIdentityAsync(db, "UtilityBillingCostLineSnapshots", document.UtilityBillingCostLines, cancellationToken);
            await InsertIdentityAsync(db, "Invoices", document.Invoices, cancellationToken);
            await InsertIdentityAsync(db, "InvoiceItems", document.InvoiceItems, cancellationToken);
            await InsertIdentityAsync(db, "Payments", document.Payments, cancellationToken);
            await InsertAsync(db, document.PaymentProofs, cancellationToken);
            await InsertIdentityAsync(db, "PaymentAuditLogs", document.PaymentAuditLogs, cancellationToken);
            await InsertIdentityAsync(db, "UtilityReadingPeriods", document.UtilityReadingPeriods, cancellationToken);
            await InsertIdentityAsync(db, "UtilityServicePeriodSnapshots", document.UtilityServiceSnapshots, cancellationToken);
            await InsertIdentityAsync(db, "MaintenanceIncidents", document.Incidents, cancellationToken);
            await InsertIdentityAsync(db, "MaintenanceIncidentHistories", document.IncidentHistory, cancellationToken);

            await ResolveApprovalTokensAsync(db, document, cancellationToken);
            await InsertAsync(db, document.Approvals, cancellationToken);
            await InsertAsync(db, document.IncidentPhotos, cancellationToken);

            await InsertIdentityAsync(db, "OtpChallenges", document.OtpChallenges, cancellationToken);
            await InsertIdentityAsync(db, "PendingRoomAssignments", document.PendingRoomAssignments, cancellationToken);
            await InsertIdentityAsync(db, "AppNotifications", document.Notifications, cancellationToken);
            await InsertIdentityAsync(db, "FileAccessAuditLogs", document.FileAccessLogs, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            DeleteWrittenFiles(writtenFiles, logger);
            throw;
        }

        // VHT-104: nhật ký demo ghi ra file sau khi DB commit; dùng thời điểm hiện tại để
        // không bị hạn lưu 3 ngày dọn ngay khi vừa seed.
        var audit = provider.GetRequiredService<Web.Features.Audit.Services.AuditLogService>();
        await audit.RecordAsync(
            Web.Features.Audit.Entities.AuditLogCategory.Account,
            CompletedAction,
            metadata: JsonSerializer.Serialize(new
            {
                seedName = document.SeedName,
                schemaVersion = document.SchemaVersion,
                seedNow = document.SeedNow,
                marker,
            }),
            cancellationToken: cancellationToken);

        foreach (var log in document.AuditLogs)
        {
            await audit.RecordAsync(
                log.Category,
                log.Action,
                log.ActorUserId,
                log.PropertyId,
                log.Metadata,
                DateTimeOffset.UtcNow,
                cancellationToken);
        }

        LogSummary(document, logger);
        return true;
    }

    /// <summary>
    /// Các bảng này được nạp bằng ID cố định; database đã có bản ghi thì chèn sẽ trùng khoá.
    /// Trả về danh sách bảng đang có dữ liệu (kể cả bản ghi xoá mềm), hoặc null nếu sạch.
    /// </summary>
    private static async Task<string?> FindOccupiedTablesAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        static Task<bool> AnyAsync<T>(ApplicationDbContext db, CancellationToken ct) where T : class =>
            db.Set<T>().IgnoreQueryFilters().AnyAsync(ct);

        (string Table, Func<Task<bool>> HasRows)[] checks =
        [
            ("Tenants", () => AnyAsync<Tenant>(db, cancellationToken)),
            ("Properties", () => AnyAsync<Property>(db, cancellationToken)),
            ("Rooms", () => AnyAsync<Room>(db, cancellationToken)),
            ("PropertyManagers", () => AnyAsync<PropertyManager>(db, cancellationToken)),
            ("TenantInvitations", () => AnyAsync<TenantInvitation>(db, cancellationToken)),
            ("ServiceCatalogs", () => AnyAsync<ServiceCatalog>(db, cancellationToken)),
            ("ServicePriceHistory", () => AnyAsync<ServicePriceHistory>(db, cancellationToken)),
            ("ContractServiceRegistrations", () => AnyAsync<ContractServiceRegistration>(db, cancellationToken)),
            ("ServiceRegistrationRequests", () => AnyAsync<ServiceRegistrationRequest>(db, cancellationToken)),
            ("UtilityReadings", () => AnyAsync<UtilityReading>(db, cancellationToken)),
            ("UtilityReadingAudits", () => AnyAsync<UtilityReadingAudit>(db, cancellationToken)),
            ("UtilityBillingPeriodSnapshots", () => AnyAsync<UtilityBillingPeriodSnapshot>(db, cancellationToken)),
            ("UtilityBillingCostLineSnapshots", () => AnyAsync<UtilityBillingCostLineSnapshot>(db, cancellationToken)),
            ("Invoices", () => AnyAsync<Invoice>(db, cancellationToken)),
            ("InvoiceItems", () => AnyAsync<InvoiceItem>(db, cancellationToken)),
            ("Payments", () => AnyAsync<Payment>(db, cancellationToken)),
            ("PaymentAuditLogs", () => AnyAsync<PaymentAuditLog>(db, cancellationToken)),
            ("UtilityReadingPeriods", () => AnyAsync<UtilityReadingPeriod>(db, cancellationToken)),
            ("UtilityServicePeriodSnapshots", () => AnyAsync<UtilityServicePeriodSnapshot>(db, cancellationToken)),
            ("MaintenanceIncidents", () => AnyAsync<MaintenanceIncident>(db, cancellationToken)),
            ("MaintenanceIncidentHistories", () => AnyAsync<MaintenanceIncidentHistory>(db, cancellationToken)),
            ("OtpChallenges", () => AnyAsync<OtpChallenge>(db, cancellationToken)),
            ("PendingRoomAssignments", () => AnyAsync<PendingRoomAssignment>(db, cancellationToken)),
            ("AppNotifications", () => AnyAsync<AppNotification>(db, cancellationToken)),
            ("FileAccessAuditLogs", () => AnyAsync<FileAccessAuditLog>(db, cancellationToken)),
        ];

        var occupied = new List<string>();
        foreach (var check in checks)
        {
            if (await check.HasRows())
            {
                occupied.Add(check.Table);
            }
        }

        if (occupied.Count == 0)
        {
            return null;
        }

        var preview = string.Join(", ", occupied.Take(6));
        return occupied.Count > 6 ? $"{preview} (+{occupied.Count - 6} bảng khác)" : preview;
    }

    private static async Task SeedAccountsAsync(
        IServiceProvider provider,
        DemoSeedDocument document,
        string password,
        ILogger logger)
    {
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var account in document.Accounts)
        {
            var existing = await userManager.FindByEmailAsync(account.Email);
            if (existing is not null)
            {
                throw new InvalidOperationException(
                    $"DemoSeed: tài khoản {account.Email} đã tồn tại; không nạp chồng.");
            }

            var user = new ApplicationUser
            {
                Id = account.Id,
                UserName = account.Email,
                Email = account.Email,
                EmailConfirmed = account.EmailConfirmed,
                FullName = account.FullName,
                PhoneNumber = account.PhoneNumber,
                ApprovedAt = account.ApprovedAt,
                LockoutEnd = account.LockoutEnd,
                OriginalEmail = account.OriginalEmail,
                IsDeleted = account.IsDeleted,
                DeletedAt = account.DeletedAt,
                DeletedByUserId = account.DeletedByUserId,
            };
            user.LockoutEnabled = true;

            var created = await userManager.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"DemoSeed: không tạo được {account.Email}: {string.Join(", ", created.Errors.Select(error => error.Description))}");
            }

            foreach (var role in account.Roles.Distinct(StringComparer.Ordinal))
            {
                var added = await userManager.AddToRoleAsync(user, role);
                if (!added.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"DemoSeed: không gán được vai trò {role} cho {account.Email}: {string.Join(", ", added.Errors.Select(error => error.Description))}");
                }
            }

            logger.LogDebug("DemoSeed: đã tạo {Email} ({Roles}).", account.Email, string.Join('+', account.Roles));
        }
    }

    private static void WritePhysicalFiles(DemoSeedDocument document, string storageRoot, List<string> written)
    {
        var root = Path.GetFullPath(storageRoot);
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        foreach (var entry in document.Files)
        {
            var file = entry.File;
            if (!file.RelativePath.StartsWith("demo/", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"DemoSeed: tệp {file.Id} phải nằm dưới demo/ để dọn dẹp an toàn.");
            }

            var bytes = Convert.FromBase64String(entry.ContentBase64);
            if (bytes.Length != file.FileSizeBytes)
            {
                throw new InvalidOperationException(
                    $"DemoSeed: FileSizeBytes của {file.Id} ({file.FileSizeBytes}) không khớp nội dung ({bytes.Length}).");
            }

            var physical = Path.GetFullPath(Path.Combine(root, file.RelativePath));
            if (!physical.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"DemoSeed: đường dẫn tệp {file.RelativePath} thoát khỏi Storage:RootPath.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(physical)!);
            System.IO.File.WriteAllBytes(physical, bytes);
            written.Add(physical);
        }
    }

    private static void DeleteWrittenFiles(IEnumerable<string> paths, ILogger logger)
    {
        foreach (var path in paths)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning("DemoSeed: không xoá được tệp tạm {Name}.", Path.GetFileName(path));
            }
        }
    }

    private static async Task InsertAsync<TEntity>(
        ApplicationDbContext db,
        IEnumerable<TEntity> rows,
        CancellationToken cancellationToken) where TEntity : class
    {
        var list = rows as IReadOnlyList<TEntity> ?? rows.ToList();
        if (list.Count == 0)
        {
            return;
        }

        db.Set<TEntity>().AddRange(list);
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// Bảng có khoá int/long tự tăng nhưng dữ liệu demo trỏ FK bằng ID tường minh,
    /// nên mỗi bảng phải bật IDENTITY_INSERT riêng và lưu riêng (SQL Server chỉ cho một bảng).
    /// </summary>
    private static async Task InsertIdentityAsync<TEntity>(
        ApplicationDbContext db,
        string table,
        IEnumerable<TEntity> rows,
        CancellationToken cancellationToken) where TEntity : class
    {
        var list = rows as IReadOnlyList<TEntity> ?? rows.ToList();
        if (list.Count == 0)
        {
            return;
        }

        await SetIdentityInsertAsync(db, table, on: true, cancellationToken);
        try
        {
            db.Set<TEntity>().AddRange(list);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            await SetIdentityInsertAsync(db, table, on: false, cancellationToken);
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Chỉ bật/tắt IDENTITY_INSERT cho tên bảng nội bộ; dựng câu lệnh bằng nối chuỗi
    /// (không nội suy) và escape ngoặc vuông để EF1002 không báo.
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "EF1002",
        Justification = "Tên bảng là hằng nội bộ của seeder, đã escape ngoặc vuông.")]
    private static Task SetIdentityInsertAsync(
        ApplicationDbContext db,
        string table,
        bool on,
        CancellationToken cancellationToken)
    {
        var target = "[" + table.Replace("]", "]]", StringComparison.Ordinal) + "]";
        var sql = "SET IDENTITY_INSERT " + target + (on ? " ON" : " OFF");
        return db.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    /// <summary>
    /// SW-11/SW-13 lưu rowversion của đối tượng tại thời điểm gửi yêu cầu.
    /// JSON không thể chứa giá trị do SQL sinh, nên đọc lại sau khi đã chèn đối tượng.
    /// </summary>
    private static async Task ResolveApprovalTokensAsync(
        ApplicationDbContext db,
        DemoSeedDocument document,
        CancellationToken cancellationToken)
    {
        foreach (var request in document.Approvals.Where(item => item.Status == ApprovalRequestStatus.Pending))
        {
            switch (request.OperationCode)
            {
                case "SW-06":
                {
                    var tenantId = ParseKey(request.TargetKey);
                    request.ExpectedToken = await db.Tenants.IgnoreQueryFilters()
                        .Where(item => item.Id == tenantId)
                        .Select(item => item.RowVersion)
                        .SingleAsync(cancellationToken);
                    break;
                }
                case "SW-11":
                {
                    var invoiceId = ParseKey(request.TargetKey);
                    request.ExpectedToken = await db.Invoices
                        .Where(item => item.Id == invoiceId)
                        .Select(item => item.RowVersion)
                        .SingleAsync(cancellationToken);
                    break;
                }
                case "SW-13":
                {
                    var incidentId = ParseKey(request.TargetKey);
                    request.ExpectedToken = await db.MaintenanceIncidents
                        .Where(item => item.Id == incidentId)
                        .Select(item => item.RowVersion)
                        .SingleAsync(cancellationToken);
                    break;
                }
            }
        }
    }

    private static int ParseKey(string key) =>
        int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"DemoSeed: targetKey '{key}' phải là số dương.");

    private static void LogSummary(DemoSeedDocument document, ILogger logger)
    {
        logger.LogInformation(
            "DemoSeed.Completed {Seed}: {Accounts} tài khoản, {Tenants} hồ sơ khách, {Properties} nhà trọ, {Rooms} phòng, " +
            "{Contracts} hợp đồng, {Invoices} hóa đơn, {Payments} thanh toán, {Readings} chỉ số, {Incidents} sự cố, " +
            "{Approvals} yêu cầu duyệt, {Invitations} lời mời, {Files} tệp.",
            document.SeedName,
            document.Accounts.Count,
            document.Tenants.Count,
            document.Properties.Count,
            document.Rooms.Count,
            document.Contracts.Count,
            document.Invoices.Count,
            document.Payments.Count,
            document.UtilityReadings.Count,
            document.Incidents.Count,
            document.Approvals.Count,
            document.Invitations.Count,
            document.Files.Count);
    }

    // ── Kiểm tra toàn vẹn trước khi ghi ───────────────────────────────────────
    private static void Validate(DemoSeedDocument document)
    {
        var userIds = document.Accounts.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var propertyIds = document.Properties.Select(item => item.Id).ToHashSet();
        var roomIds = document.Rooms.Select(item => item.Id).ToHashSet();
        var tenantIds = document.Tenants.Select(item => item.Id).ToHashSet();
        var contractById = document.Contracts.ToDictionary(item => item.ContractId);
        var invoiceById = document.Invoices.ToDictionary(item => item.Id);
        var fileIds = document.Files.Select(item => item.File.Id).ToHashSet();
        var serviceIds = document.ServiceCatalogs.Select(item => item.Id).ToHashSet();
        var priceIds = document.ServicePriceHistories.Select(item => item.Id).ToHashSet();
        var readingIds = document.UtilityReadings.Select(item => item.Id).ToHashSet();
        var readingPeriodIds = document.UtilityReadingPeriods.Select(item => item.Id).ToHashSet();
        var incidentById = document.Incidents.ToDictionary(item => item.Id);

        Require(document.Accounts.Count == 120, "phải có đúng 120 tài khoản.");
        Require(document.Tenants.Count == 106, "phải có đúng 106 hồ sơ khách.");
        Require(document.Accounts.Select(item => item.Email).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                == document.Accounts.Count, "email tài khoản bị trùng.");
        Require(document.Accounts.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count()
                == document.Accounts.Count, "UserId bị trùng.");
        Require(document.Accounts.All(item => item.Roles.Count > 0), "mỗi tài khoản phải có ít nhất một vai trò.");
        Require(document.Properties.Count == 8, "phải có 8 nhà trọ.");
        Require(document.Rooms.Count == 60, "phải có 60 phòng.");
        Require(document.Rooms.Select(item => (item.PropertyId, item.Code)).Distinct().Count() == document.Rooms.Count,
            "mã phòng trùng trong cùng nhà trọ.");

        foreach (var account in document.Accounts)
        {
            Require(account.Roles.All(role => role is Roles.Admin or Roles.ChuTro or Roles.QuanLy or Roles.KhachThue),
                $"vai trò không hợp lệ ở {account.Email}.");
        }

        foreach (var room in document.Rooms)
        {
            Require(propertyIds.Contains(room.PropertyId), $"phòng {room.Code} trỏ nhà trọ không tồn tại.");
            Require(room.StandardPrice > 0, $"phòng {room.Code} phải có giá thuê.");
        }

        foreach (var property in document.Properties)
        {
            Require(userIds.Contains(property.OwnerId), $"nhà trọ {property.Name} có chủ trọ không tồn tại.");
        }

        foreach (var tenant in document.Tenants)
        {
            if (tenant.UserId is not null)
            {
                Require(userIds.Contains(tenant.UserId), $"hồ sơ khách {tenant.Id} trỏ tài khoản không tồn tại.");
            }
        }

        var linkedTenants = document.TenantOwnerLinks.Select(item => item.TenantId).ToHashSet();
        foreach (var tenantId in tenantIds)
        {
            Require(linkedTenants.Contains(tenantId), $"hồ sơ khách {tenantId} chưa có chủ trọ liên kết.");
        }

        foreach (var link in document.TenantOwnerLinks)
        {
            Require(tenantIds.Contains(link.TenantId), "TenantOwnerLink trỏ hồ sơ khách không tồn tại.");
            Require(userIds.Contains(link.OwnerId), "TenantOwnerLink trỏ chủ trọ không tồn tại.");
        }

        foreach (var manager in document.PropertyManagers)
        {
            Require(propertyIds.Contains(manager.PropertyId), "phân công quản lý trỏ nhà trọ không tồn tại.");
            Require(userIds.Contains(manager.ManagerUserId), "phân công quản lý trỏ tài khoản không tồn tại.");
            Require(userIds.Contains(manager.AssignedByUserId), "người gán quản lý không tồn tại.");
        }

        Require(document.Contracts.Select(item => item.ContractNumber).Distinct(StringComparer.Ordinal).Count()
                == document.Contracts.Count, "số hợp đồng bị trùng.");

        var approvedByRoom = document.Contracts
            .Where(item => item.Status == ContractStatus.Approved)
            .GroupBy(item => item.RoomId)
            .ToDictionary(group => group.Key, group => group.ToList());
        Require(approvedByRoom.Count == 42, "phải có 42 phòng có hợp đồng Approved.");
        Require(approvedByRoom.Values.All(items => items.Count == 1), "một phòng có nhiều hợp đồng Approved.");
        Require(document.Rooms.Count(item => item.Status == RoomStatus.Rented) == 42,
            "số phòng Rented phải bằng 42.");
        Require(approvedByRoom.Keys.All(roomIds.Contains), "hợp đồng Approved trỏ phòng không tồn tại.");
        Require(document.Contracts.Where(item => item.Status == ContractStatus.Approved)
            .All(item => document.Rooms.Single(room => room.Id == item.RoomId).Status == RoomStatus.Rented),
            "hợp đồng Approved phải nằm trên phòng Rented.");

        foreach (var contract in document.Contracts)
        {
            Require(roomIds.Contains(contract.RoomId), $"hợp đồng {contract.ContractNumber} trỏ phòng không tồn tại.");
            Require(contract.EndDate > contract.StartDate, $"hợp đồng {contract.ContractNumber} có kỳ hạn không hợp lệ.");
            Require(contract.DepositAmount >= 0, $"hợp đồng {contract.ContractNumber} có cọc âm.");
        }

        var membersByContract = document.ContractMembers
            .GroupBy(item => item.ContractId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.TenantId).ToList());
        foreach (var contract in document.Contracts.Where(item => item.Status == ContractStatus.Approved))
        {
            Require(membersByContract.TryGetValue(contract.ContractId, out var members) && members.Count > 0,
                $"hợp đồng Approved {contract.ContractNumber} thiếu thành viên.");
        }

        foreach (var member in document.ContractMembers)
        {
            Require(contractById.ContainsKey(member.ContractId), "ContractMember trỏ hợp đồng không tồn tại.");
            Require(tenantIds.Contains(member.TenantId), "ContractMember trỏ hồ sơ khách không tồn tại.");
        }

        foreach (var renewal in document.RenewalRequests)
        {
            Require(contractById.ContainsKey(renewal.ContractId), "yêu cầu gia hạn trỏ hợp đồng không tồn tại.");
            Require(tenantIds.Contains(renewal.RequestedByTenantId), "yêu cầu gia hạn trỏ khách không tồn tại.");
        }

        foreach (var catalog in document.ServiceCatalogs)
        {
            if (catalog.PropertyId is { } propertyId)
            {
                Require(propertyIds.Contains(propertyId), $"danh mục dịch vụ {catalog.Id} trỏ nhà trọ không tồn tại.");
            }
        }

        Require(document.ServicePriceHistories.Select(item => (item.ServiceCatalogId, item.EffectiveDate)).Distinct().Count()
                == document.ServicePriceHistories.Count, "lịch sử giá dịch vụ trùng mốc hiệu lực.");
        foreach (var price in document.ServicePriceHistories)
        {
            Require(serviceIds.Contains(price.ServiceCatalogId), "lịch sử giá trỏ danh mục không tồn tại.");
            Require(price.UnitPrice > 0, "giá dịch vụ phải dương.");
            Require(price.EffectiveTo is null || price.EffectiveTo > price.EffectiveDate,
                "EffectiveTo phải sau EffectiveDate.");
        }

        foreach (var registration in document.ServiceRegistrations)
        {
            Require(contractById.ContainsKey(registration.ContractId), "đăng ký dịch vụ trỏ hợp đồng không tồn tại.");
            Require(serviceIds.Contains(registration.ServiceCatalogId), "đăng ký dịch vụ trỏ danh mục không tồn tại.");
        }

        foreach (var request in document.ServiceRegistrationRequests)
        {
            Require(contractById.ContainsKey(request.ContractId), "yêu cầu dịch vụ trỏ hợp đồng không tồn tại.");
            Require(serviceIds.Contains(request.ServiceCatalogId), "yêu cầu dịch vụ trỏ danh mục không tồn tại.");
        }

        Require(document.UtilityReadings.Select(item => (item.RoomId, item.PeriodStart)).Distinct().Count()
                == document.UtilityReadings.Count, "chỉ số điện nước trùng phòng/kỳ.");
        foreach (var reading in document.UtilityReadings)
        {
            Require(roomIds.Contains(reading.RoomId), "chỉ số trỏ phòng không tồn tại.");
            Require(reading.Id <= 9999, "chỉ số cũ phải có Id ≤ 9999.");
            Require(reading.ElectricEnd >= reading.ElectricStart && reading.WaterEnd >= reading.WaterStart,
                "chỉ số cuối phải lớn hơn hoặc bằng chỉ số đầu.");
            Require(userIds.Contains(reading.RecordedByUserId), "người ghi chỉ số không tồn tại.");
        }

        foreach (var audit in document.UtilityReadingAudits)
        {
            Require(readingIds.Contains(audit.UtilityReadingId), "lịch sử sửa chỉ số trỏ chỉ số không tồn tại.");
            Require(userIds.Contains(audit.ChangedByUserId), "người sửa chỉ số không tồn tại.");
            Require(audit.OldElectricEnd != audit.NewElectricEnd || audit.OldWaterEnd != audit.NewWaterEnd,
                "lịch sử sửa chỉ số phải có giá trị thay đổi.");
        }

        Require(document.UtilityBillingPeriods.Select(item => (item.BillingPeriodId, item.Type)).Distinct().Count()
                == document.UtilityBillingPeriods.Count, "snapshot chi phí trùng kỳ/loại.");
        foreach (var snapshot in document.UtilityBillingPeriods)
        {
            Require(readingIds.Contains(snapshot.BillingPeriodId), "snapshot chi phí trỏ chỉ số không tồn tại.");
            Require(!readingPeriodIds.Contains(snapshot.BillingPeriodId),
                "BillingPeriodId của snapshot không được trùng Id UtilityReadingPeriods.");
            Require(snapshot.ResidentCount > 0, "snapshot chi phí phải có số người dương.");
        }

        var costLinesBySnapshot = document.UtilityBillingCostLines
            .GroupBy(item => item.UtilityBillingPeriodSnapshotId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var snapshotIds = document.UtilityBillingPeriods.Select(item => item.Id).ToHashSet();
        foreach (var group in costLinesBySnapshot)
        {
            Require(snapshotIds.Contains(group.Key), "dòng chi phí trỏ snapshot không tồn tại.");
            var total = document.UtilityBillingPeriods.Single(item => item.Id == group.Key).Total;
            Require(group.Value.Sum(line => line.Amount) == total, $"tổng bậc chi phí snapshot {group.Key} không khớp Total.");
        }

        Require(document.UtilityReadingPeriods.Select(item => (item.ContractId, item.PeriodStart)).Distinct().Count()
                == document.UtilityReadingPeriods.Count, "kỳ đọc số mới trùng hợp đồng/kỳ.");
        foreach (var period in document.UtilityReadingPeriods)
        {
            Require(period.Id >= 100001, "kỳ đọc số mới phải có Id ≥ 100001.");
            Require(contractById.ContainsKey(period.ContractId), "kỳ đọc số mới trỏ hợp đồng không tồn tại.");
            Require(roomIds.Contains(period.RoomId), "kỳ đọc số mới trỏ phòng không tồn tại.");
            Require(period.ResidentCount > 0, "kỳ đọc số mới phải có số người dương.");
            if (period.Status == UtilityReadingPeriodStatus.Invoiced)
            {
                Require(period.InvoiceId is not null && invoiceById.ContainsKey(period.InvoiceId.Value),
                    $"kỳ {period.Id} đã phát hành nhưng thiếu hóa đơn.");
            }
        }

        foreach (var service in document.UtilityServiceSnapshots)
        {
            Require(readingPeriodIds.Contains(service.UtilityReadingPeriodId), "snapshot dịch vụ trỏ kỳ đọc số không tồn tại.");
            Require(serviceIds.Contains(service.SourceServiceCatalogId), "snapshot dịch vụ trỏ danh mục không tồn tại.");
            Require(priceIds.Contains(service.SourceServicePriceHistoryId), "snapshot dịch vụ trỏ bảng giá không tồn tại.");
        }

        Require(document.Invoices.Select(item => item.Id).Distinct().Count() == document.Invoices.Count,
            "Id hóa đơn bị trùng.");
        var invoicePeriodKeys = document.Invoices
            .Where(item => item.RegistrationId is null)
            .Select(item => (item.ContractId, item.PeriodStart))
            .ToList();
        Require(invoicePeriodKeys.Distinct().Count() == invoicePeriodKeys.Count, "hóa đơn trùng hợp đồng/kỳ.");
        foreach (var invoice in document.Invoices)
        {
            Require(contractById.TryGetValue(invoice.ContractId, out var contract), "hóa đơn trỏ hợp đồng không tồn tại.");
            Require(invoice.TotalAmount >= 0, "tổng hóa đơn không được âm.");
            Require(invoice.Status == InvoiceStatus.Draft
                    || invoice.PeriodStart >= DateOnly.FromDateTime(contract!.StartDate)
                    && invoice.PeriodEnd <= DateOnly.FromDateTime(contract.EndDate),
                $"hóa đơn {invoice.Id} nằm ngoài kỳ hạn hợp đồng.");
            if (invoice.Status != InvoiceStatus.Draft)
            {
                Require(invoice.IssuedAt is not null, $"hóa đơn {invoice.Id} đã phát hành nhưng thiếu IssuedAt.");
            }
        }

        var itemsByInvoice = document.InvoiceItems.GroupBy(item => item.InvoiceId)
            .ToDictionary(group => group.Key, group => group.ToList());
        Require(document.InvoiceItems.Select(item => item.Id).Distinct().Count() == document.InvoiceItems.Count,
            "Id dòng hóa đơn bị trùng.");
        foreach (var invoice in document.Invoices)
        {
            Require(itemsByInvoice.TryGetValue(invoice.Id, out var items) && items.Count > 0,
                $"hóa đơn {invoice.Id} không có dòng.");
            var sum = items!.Sum(item => item.Amount);
            Require(sum == invoice.TotalAmount, $"tổng dòng hóa đơn {invoice.Id} ({sum}) khác TotalAmount ({invoice.TotalAmount}).");
            foreach (var item in items!)
            {
                Require(item.Quantity > 0, $"dòng hóa đơn {item.Id} phải có số lượng dương.");
                Require(item.ItemType == InvoiceItemType.Discount ? item.Amount <= 0 : item.Amount >= 0,
                    $"dấu tiền dòng hóa đơn {item.Id} sai.");
            }
        }

        var paymentsByInvoice = document.Payments.GroupBy(item => item.InvoiceId)
            .ToDictionary(group => group.Key, group => group.ToList());
        Require(document.Payments.Select(item => item.Id).Distinct().Count() == document.Payments.Count,
            "Id thanh toán bị trùng.");
        foreach (var payment in document.Payments)
        {
            Require(invoiceById.TryGetValue(payment.InvoiceId, out var invoice), "thanh toán trỏ hóa đơn không tồn tại.");
            Require(payment.Amount == invoice!.TotalAmount, $"thanh toán {payment.Id} lệch tổng hóa đơn.");
            Require(userIds.Contains(payment.RequestedByUserId), "người gửi thanh toán không tồn tại.");
            Require(payment.Status == PaymentStatus.Pending && payment.ReviewedAt is null
                    || payment.Status != PaymentStatus.Pending && payment.ReviewedAt is not null,
                $"trạng thái/ReviewedAt của thanh toán {payment.Id} không khớp.");
            Require(payment.Status != PaymentStatus.Rejected || !string.IsNullOrWhiteSpace(payment.RejectionReason),
                $"thanh toán {payment.Id} bị từ chối nhưng thiếu lý do.");
            Require(payment.Status == PaymentStatus.Rejected || payment.RejectionReason is null,
                $"thanh toán {payment.Id} không bị từ chối nhưng có lý do từ chối.");
        }

        foreach (var invoice in document.Invoices.Where(item => item.Status == InvoiceStatus.Paid))
        {
            var confirmed = paymentsByInvoice.TryGetValue(invoice.Id, out var list)
                ? list.Count(item => item.Status == PaymentStatus.Confirmed)
                : 0;
            Require(confirmed == 1, $"hóa đơn Paid {invoice.Id} phải có đúng 1 thanh toán Confirmed.");
        }

        foreach (var invoice in document.Invoices.Where(item => item.Status != InvoiceStatus.Paid))
        {
            Require(!paymentsByInvoice.TryGetValue(invoice.Id, out var list)
                    || list.All(item => item.Status != PaymentStatus.Confirmed),
                $"hóa đơn chưa Paid {invoice.Id} không được có thanh toán Confirmed.");
        }

        var proofsByPayment = document.PaymentProofs.GroupBy(item => item.PaymentId)
            .ToDictionary(group => group.Key, group => group.ToList());
        foreach (var payment in document.Payments)
        {
            Require(proofsByPayment.TryGetValue(payment.Id, out var proofs) && proofs.Count > 0,
                $"thanh toán {payment.Id} thiếu minh chứng.");
            foreach (var proof in proofs!)
            {
                Require(fileIds.Contains(proof.FileId), $"minh chứng thanh toán {payment.Id} trỏ tệp không tồn tại.");
                var file = document.Files.Single(item => item.File.Id == proof.FileId).File;
                Require(!file.IsDeleted, $"minh chứng thanh toán {payment.Id} trỏ tệp đã xoá.");
            }
        }

        Require(document.PaymentProofs.Select(item => item.FileId).Distinct().Count() == document.PaymentProofs.Count,
            "một tệp được gắn cho nhiều thanh toán.");
        foreach (var log in document.PaymentAuditLogs)
        {
            Require(document.Payments.Any(item => item.Id == log.PaymentId), "nhật ký thanh toán trỏ thanh toán không tồn tại.");
            Require(invoiceById.ContainsKey(log.InvoiceId), "nhật ký thanh toán trỏ hóa đơn không tồn tại.");
        }

        foreach (var incident in document.Incidents)
        {
            Require(roomIds.Contains(incident.RoomId), $"sự cố {incident.Id} trỏ phòng không tồn tại.");
            Require(userIds.Contains(incident.ReportedByUserId), $"người báo sự cố {incident.Id} không tồn tại.");
            var history = document.IncidentHistory.Where(item => item.IncidentId == incident.Id).ToList();
            Require(history.Count > 0 && history[0].Action == IncidentHistoryAction.Created,
                $"lịch sử sự cố {incident.Id} phải bắt đầu bằng Created.");
        }

        foreach (var photo in document.IncidentPhotos)
        {
            Require(fileIds.Contains(photo.FileId), "ảnh sự cố trỏ tệp không tồn tại.");
            Require(photo.IncidentId is not null || photo.ApprovalRequestId is not null,
                "ảnh sự cố phải gắn sự cố hoặc yêu cầu duyệt.");
            if (photo.IncidentId is { } incidentId)
            {
                Require(incidentById.ContainsKey(incidentId), "ảnh trỏ sự cố không tồn tại.");
            }
        }

        var approvalIds = document.Approvals.Select(item => item.Id).ToHashSet();
        Require(approvalIds.Count == document.Approvals.Count, "Id yêu cầu duyệt bị trùng.");
        Require(document.Approvals
                .Where(item => item.Status == ApprovalRequestStatus.Pending)
                .Select(item => (item.OperationCode, item.TargetType, item.TargetKey))
                .Distinct().Count()
                == document.Approvals.Count(item => item.Status == ApprovalRequestStatus.Pending),
            "hai yêu cầu Pending trùng mã/đối tượng.");
        foreach (var approval in document.Approvals)
        {
            Require(userIds.Contains(approval.RequestedByUserId), "yêu cầu duyệt có người gửi không tồn tại.");
            if (approval.DecidedByUserId is { } decider)
            {
                Require(userIds.Contains(decider), "yêu cầu duyệt có người quyết định không tồn tại.");
            }

            if (approval.PropertyId is { } approvalPropertyId)
            {
                Require(propertyIds.Contains(approvalPropertyId), "yêu cầu duyệt trỏ nhà trọ không tồn tại.");
            }

            using var payload = ParsePayload(approval.PayloadJson, approval.OperationCode);

            if (approval.Status != ApprovalRequestStatus.Pending)
            {
                continue;
            }

            switch (approval.OperationCode)
            {
                case "SW-11":
                    Require(invoiceById.ContainsKey(ParseKey(approval.TargetKey)),
                        "yêu cầu SW-11 Pending trỏ hóa đơn không tồn tại.");
                    break;
                case "SW-13":
                    Require(incidentById.ContainsKey(ParseKey(approval.TargetKey)),
                        "yêu cầu SW-13 Pending trỏ sự cố không tồn tại.");
                    break;
                case "SW-10":
                case "SW-07":
                case "SW-08":
                    Require(Guid.TryParse(approval.TargetKey, out var contractId) && contractById.ContainsKey(contractId),
                        $"yêu cầu {approval.OperationCode} Pending trỏ hợp đồng không tồn tại.");
                    break;
                case "SW-12":
                    Require(roomIds.Contains(ParseKey(approval.TargetKey)),
                        "yêu cầu SW-12 Pending trỏ phòng không tồn tại.");
                    break;
                case "SW-14":
                    Require(incidentById.ContainsKey(ParseKey(approval.TargetKey)),
                        "yêu cầu SW-14 Pending trỏ sự cố không tồn tại.");
                    break;
                case "SW-16":
                    Require(approval.TargetKey.StartsWith("export:", StringComparison.Ordinal),
                        "yêu cầu SW-16 Pending phải có targetKey export:…");
                    break;
            }

            var approvalPhotos = document.IncidentPhotos.Where(item => item.ApprovalRequestId == approval.Id).ToList();
            if (approval.OperationCode == "SW-12")
            {
                var expected = payload.RootElement.GetProperty("expectedPhotoCount").GetInt32();
                Require(approvalPhotos.Count == expected
                        && approvalPhotos.All(item => item.Kind == IncidentPhotoKind.Report && item.IncidentId is null),
                    "yêu cầu SW-12 Pending phải có đúng số ảnh Report chưa gắn sự cố.");
            }
            else if (approval.OperationCode == "SW-14")
            {
                var expected = payload.RootElement.GetProperty("expectedPhotoCount").GetInt32();
                var incidentId = ParseKey(approval.TargetKey);
                Require(expected is >= 1 and <= 3 && approvalPhotos.Count == expected
                        && approvalPhotos.All(item => item.Kind == IncidentPhotoKind.Result && item.IncidentId == incidentId),
                    "yêu cầu SW-14 Pending phải có 1–3 ảnh Result gắn đúng sự cố.");
            }
        }

        foreach (var otp in document.OtpChallenges)
        {
            Require(!string.IsNullOrWhiteSpace(otp.CodeHash) && !string.IsNullOrWhiteSpace(otp.Salt),
                "OTP challenge thiếu hash/salt.");
            if (otp.UserId is { } otpUserId)
            {
                Require(userIds.Contains(otpUserId), "OTP challenge trỏ tài khoản không tồn tại.");
            }
        }

        foreach (var assignment in document.PendingRoomAssignments)
        {
            Require(tenantIds.Contains(assignment.TenantId), "phòng chờ nhận phòng trỏ khách không tồn tại.");
            Require(propertyIds.Contains(assignment.PropertyId), "phòng chờ nhận phòng trỏ nhà trọ không tồn tại.");
            Require(roomIds.Contains(assignment.RoomId), "phòng chờ nhận phòng trỏ phòng không tồn tại.");
        }

        foreach (var notification in document.Notifications)
        {
            Require(userIds.Contains(notification.RecipientUserId), "thông báo trỏ người nhận không tồn tại.");
        }

        foreach (var log in document.AuditLogs)
        {
            if (log.ActorUserId is { } actor)
            {
                Require(userIds.Contains(actor), "nhật ký thao tác trỏ người dùng không tồn tại.");
            }

            if (log.Category == AuditLogCategory.Business)
            {
                Require(log.PropertyId is not null && propertyIds.Contains(log.PropertyId.Value),
                    "nhật ký nghiệp vụ phải có PropertyId hợp lệ.");
            }
        }

        foreach (var log in document.FileAccessLogs)
        {
            Require(userIds.Contains(log.ActorUserId), "nhật ký tệp trỏ người dùng không tồn tại.");
            if (log.FileId is { } fileId)
            {
                Require(fileIds.Contains(fileId), "nhật ký tệp trỏ tệp không tồn tại.");
            }
        }
    }

    private static JsonDocument ParsePayload(string payloadJson, string operationCode)
    {
        try
        {
            var payload = JsonDocument.Parse(payloadJson);
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
            {
                payload.Dispose();
                throw new InvalidOperationException($"DemoSeed: payload của {operationCode} phải là JSON object.");
            }

            return payload;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"DemoSeed: payload của {operationCode} không phải JSON hợp lệ.", ex);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"DemoSeed: {message}");
        }
    }
}
