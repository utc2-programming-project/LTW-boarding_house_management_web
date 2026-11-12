using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Audit.DTOs;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Infrastructure.Auditing;

namespace NhaTro.UnitTests.Features.Audit;

public sealed class AuditLogServiceTests : IDisposable
{
    private const string OwnerA = "owner-a";
    private const string OwnerB = "owner-b";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "NhaTro_AuditServiceTests", Guid.NewGuid().ToString("N"));
    private readonly AuditLogFileStore _store;
    private readonly ApplicationDbContext _db;

    public AuditLogServiceTests()
    {
        _store = new AuditLogFileStore(_root);
        _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
    }

    private AuditLogService NewService() => new(_store, _db);

    private async Task SeedWorldAsync()
    {
        _db.Properties.AddRange(
            new Property { Id = 1, Name = "Nhà A", OwnerId = OwnerA },
            new Property { Id = 2, Name = "Nhà B", OwnerId = OwnerB });
        _db.Users.AddRange(
            new ApplicationUser { Id = OwnerA, UserName = OwnerA, FullName = "Chủ trọ A" },
            new ApplicationUser { Id = OwnerB, UserName = OwnerB, FullName = "Chủ trọ B" });
        await _db.SaveChangesAsync();

        await _store.AppendManyAsync(
        [
            Entry(AuditLogCategory.Account, "OwnerLocked", OwnerA),
            Entry(AuditLogCategory.Account, "OwnerUnlocked", OwnerA),
            Entry(AuditLogCategory.Business, "ContractRenewed", OwnerA, propertyId: 1, ownerId: OwnerA),
            Entry(AuditLogCategory.Business, "ContractLiquidated", OwnerA, propertyId: 1, ownerId: OwnerA),
            Entry(AuditLogCategory.Business, "ContractRenewed", OwnerB, propertyId: 2, ownerId: OwnerB),
        ]);
    }

    [Fact]
    public async Task Admin_chi_doc_so_tai_khoan()
    {
        await SeedWorldAsync();

        var page = await NewService().QueryAsync(new PropertyCaller("admin-1", Roles.Admin));

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, item =>
        {
            Assert.Equal("Account", item.Category);
            Assert.Null(item.PropertyId);
        });
    }

    [Fact]
    public async Task ChuTro_chi_doc_so_nghiep_vu_trong_nha_minh()
    {
        await SeedWorldAsync();

        var page = await NewService().QueryAsync(new PropertyCaller(OwnerA, Roles.ChuTro));

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items, item => Assert.Equal(1, item.PropertyId));
        Assert.Equal("Nhà A", page.Items[0].PropertyName);
        Assert.Equal("Chủ trọ A", page.Items[0].ActorDisplayName);
    }

    [Fact]
    public async Task QuanLy_va_Khach_bi_tu_choi()
    {
        await SeedWorldAsync();
        var service = NewService();

        await Assert.ThrowsAsync<AuditLogAuthorizationException>(
            () => service.QueryAsync(new PropertyCaller("manager-1", Roles.QuanLy)));
        await Assert.ThrowsAsync<AuditLogAuthorizationException>(
            () => service.QueryAsync(new PropertyCaller("tenant-1", Roles.KhachThue)));
    }

    [Fact]
    public async Task ChuTro_loc_nha_ngoai_pham_vi_bi_tu_choi()
    {
        await SeedWorldAsync();

        var error = await Assert.ThrowsAsync<AuditLogAuthorizationException>(() =>
            NewService().QueryAsync(new PropertyCaller(OwnerA, Roles.ChuTro), new AuditLogQuery(PropertyId: 2)));

        Assert.Equal(AuditLogErrorCodes.OutsideScope, error.ErrorCode);
    }

    [Fact]
    public async Task Loc_theo_nguoi_va_loai_hanh_dong()
    {
        await SeedWorldAsync();

        var byActor = await NewService().QueryAsync(
            new PropertyCaller(OwnerA, Roles.ChuTro),
            new AuditLogQuery(ActorUserId: OwnerB));
        var byAction = await NewService().QueryAsync(
            new PropertyCaller(OwnerA, Roles.ChuTro),
            new AuditLogQuery(Action: "ContractRenewed"));

        Assert.Empty(byActor.Items);
        Assert.Equal(1, byAction.TotalCount);
        Assert.All(byAction.Items, item => Assert.Equal("ContractRenewed", item.Action));
    }

    [Fact]
    public async Task Phan_trang_tra_dung_tong_va_trang_hien_tai()
    {
        await SeedWorldAsync();
        await _store.AppendManyAsync(Enumerable.Range(1, 5)
            .Select(index => Entry(AuditLogCategory.Business, $"Action{index}", OwnerA, propertyId: 1, ownerId: OwnerA))
            .ToList());

        var page = await NewService().QueryAsync(
            new PropertyCaller(OwnerA, Roles.ChuTro),
            new AuditLogQuery(Page: 2, PageSize: 3));

        Assert.Equal(7, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public async Task Bo_loc_chi_liet_ke_nguoi_va_hanh_dong_trong_so_duoc_doc()
    {
        await SeedWorldAsync();

        var options = await NewService().GetFilterOptionsAsync(new PropertyCaller(OwnerA, Roles.ChuTro));

        Assert.Equal([OwnerA], options.Actors.Select(actor => actor.UserId));
        Assert.Equal(["ContractLiquidated", "ContractRenewed"], options.Actions.OrderBy(action => action));
    }

    private static AuditLogFileEntry Entry(
        AuditLogCategory category,
        string action,
        string actorUserId,
        int? propertyId = null,
        string? ownerId = null) =>
        new(Guid.NewGuid(), category, action, actorUserId, propertyId, ownerId, null, DateTimeOffset.UtcNow);

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
