using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Data.Seeding;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Properties.Entities;
using Web.Infrastructure.Auditing;

namespace NhaTro.UnitTests.Features.Audit;

/// <summary>VHT-104: seeder nhật ký mẫu ghi đúng sổ file và không nhân bản khi chạy lại.</summary>
public sealed class AuditDemoSeederTests : IDisposable
{
    private const string OwnerA = "owner-a";
    private const string OwnerB = "owner-b";
    private const string ManagerA = "manager-a";
    private const string AdminId = "admin-1";

    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "NhaTro_AuditDemoTests", Guid.NewGuid().ToString("N"));

    private static readonly DateTimeOffset Now = new(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);

    private ApplicationDbContext NewDb()
    {
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        db.Properties.AddRange(
            new Property { Id = 1, Name = "Nhà A", OwnerId = OwnerA },
            new Property { Id = 2, Name = "Nhà A2", OwnerId = OwnerA },
            new Property { Id = 3, Name = "Nhà B", OwnerId = OwnerB });
        db.PropertyManagers.Add(new PropertyManager
        {
            Id = 1,
            PropertyId = 1,
            ManagerUserId = ManagerA,
            AssignedByUserId = OwnerA,
        });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task Seed_ghi_dung_so_he_thong_va_so_tung_chu_tro()
    {
        using var db = NewDb();
        var store = new AuditLogFileStore(_root);
        var audit = new AuditLogService(store, db);

        var written = await AuditDemoSeeder.SeedAsync(store, db, audit, Now, AdminId);

        Assert.Equal(8, written);

        var system = await store.ReadAsync(AuditLogCategory.Account, ownerId: null);
        Assert.Equal(2, system.Count);
        Assert.All(system, entry =>
        {
            Assert.Equal(AdminId, entry.ActorUserId);
            Assert.True(entry.OccurredAt >= Now.AddDays(-3));
        });

        var ownerA = await store.ReadAsync(AuditLogCategory.Business, OwnerA);
        Assert.Equal(3, ownerA.Count);
        Assert.All(ownerA, entry =>
        {
            Assert.Contains(entry.PropertyId, new int?[] { 1, 2 });
            Assert.True(entry.OccurredAt >= Now.AddDays(-3));
        });
        Assert.Contains(ownerA, entry => entry.Action == "ContractRenewed" && entry.ActorUserId == ManagerA);
        Assert.Contains(ownerA, entry => entry.Action == "ContractLiquidated" && entry.PropertyId == 2);

        var ownerB = await store.ReadAsync(AuditLogCategory.Business, OwnerB);
        Assert.Equal(3, ownerB.Count);
        Assert.All(ownerB, entry => Assert.Equal(3, entry.PropertyId));
    }

    [Fact]
    public async Task Seed_chay_lai_khong_nhan_ban_ban_ghi()
    {
        using var db = NewDb();
        var store = new AuditLogFileStore(_root);
        var audit = new AuditLogService(store, db);

        await AuditDemoSeeder.SeedAsync(store, db, audit, Now, AdminId);
        var again = await AuditDemoSeeder.SeedAsync(store, db, audit, Now.AddMinutes(5), AdminId);

        Assert.Equal(0, again);
        Assert.Equal(2, (await store.ReadAsync(AuditLogCategory.Account, ownerId: null)).Count);
        Assert.Equal(3, (await store.ReadAsync(AuditLogCategory.Business, OwnerA)).Count);
        Assert.Equal(3, (await store.ReadAsync(AuditLogCategory.Business, OwnerB)).Count);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
