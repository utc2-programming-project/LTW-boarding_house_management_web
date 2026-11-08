using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;

namespace NhaTro.IntegrationTests.Features.Properties;

/// <summary>
/// VHT-24 — phân công Quản lý theo nhà trọ trên SQL thật: danh sách tài khoản đủ điều kiện,
/// gán/bỏ gán, và phạm vi nhà/phòng của Quản lý thay đổi theo phân công.
/// </summary>
public sealed class PropertyManagerAssignmentIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly PropertyCaller ChuTro = new("chu-tro-assign", Roles.ChuTro);
    private static readonly PropertyCaller ChuTroKhac = new("chu-tro-other", Roles.ChuTro);
    private static readonly PropertyCaller QuanLy = new("quan-ly-assign", Roles.QuanLy);

    private const string QuanLyDaDuyet = "quan-ly-active";
    private const string QuanLyChuaDuyet = "quan-ly-pending";
    private const string QuanLyBiKhoa = "quan-ly-locked";

    private PropertyManagerService NewManagerService() => new(fixture, TimeProvider.System);

    private PropertyService NewPropertyService() => new(fixture, TimeProvider.System);

    [SqlServerFact]
    public async Task QuanLy_chua_duoc_phan_cong_khong_thay_nha_va_phong()
    {
        await SeedPropertyAndRoomAsync();

        Assert.Equal(0, (await NewPropertyService().ListAsync(new PropertyFilter(), QuanLy)).TotalCount);

        await using var db = fixture.CreateContext();
        Assert.Equal(0, await RoomScope.Apply(db.Rooms, db.Properties, db.PropertyManagers, QuanLy).CountAsync());
    }

    [SqlServerFact]
    public async Task Sau_khi_phan_cong_quan_ly_thay_dung_nha_va_phong_duoc_gan()
    {
        var (propertyId, roomId) = await SeedPropertyAndRoomAsync();

        var assigned = await NewManagerService().AssignAsync(propertyId, QuanLy.UserId, ChuTro);
        Assert.True(assigned.Succeeded, assigned.ErrorMessage);

        var listed = await NewPropertyService().ListAsync(new PropertyFilter(), QuanLy);
        Assert.Equal(1, listed.TotalCount);
        Assert.Equal(propertyId, Assert.Single(listed.Items).Id);

        await using var db = fixture.CreateContext();
        var visibleRoomIds = await RoomScope.Apply(db.Rooms, db.Properties, db.PropertyManagers, QuanLy)
            .Select(room => room.Id)
            .ToListAsync();
        Assert.Equal(roomId, Assert.Single(visibleRoomIds));

        var candidates = await NewManagerService().ListAssignableManagersAsync(propertyId, ChuTro);
        Assert.True(candidates.Succeeded, candidates.ErrorMessage);
        Assert.Contains(candidates.Items, candidate => candidate.UserId == QuanLy.UserId && candidate.IsAssigned);
    }

    [SqlServerFact]
    public async Task Bo_phan_cong_thi_quan_ly_khong_con_thay_nha_va_phong()
    {
        var (propertyId, _) = await SeedPropertyAndRoomAsync();
        await NewManagerService().AssignAsync(propertyId, QuanLy.UserId, ChuTro);

        var revoked = await NewManagerService().RevokeAsync(propertyId, QuanLy.UserId, ChuTro);
        Assert.True(revoked.Succeeded, revoked.ErrorMessage);

        Assert.Equal(0, (await NewPropertyService().ListAsync(new PropertyFilter(), QuanLy)).TotalCount);

        var assignments = await NewManagerService().ListAssignmentsAsync(propertyId, ChuTro);
        Assert.Empty(assignments.Items);

        // Lịch sử vẫn giữ dòng đã bỏ gán khi bật includeHistory.
        var history = await NewManagerService().ListAssignmentsAsync(propertyId, ChuTro, includeHistory: true);
        Assert.Contains(history.Items, item => item.ManagerUserId == QuanLy.UserId && !item.IsActive);
    }

    [SqlServerFact]
    public async Task Ung_vien_phan_cong_chi_gom_quan_ly_da_duyet_va_chua_bi_khoa()
    {
        await fixture.ResetAsync();
        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, ChuTro.UserId, Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(db, QuanLy.UserId, Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(db, QuanLyDaDuyet, Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(db, QuanLyChuaDuyet, Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(db, QuanLyBiKhoa, Roles.QuanLy);

            await db.Users.Where(user => user.Id == QuanLyChuaDuyet)
                .ExecuteUpdateAsync(setters => setters.SetProperty(u => u.ApprovedAt, (DateTime?)null));
            await db.Users.Where(user => user.Id == QuanLyBiKhoa)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    u => u.LockoutEnd, AccountStatusRules.PermanentLockout));
        }

        var propertyId = await CreatePropertyAsync("Nhà chọn ứng viên");

        var result = await NewManagerService().ListAssignableManagersAsync(propertyId, ChuTro);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(
            new[] { QuanLy.UserId, QuanLyDaDuyet }.OrderBy(id => id, StringComparer.Ordinal),
            result.Items.Select(item => item.UserId).OrderBy(id => id, StringComparer.Ordinal));
        Assert.All(result.Items, item => Assert.False(item.IsAssigned));
    }

    [SqlServerFact]
    public async Task Chu_tro_khac_khong_the_phan_cong_nha_khong_cua_minh()
    {
        var (propertyId, _) = await SeedPropertyAndRoomAsync();

        var assigned = await NewManagerService().AssignAsync(propertyId, QuanLy.UserId, ChuTroKhac);
        Assert.False(assigned.Succeeded);
        Assert.Equal(PropertyManagerErrorCodes.PropertyNotFound, assigned.ErrorCode);

        var candidates = await NewManagerService().ListAssignableManagersAsync(propertyId, ChuTroKhac);
        Assert.False(candidates.Succeeded);
        Assert.Equal(PropertyManagerErrorCodes.PropertyNotFound, candidates.ErrorCode);
    }

    [SqlServerFact]
    public async Task QuanLy_khong_the_tu_phan_cong_hoac_doc_ung_vien()
    {
        var (propertyId, _) = await SeedPropertyAndRoomAsync();

        var assigned = await NewManagerService().AssignAsync(propertyId, QuanLyDaDuyet, QuanLy);
        Assert.False(assigned.Succeeded);
        Assert.Equal(PropertyManagerErrorCodes.Forbidden, assigned.ErrorCode);

        var candidates = await NewManagerService().ListAssignableManagersAsync(propertyId, QuanLy);
        Assert.False(candidates.Succeeded);
        Assert.Equal(PropertyManagerErrorCodes.Forbidden, candidates.ErrorCode);
    }

    [SqlServerFact]
    public async Task Gan_lai_quan_ly_dang_phan_cong_bi_chan_Trung_phan_cong()
    {
        var (propertyId, _) = await SeedPropertyAndRoomAsync();
        await NewManagerService().AssignAsync(propertyId, QuanLy.UserId, ChuTro);

        var again = await NewManagerService().AssignAsync(propertyId, QuanLy.UserId, ChuTro);
        Assert.False(again.Succeeded);
        Assert.Equal(PropertyManagerErrorCodes.AlreadyAssigned, again.ErrorCode);
    }

    private async Task<(int PropertyId, int RoomId)> SeedPropertyAndRoomAsync()
    {
        await fixture.ResetAsync();
        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, ChuTro.UserId, Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(db, ChuTroKhac.UserId, Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(db, QuanLy.UserId, Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(db, QuanLyDaDuyet, Roles.QuanLy);
        }

        var propertyId = await CreatePropertyAsync("Nhà phân công Quản lý");

        await using var roomDb = fixture.CreateContext();
        var room = new Room
        {
            PropertyId = propertyId,
            Code = "A101",
            Floor = 1,
            AreaM2 = 25m,
            StandardPrice = 3_000_000m,
            Status = RoomStatus.Available,
        };
        roomDb.Rooms.Add(room);
        await roomDb.SaveChangesAsync();

        return (propertyId, room.Id);
    }

    private async Task<int> CreatePropertyAsync(string name)
    {
        var created = await NewPropertyService().CreateAsync(
            new PropertySave { Name = name, Address = "1 Đường Test" },
            ChuTro);

        Assert.True(created.Succeeded, created.ErrorMessage);
        return created.Property!.Id;
    }
}
