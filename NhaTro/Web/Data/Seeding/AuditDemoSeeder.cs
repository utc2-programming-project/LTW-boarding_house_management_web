using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Infrastructure.Auditing;

namespace Web.Data.Seeding;

/// <summary>
/// VHT-104: đổ nhật ký mẫu ra kho file để test màn hình /admin/audit-log khi DB demo đã có
/// dữ liệu (không thể chạy lại toàn bộ demo seed). Chỉ chạy tường minh bằng --seed-audit-demo.
/// </summary>
public static class AuditDemoSeeder
{
    public static async Task<bool> RunAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<ApplicationDbContext>();

        if (!environment.IsDevelopment())
        {
            logger.LogError("AuditDemo: chỉ chạy ở Development (hiện tại {Environment}).", environment.EnvironmentName);
            return false;
        }

        var databaseName = db.Database.IsRelational() ? db.Database.GetDbConnection().Database : string.Empty;
        if (!databaseName.Contains("Demo", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("AuditDemo: tên database '{Database}' không chứa 'Demo'; từ chối thao tác.", databaseName);
            return false;
        }

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var admins = await userManager.GetUsersInRoleAsync(Roles.Admin);
        var adminId = admins.OrderBy(user => user.Id, StringComparer.Ordinal).FirstOrDefault()?.Id;

        var written = await SeedAsync(
            provider.GetRequiredService<AuditLogFileStore>(),
            db,
            provider.GetRequiredService<AuditLogService>(),
            DateTimeOffset.UtcNow,
            adminId,
            cancellationToken);

        logger.LogInformation("AuditDemo: đã ghi {Count} dòng nhật ký mẫu vào kho file.", written);
        return true;
    }

    /// <summary>
    /// Ghi nhật ký mẫu theo từng sổ; sổ nào đã có file thì bỏ qua để chạy lại không nhân bản.
    /// Trả số dòng đã ghi.
    /// </summary>
    public static async Task<int> SeedAsync(
        AuditLogFileStore store,
        ApplicationDbContext db,
        AuditLogService audit,
        DateTimeOffset now,
        string? adminActorId,
        CancellationToken cancellationToken = default)
    {
        var properties = await db.Properties.AsNoTracking()
            .Select(property => new { property.Id, property.OwnerId })
            .ToListAsync(cancellationToken);
        var managers = await db.PropertyManagers.AsNoTracking()
            .Where(manager => manager.RevokedAt == null)
            .Select(manager => new { manager.PropertyId, manager.ManagerUserId })
            .ToListAsync(cancellationToken);

        var written = 0;

        if (!File.Exists(store.SystemFilePath))
        {
            await audit.RecordAsync(
                AuditLogCategory.Account,
                "OwnerUnlocked",
                adminActorId,
                metadata: JsonSerializer.Serialize(new { scope = "demo" }),
                occurredAt: now.AddHours(-3),
                cancellationToken: cancellationToken);
            await audit.RecordAsync(
                AuditLogCategory.Account,
                "OwnerLocked",
                adminActorId,
                metadata: JsonSerializer.Serialize(new { scope = "demo" }),
                occurredAt: now.AddDays(-2),
                cancellationToken: cancellationToken);
            written += 2;
        }

        var owners = properties
            .Where(property => !string.IsNullOrWhiteSpace(property.OwnerId))
            .GroupBy(property => property.OwnerId!)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();

        for (var index = 0; index < owners.Count; index++)
        {
            var owner = owners[index];
            if (File.Exists(store.OwnerFilePath(owner.Key)))
            {
                continue;
            }

            var ownerProperties = owner.OrderBy(property => property.Id).ToList();
            var primary = ownerProperties[0];
            var secondary = ownerProperties.Count > 1 ? ownerProperties[1] : primary;
            var managerId = managers.FirstOrDefault(item => item.PropertyId == primary.Id)?.ManagerUserId;
            var managerActor = string.IsNullOrWhiteSpace(managerId) ? owner.Key : managerId;
            var offset = TimeSpan.FromMinutes(index * 17);
            var contractId = 100 + index * 10;

            await audit.RecordAsync(
                AuditLogCategory.Business,
                "ContractDepositUpdated",
                owner.Key,
                primary.Id,
                JsonSerializer.Serialize(new { contractId = contractId + 1, oldDeposit = 2_000_000, newDeposit = 2_500_000 }),
                now.AddHours(-1) - offset,
                cancellationToken);
            await audit.RecordAsync(
                AuditLogCategory.Business,
                "ContractRenewed",
                managerActor,
                primary.Id,
                JsonSerializer.Serialize(new { contractId = contractId + 1, termMonths = 12 }),
                now.AddHours(-8) - offset,
                cancellationToken);
            await audit.RecordAsync(
                AuditLogCategory.Business,
                "ContractLiquidated",
                owner.Key,
                secondary.Id,
                JsonSerializer.Serialize(new { contractId = contractId + 2, reason = "demo" }),
                now.AddDays(-1) - offset,
                cancellationToken);
            written += 3;
        }

        return written;
    }
}
