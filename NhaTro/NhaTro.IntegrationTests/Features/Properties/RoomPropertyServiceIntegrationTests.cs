using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Enums;

namespace NhaTro.IntegrationTests.Features.Properties;

/// <summary>
/// P2 — integration test trên SQL Server thật, chứng minh các tiêu chí nghiệm thu:
/// lưu rồi tải lại đúng; chặn mã phòng trùng theo nhà; chặn giá sai; chặn truy cập ngoài phạm vi.
/// </summary>
public sealed class RoomPropertyServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly PropertyCaller ChuTro = new("chu-tro-1", Roles.ChuTro);
    private static readonly PropertyCaller ChuTroKhac = new("chu-tro-2", Roles.ChuTro);
    private static readonly PropertyCaller QuanLy = new("quan-ly-1", Roles.QuanLy);

    private static RoomService NewRoomService(ApplicationDbContext db, IRoomChangePolicy? policy = null) =>
        new(
            CreateContextFactory(db),
            TimeProvider.System,
            policy ?? new RoomChangePolicyStub(),
            new NoActiveContractRoomUsageChecker());

    private static PropertyService NewPropertyService(ApplicationDbContext db) =>
        new(CreateContextFactory(db), TimeProvider.System);

    private static IDbContextFactory<ApplicationDbContext> CreateContextFactory(ApplicationDbContext db) =>
        new TestDbContextFactory(db.Database.GetConnectionString()
            ?? throw new InvalidOperationException("The test database has no connection string."));

    private sealed class TestDbContextFactory(string connectionString) : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(connectionString)
                .Options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    // ---------- Tiêu chí 1: lưu rồi tải lại đúng ----------

    [SqlServerFact]
    public async Task Luu_nha_tro_roi_tai_lai_dung()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();

        var created = await NewPropertyService(db).CreateAsync(new PropertySave
        {
            Name = "  Trọ An Bình  ",
            Address = " 12 Nguyễn Trãi ",
            IsActive = true,
        }, ChuTro);

        Assert.True(created.Succeeded, created.ErrorMessage);
        Assert.True(created.Property!.Id > 0);
        Assert.Equal("Trọ An Bình", created.Property.Name);
        Assert.Equal("12 Nguyễn Trãi", created.Property.Address);
        Assert.Equal(ChuTro.UserId, created.Property.OwnerId);

        // Đọc lại bằng context mới (không dùng cache của context vừa ghi).
        await using var reloadDb = fixture.CreateContext();
        var reloaded = await NewPropertyService(reloadDb).GetAsync(created.Property.Id, ChuTro);

        Assert.True(reloaded.Succeeded, reloaded.ErrorMessage);
        Assert.Equal("Trọ An Bình", reloaded.Property!.Name);
        Assert.Equal(ChuTro.UserId, reloaded.Property.OwnerId);
        Assert.True(reloaded.Property.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [SqlServerFact]
    public async Task Luu_phong_roi_tai_lai_dung_moi_truong()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db)
            .CreateAsync(new PropertySave { Name = "Trọ An Bình" }, ChuTro);

        var created = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id,
            Code = "  P101  ",
            Floor = 1,
            AreaM2 = 20.5m,
            StandardPrice = 2_500_000m,
            Status = RoomStatus.Available,
        }, ChuTro);

        Assert.True(created.Succeeded, created.ErrorMessage);
        var room = created.Room!;
        Assert.True(room.Id > 0);
        Assert.Equal("P101", room.Code);
        Assert.Equal(1, room.Floor);
        Assert.Equal(20.5m, room.AreaM2);
        Assert.Equal(2_500_000m, room.StandardPrice);
        Assert.Equal(RoomStatus.Available, room.Status);
        Assert.Equal("Trọ An Bình", room.PropertyName);

        await using var reloadDb = fixture.CreateContext();
        var reloaded = await NewRoomService(reloadDb).GetAsync(room.Id, ChuTro);

        Assert.True(reloaded.Succeeded, reloaded.ErrorMessage);
        Assert.Equal("P101", reloaded.Room!.Code);
        Assert.Equal(2_500_000m, reloaded.Room.StandardPrice);
        Assert.Equal(RoomStatus.Available, reloaded.Room.Status);
    }

    [SqlServerFact]
    public async Task Sua_gia_va_trang_thai_roi_doc_lai_dung()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db)
            .CreateAsync(new PropertySave { Name = "Trọ An Bình" }, ChuTro);
        var created = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id,
            Code = "P101",
            StandardPrice = 2_500_000m,
        }, ChuTro);

        var updated = await NewRoomService(db).UpdateAsync(created.Room!.Id, new RoomSave
        {
            PropertyId = property.Property.Id,
            Code = "P101",
            StandardPrice = 3_200_000m,
            Status = RoomStatus.Maintenance,
        }, ChuTro);

        Assert.True(updated.Succeeded, updated.ErrorMessage);
        Assert.Equal(3_200_000m, updated.Room!.StandardPrice);
        Assert.Equal(RoomStatus.Maintenance, updated.Room.Status);
        Assert.NotNull(updated.Room.UpdatedAt);

        await using var reloadDb = fixture.CreateContext();
        var reloaded = await NewRoomService(reloadDb).GetAsync(created.Room.Id, ChuTro);
        Assert.Equal(3_200_000m, reloaded.Room!.StandardPrice);
        Assert.Equal(RoomStatus.Maintenance, reloaded.Room.Status);
    }

    // ---------- Tiêu chí 2: chặn mã trùng theo quy tắc đã chốt ----------

    [SqlServerFact]
    public async Task Ma_phong_trung_trong_cung_nha_bi_chan()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db)
            .CreateAsync(new PropertySave { Name = "Trọ An Bình" }, ChuTro);
        var propertyId = property.Property!.Id;

        var first = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = propertyId, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);
        Assert.True(first.Succeeded, first.ErrorMessage);

        var duplicate = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = propertyId, Code = " P101 ", StandardPrice = 2_000_000m,
        }, ChuTro);

        Assert.False(duplicate.Succeeded);
        Assert.Equal(RoomErrorCodes.DuplicateRoomCode, duplicate.ErrorCode);
    }

    [SqlServerFact]
    public async Task Cung_ma_phong_o_nha_khac_duoc_phep()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var a = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var b = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ B" }, ChuTro);

        var roomA = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = a.Property!.Id, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);
        var roomB = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = b.Property!.Id, Code = "P101", StandardPrice = 1_500_000m,
        }, ChuTro);

        Assert.True(roomA.Succeeded, roomA.ErrorMessage);
        Assert.True(roomB.Succeeded, roomB.ErrorMessage);
    }

    [SqlServerFact]
    public async Task Sua_ma_phong_trung_phong_khac_bi_chan()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var propertyId = property.Property!.Id;

        var service = NewRoomService(db);
        await service.CreateAsync(new RoomSave { PropertyId = propertyId, Code = "P101", StandardPrice = 1_000_000m }, ChuTro);
        var second = await service.CreateAsync(new RoomSave { PropertyId = propertyId, Code = "P102", StandardPrice = 1_000_000m }, ChuTro);

        var updated = await service.UpdateAsync(second.Room!.Id, new RoomSave
        {
            PropertyId = propertyId, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);

        Assert.False(updated.Succeeded);
        Assert.Equal(RoomErrorCodes.DuplicateRoomCode, updated.ErrorCode);
    }

    // ---------- Tiêu chí 3: chặn giá sai ----------

    [SqlServerFact]
    public async Task Gia_am_bi_chan_va_khong_luu()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);

        var created = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = -1,
        }, ChuTro);

        Assert.False(created.Succeeded);
        Assert.Equal(RoomErrorCodes.InvalidRequest, created.ErrorCode);

        await using var checkDb = fixture.CreateContext();
        Assert.False(await checkDb.Rooms.AnyAsync());
    }

    [SqlServerFact]
    public async Task Gia_qua_lon_bi_chan()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);

        var created = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id,
            Code = "P101",
            StandardPrice = 10_000_000_000_000_000m, // > decimal(18,2)
        }, ChuTro);

        Assert.False(created.Succeeded);
        Assert.Equal(RoomErrorCodes.InvalidRequest, created.ErrorCode);
    }

    [SqlServerFact]
    public async Task Gia_nhieu_hon_2_so_le_duoc_lam_tron()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);

        var created = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = 1_000_000.555m,
        }, ChuTro);

        Assert.True(created.Succeeded, created.ErrorMessage);
        Assert.Equal(1_000_000.56m, created.Room!.StandardPrice);
    }

    // ---------- Tiêu chí 4: chặn truy cập ngoài phạm vi ----------

    [SqlServerFact]
    public async Task ChuTro_khac_khong_doc_duoc_nha_va_phong()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var room = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);

        var otherProperty = await NewPropertyService(db).GetAsync(property.Property.Id, ChuTroKhac);
        Assert.Equal(PropertyErrorCodes.PropertyNotFound, otherProperty.ErrorCode);

        var otherRoom = await NewRoomService(db).GetAsync(room.Room!.Id, ChuTroKhac);
        Assert.Equal(RoomErrorCodes.RoomNotFound, otherRoom.ErrorCode);

        var otherUpdate = await NewRoomService(db).UpdateAsync(room.Room.Id, new RoomSave
        {
            PropertyId = property.Property.Id, Code = "P101", StandardPrice = 9_000_000m,
        }, ChuTroKhac);
        Assert.Equal(RoomErrorCodes.RoomNotFound, otherUpdate.ErrorCode);

        // Danh sách cũng bị lọc theo phạm vi.
        var list = await NewPropertyService(db).ListAsync(new PropertyFilter(), ChuTroKhac);
        Assert.Equal(0, list.TotalCount);
    }

    [SqlServerFact]
    public async Task Khong_tao_duoc_phong_trong_nha_nguoi_khac()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);

        var created = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTroKhac);

        Assert.Equal(RoomErrorCodes.PropertyNotFound, created.ErrorCode);
    }

    [SqlServerFact]
    public async Task Admin_va_khach_thue_bi_chan_khoi_nghiep_vu_p2()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var admin = new PropertyCaller("admin-1", Roles.Admin);
        var khach = new PropertyCaller("khach-1", Roles.KhachThue);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewPropertyService(db).ListAsync(new PropertyFilter(), admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewRoomService(db).ListAsync(new RoomFilter(), khach));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewRoomService(db).CreateAsync(new RoomSave { PropertyId = 1, Code = "P1" }, admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewRoomService(db).DeleteAsync(1, khach));
    }

    [SqlServerFact]
    public async Task Quan_ly_tao_nha_bi_chan_va_sua_phong_qua_cong_tac()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var room = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);

        // Ma trận P2: không có dòng tạo nhà trọ/phòng cho Quản lý.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ B" }, QuanLy));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewRoomService(db).CreateAsync(new RoomSave { PropertyId = property.Property.Id, Code = "P102" }, QuanLy));

        // Mặc định công tắc là Duyệt: Quản lý đổi giá bị trả ApprovalRequired, chưa ghi.
        var update = await NewRoomService(db).UpdateAsync(room.Room!.Id, new RoomSave
        {
            PropertyId = property.Property.Id, Code = "P101", StandardPrice = 2_000_000m,
        }, QuanLy);

        Assert.False(update.Succeeded);
        Assert.Equal(RoomErrorCodes.ApprovalRequired, update.ErrorCode);

        await using var checkDb = fixture.CreateContext();
        var unchanged = await checkDb.Rooms.SingleAsync();
        Assert.Equal(1_000_000m, unchanged.StandardPrice);
    }

    [SqlServerFact]
    public async Task Quan_ly_sua_phong_khi_cong_tac_cho_phep()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var room = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);

        var updated = await NewRoomService(db, new AllowAllPolicy()).UpdateAsync(room.Room!.Id, new RoomSave
        {
            PropertyId = property.Property.Id, Code = "P101", StandardPrice = 2_000_000m,
        }, QuanLy);

        Assert.True(updated.Succeeded, updated.ErrorMessage);
        Assert.Equal(2_000_000m, updated.Room!.StandardPrice);
    }

    // ---------- Thùng rác (D: xóa mềm, chặn khi còn hợp đồng) ----------

    [SqlServerFact]
    public async Task Xoa_mem_an_khoi_query_va_khoi_phuc_duoc()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var room = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);

        var deleted = await NewRoomService(db).DeleteAsync(room.Room!.Id, ChuTro);
        Assert.True(deleted.Succeeded, deleted.ErrorMessage);

        await using var afterDb = fixture.CreateContext();
        Assert.False(await afterDb.Rooms.AnyAsync());
        Assert.Equal(0, (await NewRoomService(afterDb).ListAsync(new RoomFilter(), ChuTro)).TotalCount);
        Assert.True(await afterDb.Rooms.IgnoreQueryFilters().AnyAsync(r => r.IsDeleted));

        // Tạo lại cùng mã được vì phòng cũ đã vào thùng rác.
        var reused = await NewRoomService(afterDb).CreateAsync(new RoomSave
        {
            PropertyId = property.Property.Id, Code = "P101", StandardPrice = 1_500_000m,
        }, ChuTro);
        Assert.True(reused.Succeeded, reused.ErrorMessage);

        // Khôi phục khi mã đã bị dùng lại => chặn.
        var conflict = await NewRoomService(afterDb).RestoreAsync(room.Room.Id, ChuTro);
        Assert.Equal(RoomErrorCodes.DuplicateRoomCode, conflict.ErrorCode);

        // Xóa phòng mới rồi khôi phục phòng cũ thành công.
        await NewRoomService(afterDb).DeleteAsync(reused.Room!.Id, ChuTro);
        var restored = await NewRoomService(afterDb).RestoreAsync(room.Room.Id, ChuTro);
        Assert.True(restored.Succeeded, restored.ErrorMessage);
    }

    [SqlServerFact]
    public async Task Xoa_phong_con_hop_dong_hieu_luc_bi_chan()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var room = await NewRoomService(db).CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);

        var service = new RoomService(
            CreateContextFactory(db), TimeProvider.System, new RoomChangePolicyStub(), new AlwaysInUseChecker());

        var deleted = await service.DeleteAsync(room.Room!.Id, ChuTro);

        Assert.False(deleted.Succeeded);
        Assert.Equal(RoomErrorCodes.RoomInUse, deleted.ErrorCode);
    }

    // ---------- Dữ liệu chọn phòng (bàn giao Đào) ----------

    [SqlServerFact]
    public async Task Du_lieu_chon_phong_chi_tra_phong_trong_trong_pham_vi()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var a = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var b = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ B" }, ChuTroKhac);

        var service = NewRoomService(db);
        await service.CreateAsync(new RoomSave { PropertyId = a.Property!.Id, Code = "P101", StandardPrice = 1_000_000m }, ChuTro);
        await service.CreateAsync(new RoomSave { PropertyId = a.Property.Id, Code = "P102", StandardPrice = 1_200_000m, Status = RoomStatus.Rented }, ChuTro);
        await service.CreateAsync(new RoomSave { PropertyId = b.Property!.Id, Code = "P101", StandardPrice = 2_000_000m }, ChuTroKhac);

        var options = await service.SearchForPickerAsync(new RoomOptionRequest(), ChuTro);

        var option = Assert.Single(options);
        Assert.Equal("P101", option.Code);
        Assert.Equal("Trọ A", option.PropertyName);
        Assert.Equal(1_000_000m, option.StandardPrice);

        // Có cờ lấy cả phòng đang thuê.
        var all = await service.SearchForPickerAsync(new RoomOptionRequest { IncludeUnavailable = true }, ChuTro);
        Assert.Equal(2, all.Count);
    }

    [SqlServerFact]
    public async Task Phan_trang_va_loc_gia_tra_dung()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var service = NewRoomService(db);
        for (var i = 1; i <= 5; i++)
        {
            await service.CreateAsync(new RoomSave
            {
                PropertyId = property.Property!.Id,
                Code = $"P10{i}",
                StandardPrice = 1_000_000m * i,
            }, ChuTro);
        }

        var page = await service.ListAsync(new RoomFilter { Page = 2, PageSize = 2 }, ChuTro);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal("P103", page.Items[0].Code);

        var filtered = await service.ListAsync(new RoomFilter { MinPrice = 3_000_000m }, ChuTro);
        Assert.Equal(3, filtered.TotalCount);
    }

    // ---------- B16-03: phòng ngừng hoạt động không được tính là phòng trống ----------

    [SqlServerFact]
    public async Task Tong_quan_va_loc_phong_trong_bo_qua_phong_inactive()
    {
        await SeedAsync();
        await using var db = fixture.CreateContext();
        var property = await NewPropertyService(db).CreateAsync(new PropertySave { Name = "Trọ A" }, ChuTro);
        var service = NewRoomService(db);
        await service.CreateAsync(new RoomSave
        {
            PropertyId = property.Property!.Id, Code = "P101", StandardPrice = 1_000_000m,
        }, ChuTro);
        await service.CreateAsync(new RoomSave
        {
            PropertyId = property.Property.Id, Code = "P102", StandardPrice = 1_000_000m, IsActive = false,
        }, ChuTro);

        var summary = await service.GetSummaryAsync(ChuTro);
        Assert.Equal(2, summary.Total);
        Assert.Equal(1, summary.Available);

        var onlyAvailable = await service.ListAsync(new RoomFilter { OnlyAvailable = true }, ChuTro);
        var room = Assert.Single(onlyAvailable.Items);
        Assert.Equal("P101", room.Code);
    }

    private async Task SeedAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, ChuTro.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ChuTroKhac.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, QuanLy.UserId, Roles.QuanLy);
    }

    private sealed class AllowAllPolicy : IRoomChangePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(RoomChangeRequest request, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.Allowed);
    }

    private sealed class AlwaysInUseChecker : IRoomUsageChecker
    {
        public Task<bool> HasActiveContractAsync(int roomId, CancellationToken ct = default) =>
            Task.FromResult(true);
    }
}
