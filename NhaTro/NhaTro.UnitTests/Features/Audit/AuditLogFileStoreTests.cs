using Web.Features.Audit.Entities;
using Web.Infrastructure.Auditing;

namespace NhaTro.UnitTests.Features.Audit;

public sealed class AuditLogFileStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "NhaTro_AuditTests", Guid.NewGuid().ToString("N"));

    private AuditLogFileStore NewStore(int retentionDays = 3) => new(_root, retentionDays);

    [Fact]
    public async Task Ghi_va_doc_lai_dung_file_theo_so()
    {
        var store = NewStore();
        await store.AppendManyAsync(
        [
            Entry(AuditLogCategory.Account, "OwnerLocked", ownerId: null),
            Entry(AuditLogCategory.Business, "ContractRenewed", ownerId: "owner-a"),
            Entry(AuditLogCategory.Business, "ContractLiquidated", ownerId: "owner-b"),
        ]);

        var system = await store.ReadAsync(AuditLogCategory.Account, ownerId: null);
        var ownerA = await store.ReadAsync(AuditLogCategory.Business, "owner-a");

        Assert.Equal("OwnerLocked", Assert.Single(system).Action);
        Assert.Equal("ContractRenewed", Assert.Single(ownerA).Action);
        Assert.True(File.Exists(store.OwnerFilePath("owner-b")));
    }

    [Fact]
    public async Task Metadata_bi_loc_truoc_khi_ghi_file()
    {
        var store = NewStore();
        await store.AppendAsync(new AuditLogFileEntry(
            Guid.NewGuid(), AuditLogCategory.Account, "Login", "user-1", null, null,
            "password=secret123 cccd=079123456789 Authorization: Bearer abc.def-123",
            DateTimeOffset.UtcNow));

        var entry = Assert.Single(await store.ReadAsync(AuditLogCategory.Account, ownerId: null));

        Assert.DoesNotContain("secret123", entry.Metadata);
        Assert.DoesNotContain("079123456789", entry.Metadata);
        Assert.DoesNotContain("abc.def-123", entry.Metadata);
    }

    [Fact]
    public async Task Ban_ghi_cu_hon_han_luu_khong_duoc_doc_va_bi_don()
    {
        var store = NewStore(retentionDays: 3);
        await store.AppendManyAsync(
        [
            Entry(AuditLogCategory.Account, "Recent", ownerId: null, occurredAt: DateTimeOffset.UtcNow.AddHours(-1)),
            Entry(AuditLogCategory.Account, "Old", ownerId: null, occurredAt: DateTimeOffset.UtcNow.AddDays(-5)),
        ]);

        Assert.Equal("Recent", Assert.Single(await store.ReadAsync(AuditLogCategory.Account, ownerId: null)).Action);

        Assert.Equal(1, await store.PruneAsync());
        Assert.Equal("Recent", Assert.Single(await store.ReadAsync(AuditLogCategory.Account, ownerId: null)).Action);
    }

    [Fact]
    public async Task Don_dep_xoa_file_khi_khong_con_dong_nao()
    {
        var store = NewStore(retentionDays: 1);
        await store.AppendAsync(
            Entry(AuditLogCategory.Business, "Old", ownerId: "owner-a", occurredAt: DateTimeOffset.UtcNow.AddDays(-2)));

        Assert.Equal(1, await store.PruneAsync());
        Assert.False(File.Exists(store.OwnerFilePath("owner-a")));
    }

    [Fact]
    public void OwnerId_co_ky_tu_la_duoc_thay_bang_gach_duoi()
    {
        var store = NewStore();

        Assert.Equal(
            Path.Combine(store.RootDirectory, "owners", "a_b_c.jsonl"),
            store.OwnerFilePath("a/b:c"));
    }

    private static AuditLogFileEntry Entry(
        AuditLogCategory category,
        string action,
        string? ownerId,
        DateTimeOffset? occurredAt = null) =>
        new(Guid.NewGuid(), category, action, "actor-1", null, ownerId, null, occurredAt ?? DateTimeOffset.UtcNow);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
