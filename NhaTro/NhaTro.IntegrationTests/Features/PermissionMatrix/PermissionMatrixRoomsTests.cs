using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Features.Accounts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// P2 phòng — sheet Ma trận quyền, dòng 12..17 (OP11 xem, OP12 sửa thông tin, OP13 đổi giá,
/// OP14 đổi trạng thái, OP15 xóa mềm, OP16 khôi phục).
/// Mỗi test mang định danh của đúng một ô và dùng dependency THẬT: công tắc Quản lý là
/// ManagerTogglePolicy đọc 16 dòng SW từ DB, checker hợp đồng là ActiveContractRoomUsageChecker.
/// Ô ⚙️ (F13/F14/F15 — 3 chế độ của SW-01/02/03) và ô ⚠️ (G12 — khách thuê chỉ trong phạm vi phòng
/// mình đang thuê) do bộ harness data-driven <see cref="PermissionMatrixAutomatedTests"/> chứng minh,
/// vì một test đơn lẻ không phủ được cả ba chế độ công tắc lẫn cả hai nửa phạm vi.
/// </summary>
public sealed class PermissionMatrixRoomsTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static PropertyCaller OwnerA => new("owner-a", Roles.ChuTro);
    private static PropertyCaller OwnerB => new("owner-b", Roles.ChuTro);
    private static PropertyCaller Manager => new("manager-1", Roles.QuanLy);
    private static PropertyCaller TenantA => new("khach-1", Roles.KhachThue);
    private static PropertyCaller Admin => new("admin-1", Roles.Admin);

    private static TenantCaller OwnerATenant => new("owner-a", Roles.ChuTro);

    private RoomService NewRoomService() =>
        new(
            fixture,
            TimeProvider.System,
            PermissionMatrixWorld.NewRealTogglePolicy(fixture),
            new ActiveContractRoomUsageChecker(fixture, TimeProvider.System));

    // === OP11 Xem phòng (dòng 12) ===

    /// <summary>Ô D12 ❌: Admin chỉ quản trị tài khoản/vai trò (D1), không gọi service nghiệp vụ phòng.</summary>
    [SqlServerFact]
    public async Task Cell_D12_OP11_Admin_IsDenied()
    {
        await SeedAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewRoomService().ListAsync(new RoomFilter(), Admin));
    }

    /// <summary>Ô E12 ✅: Chủ trọ xem toàn bộ phòng của nhà mình — và không thấy phòng nhà khác.</summary>
    [SqlServerFact]
    public async Task Cell_E12_OP11_ChuTro_IsAllowed()
    {
        var data = await SeedAsync();
        var list = await NewRoomService().ListAsync(new RoomFilter(), OwnerA);

        Assert.Equal(2, list.TotalCount);
        Assert.All(list.Items, room => Assert.Equal(data.PropertyAId, room.PropertyId));
        Assert.DoesNotContain(list.Items, room => room.Id == data.RoomBId);
    }

    /// <summary>Ô F12 ✅: Quản lý được giao nhà A xem đúng phòng nhà A, không thấy phòng nhà B.</summary>
    [SqlServerFact]
    public async Task Cell_F12_OP11_QuanLy_IsAllowed()
    {
        var data = await SeedAsync();
        var list = await NewRoomService().ListAsync(new RoomFilter(), Manager);

        Assert.Equal(2, list.TotalCount);
        Assert.All(list.Items, room => Assert.Equal(data.PropertyAId, room.PropertyId));
        Assert.DoesNotContain(list.Items, room => room.Id == data.RoomBId);
    }

    // === OP12 Sửa thông tin phòng (dòng 13) ===

    /// <summary>Ô D13 ❌: Admin sửa thông tin phòng bị từ chối và dữ liệu giữ nguyên.</summary>
    [SqlServerFact]
    public async Task Cell_D13_OP12_Admin_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId, () =>
            NewRoomService().UpdateAsync(data.RoomSpareId, EditInfo(data.PropertyAId), Admin));
    }

    /// <summary>Ô E13 ✅: Chủ trọ sửa mã phòng/tầng, giá trị mới nằm lại trong DB.</summary>
    [SqlServerFact]
    public async Task Cell_E13_OP12_ChuTro_IsAllowed()
    {
        var data = await SeedAsync();
        var rooms = NewRoomService();

        var updated = await rooms.UpdateAsync(data.RoomSpareId, EditInfo(data.PropertyAId), OwnerA);
        Assert.True(updated.Succeeded, updated.ErrorMessage);

        var stored = await rooms.GetAsync(data.RoomSpareId, OwnerA);
        Assert.Equal("A102-moi", stored.Room!.Code);
        Assert.Equal(3, stored.Room!.Floor);
    }

    /// <summary>Ô G13 ❌: Khách thuê sửa thông tin phòng bị từ chối và dữ liệu giữ nguyên.</summary>
    [SqlServerFact]
    public async Task Cell_G13_OP12_KhachThue_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId, () =>
            NewRoomService().UpdateAsync(data.RoomSpareId, EditInfo(data.PropertyAId), TenantA));
    }

    // === OP13 Thiết lập / đổi giá phòng (dòng 14) ===

    /// <summary>Ô D14 ❌: Admin đổi giá phòng bị từ chối, giá cũ giữ nguyên.</summary>
    [SqlServerFact]
    public async Task Cell_D14_OP13_Admin_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId, () =>
            NewRoomService().UpdateAsync(data.RoomSpareId, ChangePrice(data.PropertyAId), Admin));
    }

    /// <summary>Ô E14 ✅: Chủ trọ đổi giá thuê có hiệu lực thật (Chủ trọ luôn hiệu lực ngay, D4).</summary>
    [SqlServerFact]
    public async Task Cell_E14_OP13_ChuTro_IsAllowed()
    {
        var data = await SeedAsync();
        var rooms = NewRoomService();

        var updated = await rooms.UpdateAsync(data.RoomSpareId, ChangePrice(data.PropertyAId), OwnerA);
        Assert.True(updated.Succeeded, updated.ErrorMessage);

        var stored = await rooms.GetAsync(data.RoomSpareId, OwnerA);
        Assert.Equal(2_500_000m, stored.Room!.StandardPrice);
    }

    /// <summary>Ô G14 ❌: Khách thuê đổi giá phòng bị từ chối, giá cũ giữ nguyên.</summary>
    [SqlServerFact]
    public async Task Cell_G14_OP13_KhachThue_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId, () =>
            NewRoomService().UpdateAsync(data.RoomSpareId, ChangePrice(data.PropertyAId), TenantA));
    }

    // === OP14 Đổi trạng thái phòng (dòng 15) ===

    /// <summary>Ô D15 ❌: Admin đổi trạng thái phòng bị từ chối, trạng thái cũ giữ nguyên.</summary>
    [SqlServerFact]
    public async Task Cell_D15_OP14_Admin_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId, () =>
            NewRoomService().UpdateAsync(data.RoomSpareId, ChangeStatus(data.PropertyAId), Admin));
    }

    /// <summary>Ô E15 ✅: Chủ trọ đánh dấu phòng đang bảo trì, đọc lại đúng trạng thái mới.</summary>
    [SqlServerFact]
    public async Task Cell_E15_OP14_ChuTro_IsAllowed()
    {
        var data = await SeedAsync();
        var rooms = NewRoomService();

        var updated = await rooms.UpdateAsync(data.RoomSpareId, ChangeStatus(data.PropertyAId), OwnerA);
        Assert.True(updated.Succeeded, updated.ErrorMessage);

        var stored = await rooms.GetAsync(data.RoomSpareId, OwnerA);
        Assert.Equal(RoomStatus.Maintenance, stored.Room!.Status);
    }

    /// <summary>Ô G15 ❌: Khách thuê đổi trạng thái phòng bị từ chối, trạng thái cũ giữ nguyên.</summary>
    [SqlServerFact]
    public async Task Cell_G15_OP14_KhachThue_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId, () =>
            NewRoomService().UpdateAsync(data.RoomSpareId, ChangeStatus(data.PropertyAId), TenantA));
    }

    // === OP15 Xóa phòng vào thùng rác (dòng 16) ===

    /// <summary>Ô D16 ❌: Admin xóa phòng bị từ chối, phòng vẫn chưa vào thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_D16_OP15_Admin_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId,
            () => NewRoomService().DeleteAsync(data.RoomSpareId, Admin));
    }

    /// <summary>
    /// Ô E16 — Chủ trọ xóa phòng, kiểm cả hai chiều của cùng một ô: phòng còn hợp đồng hiệu lực thì
    /// checker hợp đồng THẬT chặn (mã lỗi RoomInUse, phòng không vào thùng rác) dù Chủ trọ có quyền
    /// xóa; phòng trống thì xóa mềm thành công, rời danh sách và xuất hiện trong thùng rác.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E16_OP15_ChuTro_IsAllowed()
    {
        var data = await SeedAsync();
        var rooms = NewRoomService();

        var blocked = await rooms.DeleteAsync(data.RoomWithContractId, OwnerA);
        Assert.False(blocked.Succeeded);
        Assert.Equal(RoomErrorCodes.RoomInUse, blocked.ErrorCode);
        Assert.False(await IsDeletedAsync(data.RoomWithContractId));

        var deleted = await rooms.DeleteAsync(data.RoomSpareId, OwnerA);
        Assert.True(deleted.Succeeded, deleted.ErrorMessage);

        Assert.True(await IsDeletedAsync(data.RoomSpareId));
        Assert.DoesNotContain((await rooms.ListAsync(new RoomFilter(), OwnerA)).Items,
            room => room.Id == data.RoomSpareId);
        Assert.Contains((await rooms.ListDeletedAsync(new RoomFilter(), OwnerA)).Items,
            item => item.Id == data.RoomSpareId);
    }

    /// <summary>Ô F16 ❌: Quản lý xóa phòng bị từ chối (ma trận #15 chỉ Chủ trọ), phòng vẫn còn.</summary>
    [SqlServerFact]
    public async Task Cell_F16_OP15_QuanLy_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId,
            () => NewRoomService().DeleteAsync(data.RoomSpareId, Manager));
    }

    /// <summary>Ô G16 ❌: Khách thuê xóa phòng bị từ chối, phòng vẫn còn.</summary>
    [SqlServerFact]
    public async Task Cell_G16_OP15_KhachThue_IsDenied()
    {
        var data = await SeedAsync();
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId,
            () => NewRoomService().DeleteAsync(data.RoomSpareId, TenantA));
    }

    // === OP16 Khôi phục phòng từ thùng rác (dòng 17) ===

    /// <summary>Ô D17 ❌: Admin khôi phục phòng bị từ chối, phòng vẫn nằm trong thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_D17_OP16_Admin_IsDenied()
    {
        var data = await SeedAsync();
        await DeleteSpareAsync(data.RoomSpareId);
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId,
            () => NewRoomService().RestoreAsync(data.RoomSpareId, Admin));
    }

    /// <summary>Ô E17 ✅: Chủ trọ khôi phục phòng xóa nhầm, phòng trở lại danh sách đang dùng.</summary>
    [SqlServerFact]
    public async Task Cell_E17_OP16_ChuTro_IsAllowed()
    {
        var data = await SeedAsync();
        var rooms = NewRoomService();
        await DeleteSpareAsync(data.RoomSpareId);

        var restored = await rooms.RestoreAsync(data.RoomSpareId, OwnerA);
        Assert.True(restored.Succeeded, restored.ErrorMessage);

        Assert.False(await IsDeletedAsync(data.RoomSpareId));
        Assert.Contains((await rooms.ListAsync(new RoomFilter(), OwnerA)).Items,
            room => room.Id == data.RoomSpareId);
    }

    /// <summary>Ô F17 ❌: Quản lý khôi phục phòng bị từ chối, phòng vẫn trong thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_F17_OP16_QuanLy_IsDenied()
    {
        var data = await SeedAsync();
        await DeleteSpareAsync(data.RoomSpareId);
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId,
            () => NewRoomService().RestoreAsync(data.RoomSpareId, Manager));
    }

    /// <summary>Ô G17 ❌: Khách thuê khôi phục phòng bị từ chối, phòng vẫn trong thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_G17_OP16_KhachThue_IsDenied()
    {
        var data = await SeedAsync();
        await DeleteSpareAsync(data.RoomSpareId);
        await AssertDeniedAndUnchangedAsync(data.RoomSpareId,
            () => NewRoomService().RestoreAsync(data.RoomSpareId, TenantA));
    }

    // === request builders ===

    private static RoomSave EditInfo(int propertyId) =>
        new() { PropertyId = propertyId, Code = "A102-moi", Floor = 3, StandardPrice = 2_000_000m };

    private static RoomSave ChangePrice(int propertyId) =>
        new() { PropertyId = propertyId, Code = "A102", StandardPrice = 2_500_000m };

    private static RoomSave ChangeStatus(int propertyId) =>
        new()
        {
            PropertyId = propertyId,
            Code = "A102",
            StandardPrice = 2_000_000m,
            Status = RoomStatus.Maintenance,
        };

    // === nền test ===

    private async Task DeleteSpareAsync(int roomId) =>
        Assert.True((await NewRoomService().DeleteAsync(roomId, OwnerA)).Succeeded);

    /// <summary>Ô ❌ phải vừa bị từ chối vừa để nguyên dữ liệu — bằng chứng không chỉ nằm ở exception.</summary>
    private async Task AssertDeniedAndUnchangedAsync(int roomId, Func<Task> action)
    {
        var before = await RoomStateAsync(roomId);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
        Assert.Equal(before, await RoomStateAsync(roomId));
    }

    private async Task<bool> IsDeletedAsync(int roomId)
    {
        await using var db = fixture.CreateContext();
        return await db.Rooms.IgnoreQueryFilters().Where(r => r.Id == roomId).Select(r => r.IsDeleted).FirstAsync();
    }

    private async Task<string> RoomStateAsync(int roomId)
    {
        await using var db = fixture.CreateContext();
        var room = await db.Rooms.IgnoreQueryFilters()
            .Where(r => r.Id == roomId)
            .Select(r => new { r.Code, r.Floor, r.StandardPrice, r.Status, r.IsDeleted })
            .FirstAsync();
        return $"{room.Code}|{room.Floor}|{room.StandardPrice}|{room.Status}|deleted={room.IsDeleted}";
    }

    private async Task<SeedData> SeedAsync()
    {
        await fixture.ResetAsync();
        await using (var setup = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(setup, "admin-1", Roles.Admin);
            await SqlServerFixture.SeedAccountAsync(setup, "owner-a", Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(setup, "owner-b", Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(setup, "manager-1", Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(setup, "khach-1", Roles.KhachThue);
        }

        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = NewRoomService();

        var propertyA = await properties.CreateAsync(new PropertySave { Name = "Nhà A" }, OwnerA);
        var propertyB = await properties.CreateAsync(new PropertySave { Name = "Nhà B" }, OwnerB);
        Assert.True(propertyA.Succeeded, propertyA.ErrorMessage);
        Assert.True(propertyB.Succeeded, propertyB.ErrorMessage);

        // A101 có hợp đồng đã duyệt (đo rule "không xóa phòng còn hợp đồng");
        // A102 còn trống để đo xóa mềm/khôi phục mà không vướng rule nào khác.
        var roomWithContract = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyA.Property!.Id, Code = "A101", StandardPrice = 1_500_000m }, OwnerA);
        var roomSpare = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyA.Property.Id, Code = "A102", StandardPrice = 2_000_000m }, OwnerA);
        var roomB = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyB.Property!.Id, Code = "B101", StandardPrice = 1_800_000m }, OwnerB);
        Assert.True(roomWithContract.Succeeded, roomWithContract.ErrorMessage);
        Assert.True(roomSpare.Succeeded, roomSpare.ErrorMessage);
        Assert.True(roomB.Succeeded, roomB.ErrorMessage);

        await new PropertyManagerService(fixture, TimeProvider.System)
            .AssignAsync(propertyA.Property.Id, "manager-1", OwnerA);

        await SeedActiveContractAsync(roomWithContract.Room!.Id);

        return new SeedData(
            propertyA.Property.Id,
            propertyB.Property.Id,
            roomWithContract.Room!.Id,
            roomSpare.Room!.Id,
            roomB.Room!.Id);
    }

    /// <summary>Hợp đồng đã duyệt THẬT (không stub) để checker "phòng còn hợp đồng" chạy như production.</summary>
    private async Task SeedActiveContractAsync(int roomId)
    {
        await using var db = fixture.CreateContext();
        var policy = PermissionMatrixWorld.NewRealTogglePolicy(fixture);
        var approvals = PermissionMatrixWorld.NewRealApprovals(db);

        var tenant = await new TenantService(fixture, TimeProvider.System, policy, approvals)
            .CreateAsync(new CreateTenantRequest
            {
                FullName = "Khách có hợp đồng hiệu lực",
                PhoneNumber = "0901111111",
                IdNumber = "079201000001",
            }, OwnerATenant);
        Assert.True(tenant.Succeeded, tenant.ErrorMessage);

        var today = DateTime.UtcNow.Date;
        var contracts = new ContractService(db, TimeProvider.System, policy, approvals);
        var created = await contracts.CreateContractAsync(
            new CreateContractRequest(roomId, today, today.AddMonths(6), [tenant.Tenant!.Id]), OwnerA);
        await contracts.ApproveContractAsync(
            created.ContractId, new ApproveContractRequestDto { IsApproved = true }, OwnerA);
    }

    private sealed record SeedData(
        int PropertyAId, int PropertyBId, int RoomWithContractId, int RoomSpareId, int RoomBId);

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô G12 ⚠️: OP11 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G12_OP11_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G12");

    /// <summary>Ô F13 ⚙️: OP12 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F13_OP12_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F13");

    /// <summary>Ô F14 ⚙️: OP13 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F14_OP13_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F14");

    /// <summary>Ô F15 ⚙️: OP14 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F15_OP14_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F15");
}
