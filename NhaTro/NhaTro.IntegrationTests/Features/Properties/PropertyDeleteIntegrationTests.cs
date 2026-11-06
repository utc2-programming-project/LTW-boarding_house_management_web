using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;

namespace NhaTro.IntegrationTests.Features.Properties;

/// <summary>
/// Xóa mềm nhà trọ: ẩn khỏi danh sách nhưng vẫn còn dòng trong DB, chặn khi nhà còn phòng
/// chưa vào thùng rác, và chỉ Chủ trọ mới xóa được.
/// </summary>
public sealed class PropertyDeleteIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly PropertyCaller ChuTro = new("chu-tro-delete", Roles.ChuTro);
    private static readonly PropertyCaller ChuTroKhac = new("chu-tro-other", Roles.ChuTro);
    private static readonly PropertyCaller QuanLy = new("quan-ly-delete", Roles.QuanLy);

    private PropertyService NewPropertyService() => new(fixture, TimeProvider.System);

    private RoomService NewRoomService() =>
        new(fixture, TimeProvider.System, new AllowAllPolicy(), new NoActiveContract());

    [SqlServerFact]
    public async Task Xoa_nha_trong_thi_an_khoi_danh_sach_nhung_van_con_dong_du_lieu()
    {
        var propertyId = await CreatePropertyAsync("Nhà trống để xóa");

        var deleted = await NewPropertyService().DeleteAsync(propertyId, ChuTro);
        Assert.True(deleted.Succeeded, deleted.ErrorMessage);

        Assert.Equal(0, (await NewPropertyService().ListAsync(new PropertyFilter(), ChuTro)).TotalCount);
        Assert.False((await NewPropertyService().GetAsync(propertyId, ChuTro)).Succeeded);

        await using var db = fixture.CreateContext();
        var row = await db.Properties.IgnoreQueryFilters().AsNoTracking().FirstAsync(p => p.Id == propertyId);
        Assert.True(row.IsDeleted);
    }

    [SqlServerFact]
    public async Task Nha_con_phong_thi_b_chan_xoa_va_van_hien_danh_sach()
    {
        var propertyId = await CreatePropertyAsync("Nhà còn phòng");
        await AddRoomAsync(propertyId, "D101");

        var deleted = await NewPropertyService().DeleteAsync(propertyId, ChuTro);
        Assert.False(deleted.Succeeded);
        Assert.Equal(PropertyErrorCodes.PropertyHasRooms, deleted.ErrorCode);

        Assert.Equal(propertyId, Assert.Single((await NewPropertyService().ListAsync(new PropertyFilter(), ChuTro)).Items).Id);

        await using var db = fixture.CreateContext();
        Assert.False(await db.Properties.IgnoreQueryFilters().AnyAsync(p => p.Id == propertyId && p.IsDeleted));
    }

    [SqlServerFact]
    public async Task Nha_chi_con_phong_trong_thung_rac_thi_xoa_duoc_nhung_khong_khoi_phong_duoc()
    {
        var propertyId = await CreatePropertyAsync("Nhà chỉ còn phòng thùng rác");
        var roomId = await AddRoomAsync(propertyId, "D201", isDeleted: true);

        var deleted = await NewPropertyService().DeleteAsync(propertyId, ChuTro);
        Assert.True(deleted.Succeeded, deleted.ErrorMessage);

        await using var db = fixture.CreateContext();
        // Trang thùng rác dùng IgnoreQueryFilters nên bộ lọc nhà trọ cũng bị tắt: phòng vẫn còn đó.
        Assert.True(await db.Rooms.IgnoreQueryFilters().AnyAsync(r => r.PropertyId == propertyId));

        // Khôi phục phải bị chặn, nếu không sẽ có phòng không trang nào hiển thị được.
        var restored = await NewRoomService().RestoreAsync(roomId, ChuTro);
        Assert.False(restored.Succeeded);
        Assert.Equal(RoomErrorCodes.PropertyDeleted, restored.ErrorCode);
        Assert.True(await db.Rooms.IgnoreQueryFilters().AnyAsync(r => r.Id == roomId && r.IsDeleted));
    }

    [SqlServerFact]
    public async Task Quan_ly_khong_the_xoa_nha_ke_ca_nha_duoc_phan_cong()
    {
        var propertyId = await CreatePropertyAsync("Nhà Quản lý không xóa được");
        await using (var db = fixture.CreateContext())
        {
            db.PropertyManagers.Add(new Web.Features.Properties.Entities.PropertyManager
            {
                PropertyId = propertyId,
                ManagerUserId = QuanLy.UserId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = ChuTro.UserId,
            });
            await db.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewPropertyService().DeleteAsync(propertyId, QuanLy));

        Assert.Equal(1, (await NewPropertyService().ListAsync(new PropertyFilter(), QuanLy)).TotalCount);
    }

    [SqlServerFact]
    public async Task Xoa_nha_cua_chu_tro_khac_bi_tre_khong_thay_va_khong_an_duoc()
    {
        var propertyId = await CreatePropertyAsync("Nhà của Chủ trọ khác");

        var deleted = await NewPropertyService().DeleteAsync(propertyId, ChuTroKhac);
        Assert.False(deleted.Succeeded);
        Assert.Equal(PropertyErrorCodes.PropertyNotFound, deleted.ErrorCode);

        Assert.Equal(propertyId, Assert.Single((await NewPropertyService().ListAsync(new PropertyFilter(), ChuTro)).Items).Id);
    }

    [SqlServerFact]
    public async Task Thung_rac_liet_ke_nha_da_xoa_kem_so_phong_cho_khoi_phuc()
    {
        var propertyId = await CreatePropertyAsync("Nhà vào thùng rác");
        await AddRoomAsync(propertyId, "D301", isDeleted: true);
        Assert.True((await NewPropertyService().DeleteAsync(propertyId, ChuTro)).Succeeded);

        var trash = await NewPropertyService().ListDeletedAsync(new PropertyFilter(), ChuTro);
        Assert.Equal(1, trash.TotalCount);
        var item = Assert.Single(trash.Items);
        Assert.Equal(propertyId, item.Id);
        Assert.Equal("Nhà vào thùng rác", item.Name);
        Assert.Equal(1, item.TrashedRoomCount);
        Assert.NotNull(item.DeletedAt);

        Assert.Equal(0, (await NewPropertyService().ListAsync(new PropertyFilter(), ChuTro)).TotalCount);
    }

    [SqlServerFact]
    public async Task Khoi_phuc_nha_lam_phong_trong_thung_rac_khoi_phuc_duoc_theo()
    {
        var propertyId = await CreatePropertyAsync("Nhà khôi phục");
        var roomId = await AddRoomAsync(propertyId, "D401", isDeleted: true);
        await NewPropertyService().DeleteAsync(propertyId, ChuTro);

        // Khi nhà còn trong thùng rác thì phòng của nó chưa khôi phục được.
        var blocked = await NewRoomService().RestoreAsync(roomId, ChuTro);
        Assert.False(blocked.Succeeded);
        Assert.Equal(RoomErrorCodes.PropertyDeleted, blocked.ErrorCode);

        var restored = await NewPropertyService().RestoreAsync(propertyId, ChuTro);
        Assert.True(restored.Succeeded, restored.ErrorMessage);

        Assert.Equal(propertyId, Assert.Single((await NewPropertyService().ListAsync(new PropertyFilter(), ChuTro)).Items).Id);
        Assert.Equal(0, (await NewPropertyService().ListDeletedAsync(new PropertyFilter(), ChuTro)).TotalCount);

        var roomRestored = await NewRoomService().RestoreAsync(roomId, ChuTro);
        Assert.True(roomRestored.Succeeded, roomRestored.ErrorMessage);

        await using var db = fixture.CreateContext();
        var visibleRoomIds = await RoomScope.Apply(db.Rooms, db.Properties, db.PropertyManagers, ChuTro)
            .Select(room => room.Id)
            .ToListAsync();
        Assert.Equal(roomId, Assert.Single(visibleRoomIds));
    }

    [SqlServerFact]
    public async Task Quan_ly_khong_doc_duoc_danh_sach_thung_rac_nha_tro()
    {
        await CreatePropertyAsync("Nhà thùng rác của Chủ trọ");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewPropertyService().ListDeletedAsync(new PropertyFilter(), QuanLy));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewPropertyService().RestoreAsync(1, QuanLy));
    }

    [SqlServerFact]
    public async Task Chu_tro_khac_khong_khoi_phuc_nha_dang_trong_thung_rac_cua_nguoi_khac()
    {
        var propertyId = await CreatePropertyAsync("Nhà thùng rác người khác");
        await NewPropertyService().DeleteAsync(propertyId, ChuTro);

        Assert.Equal(0, (await NewPropertyService().ListDeletedAsync(new PropertyFilter(), ChuTroKhac)).TotalCount);

        var restored = await NewPropertyService().RestoreAsync(propertyId, ChuTroKhac);
        Assert.False(restored.Succeeded);
        Assert.Equal(PropertyErrorCodes.PropertyNotFound, restored.ErrorCode);

        Assert.Equal(1, (await NewPropertyService().ListDeletedAsync(new PropertyFilter(), ChuTro)).TotalCount);
    }

    private async Task SeedAccountsAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, ChuTro.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ChuTroKhac.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, QuanLy.UserId, Roles.QuanLy);
    }    private async Task<int> CreatePropertyAsync(string name)
    {
        await SeedAccountsAsync();

        var created = await NewPropertyService().CreateAsync(
            new PropertySave { Name = name, Address = "2 Đường Test" }, ChuTro);

        Assert.True(created.Succeeded, created.ErrorMessage);
        return created.Property!.Id;
    }

    private async Task<int> AddRoomAsync(int propertyId, string code, bool isDeleted = false)
    {
        await using var db = fixture.CreateContext();
        var room = new Room
        {
            PropertyId = propertyId,
            Code = code,
            Floor = 1,
            AreaM2 = 25m,
            StandardPrice = 3_000_000m,
            Status = RoomStatus.Available,
            IsDeleted = isDeleted,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room.Id;
    }

    private sealed class AllowAllPolicy : IRoomChangePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(RoomChangeRequest request, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.Allowed);
    }

    private sealed class NoActiveContract : IRoomUsageChecker
    {
        public Task<bool> HasActiveContractAsync(int roomId, CancellationToken ct = default) =>
            Task.FromResult(false);
    }
}
