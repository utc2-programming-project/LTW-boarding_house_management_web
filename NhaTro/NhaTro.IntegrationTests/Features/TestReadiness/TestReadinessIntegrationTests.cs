using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Enums;

namespace NhaTro.IntegrationTests.Features.TestReadiness;

/// <summary>
/// VHT-14 (đợt 1A): chứng minh bộ fixture chạy trên DB test riêng và có đủ 3 loại case nền:
/// thành công, sai dữ liệu, sai quyền. Phần công tắc chạy đủ 3 chế độ Cho phép / Duyệt / Không thể.
/// </summary>
public sealed class TestReadinessIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task Fixture_gieo_du_4_vai_tro_va_hai_pham_vi()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        Assert.Equal(4, await db.Roles.CountAsync());
        Assert.Equal(5, await db.Users.CountAsync());
        Assert.Equal(2, await db.Properties.CountAsync());
        Assert.Equal(2, await db.Rooms.CountAsync());
        Assert.Equal(1, await db.Tenants.CountAsync());

        var users = await db.Users
            .Where(u => u.Email != null)
            .Select(u => u.Email!)
            .ToListAsync();
        Assert.Contains(TestDataSeeder.AdminEmail, users);
        Assert.Contains(TestDataSeeder.ChuTroAEmail, users);
        Assert.Contains(TestDataSeeder.ChuTroBEmail, users);
        Assert.Contains(TestDataSeeder.QuanLyEmail, users);
        Assert.Contains(TestDataSeeder.KhachThueEmail, users);

        // Hai nhà trọ thuộc hai chủ trọ khác nhau để test chéo phạm vi.
        var propertyA = await db.Properties.SingleAsync(p => p.Id == ids.PropertyAId);
        var propertyB = await db.Properties.SingleAsync(p => p.Id == ids.PropertyBId);
        Assert.NotEqual(propertyA.OwnerId, propertyB.OwnerId);
    }

    [SqlServerFact]
    public async Task ChuTroA_chi_thay_du_lieu_trong_pham_vi_cua_minh()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var propertyService = new PropertyService(fixture, TimeProvider.System);
        var roomService = new RoomService(
            fixture,
            TimeProvider.System,
            new FixedRoomChangePolicy(RoomChangeDecision.Allowed),
            new NoActiveContractRoomUsageChecker());

        var callerA = new PropertyCaller(ids.ChuTroAId, Roles.ChuTro);

        var properties = await propertyService.ListAsync(new PropertyFilter(), callerA);
        Assert.Single(properties.Items);
        Assert.Equal(TestDataSeeder.PropertyAName, properties.Items[0].Name);

        var rooms = await roomService.ListAsync(new RoomFilter(), callerA);
        Assert.Single(rooms.Items);
        Assert.Equal(TestDataSeeder.RoomACode, rooms.Items[0].Code);

        // Xem chéo nhà/phòng của chủ trọ B phải bị coi như không tồn tại.
        var otherProperty = await propertyService.GetAsync(ids.PropertyBId, callerA);
        Assert.False(otherProperty.Succeeded);
        Assert.Equal(PropertyErrorCodes.PropertyNotFound, otherProperty.ErrorCode);

        var otherRoom = await roomService.GetAsync(ids.RoomBId, callerA);
        Assert.False(otherRoom.Succeeded);
        Assert.Equal(RoomErrorCodes.RoomNotFound, otherRoom.ErrorCode);
    }

    [SqlServerFact]
    public async Task Du_lieu_sai_tra_ma_loi_nghiep_vu()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var propertyService = new PropertyService(fixture, TimeProvider.System);
        var roomService = new RoomService(
            fixture,
            TimeProvider.System,
            new FixedRoomChangePolicy(RoomChangeDecision.Allowed),
            new NoActiveContractRoomUsageChecker());

        var chuTroA = new PropertyCaller(ids.ChuTroAId, Roles.ChuTro);

        var invalidProperty = await propertyService.CreateAsync(new PropertySave { Name = "   " }, chuTroA);
        Assert.False(invalidProperty.Succeeded);
        Assert.Equal(PropertyErrorCodes.InvalidRequest, invalidProperty.ErrorCode);

        var duplicateRoom = await roomService.CreateAsync(new RoomSave
        {
            PropertyId = ids.PropertyAId,
            Code = TestDataSeeder.RoomACode,
            StandardPrice = 1_000_000m,
            Status = RoomStatus.Available,
        }, chuTroA);
        Assert.False(duplicateRoom.Succeeded);
        Assert.Equal(RoomErrorCodes.DuplicateRoomCode, duplicateRoom.ErrorCode);

        // Đổi phòng sang nhà trọ ngoài phạm vi cũng phải bị chặn.
        var moveOutOfScope = await roomService.UpdateAsync(ids.RoomAId, new RoomSave
        {
            PropertyId = ids.PropertyBId,
            Code = TestDataSeeder.RoomACode,
            StandardPrice = TestDataSeeder.RoomAStandardPrice,
            Status = RoomStatus.Available,
        }, chuTroA);
        Assert.False(moveOutOfScope.Succeeded);
        Assert.Equal(RoomErrorCodes.PropertyNotFound, moveOutOfScope.ErrorCode);
    }

    [SqlServerFact]
    public async Task KhachThue_khong_duoc_goi_service_quan_ly()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var propertyService = new PropertyService(fixture, TimeProvider.System);
        var roomService = new RoomService(
            fixture,
            TimeProvider.System,
            new FixedRoomChangePolicy(RoomChangeDecision.Allowed),
            new NoActiveContractRoomUsageChecker());

        var khachThue = new PropertyCaller(ids.KhachThueId, Roles.KhachThue);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => propertyService.ListAsync(new PropertyFilter(), khachThue));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => roomService.ListAsync(new RoomFilter(), khachThue));
    }

    [SqlServerFact]
    public async Task CongTac_ChoPhep_thi_cap_nhat_thanh_cong()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var service = NewRoomService(fixture, RoomChangeDecision.Allowed);
        var quanLy = new PropertyCaller(ids.QuanLyId, Roles.QuanLy);

        var result = await service.UpdateAsync(ids.RoomAId, RoomUpdate(ids.PropertyAId, 3_500_000m), quanLy);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(3_500_000m, result.Room!.StandardPrice);
        Assert.Equal(
            3_500_000m,
            await db.Rooms.Where(r => r.Id == ids.RoomAId).Select(r => r.StandardPrice).SingleAsync());
    }

    [SqlServerFact]
    public async Task CongTac_Duyet_thi_khong_ghi_va_tra_ApprovalRequired()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var service = NewRoomService(fixture, RoomChangeDecision.RequiresApproval);
        var quanLy = new PropertyCaller(ids.QuanLyId, Roles.QuanLy);

        var result = await service.UpdateAsync(ids.RoomAId, RoomUpdate(ids.PropertyAId, 3_500_000m), quanLy);

        Assert.False(result.Succeeded);
        Assert.Equal(RoomErrorCodes.ApprovalRequired, result.ErrorCode);
        Assert.Equal(
            TestDataSeeder.RoomAStandardPrice,
            await db.Rooms.Where(r => r.Id == ids.RoomAId).Select(r => r.StandardPrice).SingleAsync());
    }

    [SqlServerFact]
    public async Task CongTac_KhongThe_thi_nem_Unauthorized()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var ids = await TestDataSeeder.SeedAsync(db);

        var service = NewRoomService(fixture, RoomChangeDecision.Denied);
        var quanLy = new PropertyCaller(ids.QuanLyId, Roles.QuanLy);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdateAsync(ids.RoomAId, RoomUpdate(ids.PropertyAId, 3_500_000m), quanLy));

        Assert.Equal(
            TestDataSeeder.RoomAStandardPrice,
            await db.Rooms.Where(r => r.Id == ids.RoomAId).Select(r => r.StandardPrice).SingleAsync());
    }

    private static RoomService NewRoomService(
        IDbContextFactory<ApplicationDbContext> dbContextFactory,
        RoomChangeDecision decision) =>
        new(dbContextFactory, TimeProvider.System, new FixedRoomChangePolicy(decision), new NoActiveContractRoomUsageChecker());

    private static RoomSave RoomUpdate(int propertyId, decimal price) => new()
    {
        PropertyId = propertyId,
        Code = TestDataSeeder.RoomACode,
        Floor = 1,
        AreaM2 = 20m,
        StandardPrice = price,
        Status = RoomStatus.Available,
    };

    private sealed class FixedRoomChangePolicy(RoomChangeDecision decision) : IRoomChangePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(
            RoomChangeRequest request, CancellationToken ct = default) =>
            Task.FromResult(decision);
    }
}
