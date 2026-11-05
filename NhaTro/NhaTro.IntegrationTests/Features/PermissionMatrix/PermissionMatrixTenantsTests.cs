using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// P3 hồ sơ khách thuê — sheet Ma trận quyền, dòng 18..22 (OP17 xem, OP18 tạo, OP19 sửa,
/// OP20 xóa mềm, OP21 khôi phục).
/// Dependency THẬT: ManagerTogglePolicy đọc công tắc SW-04/05/06 từ DB (không còn stub
/// "cho phép hết"), hợp đồng duyệt thật để rule "hồ sơ còn hợp đồng hiệu lực" của TenantService
/// chạy đúng như production.
/// Ô ⚙️ (F19 tạo / F20 sửa / F21 xóa của Quản lý theo 3 chế độ công tắc) và ô ⚠️ (G18, G20 —
/// khách thuê chỉ trong phạm vi hồ sơ của mình) do harness data-driven
/// <see cref="PermissionMatrixAutomatedTests"/> chứng minh.
/// </summary>
public sealed class PermissionMatrixTenantsTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string FreeTenantName = "Khach chua co hop dong";
    private const string TenantInAName = "Khach co hop dong nha A";
    private const string TenantInBName = "Khach co hop dong nha B";

    private static PropertyCaller OwnerA => new("owner-a", Roles.ChuTro);
    private static PropertyCaller OwnerB => new("owner-b", Roles.ChuTro);
    private static TenantCaller OwnerATenant => new("owner-a", Roles.ChuTro);
    private static TenantCaller OwnerBTenant => new("owner-b", Roles.ChuTro);
    private static TenantCaller ManagerTenant => new("manager-1", Roles.QuanLy);
    private static TenantCaller TenantA => new("khach-1", Roles.KhachThue);
    private static TenantCaller Admin => new("admin-1", Roles.Admin);

    // === OP17 Xem hồ sơ khách (dòng 18) ===

    /// <summary>Ô D18 ❌: Admin không đọc hồ sơ khách (BusinessGuard D1).</summary>
    [SqlServerFact]
    public async Task Cell_D18_OP17_Admin_IsDenied()
    {
        var (db, _) = await SeedAsync();
        await using var _ = db;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewTenants(db).ListAsync(new TenantFilterRequest(), Admin));
    }

    /// <summary>Ô E18 ✅: Chủ trọ A thấy hồ sơ nhà mình, không thấy hồ sơ đã gắn hợp đồng ở nhà B.</summary>
    [SqlServerFact]
    public async Task Cell_E18_OP17_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var list = await NewTenants(db).ListAsync(new TenantFilterRequest(), OwnerATenant);

        Assert.Contains(list.Items, item => item.Id == data.FreeTenantId);
        Assert.Contains(list.Items, item => item.Id == data.TenantInAId);
        Assert.DoesNotContain(list.Items, item => item.Id == data.TenantInBId);
    }

    /// <summary>Ô F18 ✅: Quản lý được giao nhà A thấy khách có hợp đồng ở nhà A, không thấy khách nhà B.</summary>
    [SqlServerFact]
    public async Task Cell_F18_OP17_QuanLy_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var list = await NewTenants(db).ListAsync(new TenantFilterRequest(), ManagerTenant);

        Assert.Contains(list.Items, item => item.Id == data.TenantInAId);
        Assert.DoesNotContain(list.Items, item => item.Id == data.TenantInBId);
    }

    // === OP18 Tạo hồ sơ khách (dòng 19) ===

    /// <summary>Ô D19 ❌: Admin tạo hồ sơ khách bị từ chối, số hồ sơ trong DB không đổi.</summary>
    [SqlServerFact]
    public async Task Cell_D19_OP18_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        await AssertDeniedAndUnchangedAsync(data.TenantCountBefore, () =>
            NewTenants(db).CreateAsync(NewTenant("Khach Admin tao", "0909000001", "079201000101"), Admin));
    }

    /// <summary>Ô E19 ✅: Chủ trọ tạo hồ sơ mới và đọc lại được chính hồ sơ đó.</summary>
    [SqlServerFact]
    public async Task Cell_E19_OP18_ChuTro_IsAllowed()
    {
        var (db, _) = await SeedAsync();
        await using var _ = db;
        var tenants = NewTenants(db);

        var created = await tenants.CreateAsync(
            NewTenant("Khach Chủ trọ tạo", "0909000002", "079201000102"), OwnerATenant);
        Assert.True(created.Succeeded, created.ErrorMessage);

        var stored = await tenants.GetAsync(created.Tenant!.Id, OwnerATenant);
        Assert.Equal("Khach Chủ trọ tạo", stored.Tenant!.FullName);
    }

    /// <summary>Ô G19 ❌: Khách thuê tự tạo hồ sơ người khác bị từ chối, dữ liệu không đổi.</summary>
    [SqlServerFact]
    public async Task Cell_G19_OP18_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        await AssertDeniedAndUnchangedAsync(data.TenantCountBefore, () =>
            NewTenants(db).CreateAsync(NewTenant("Khach tu tao", "0909000003", "079201000103"), TenantA));
    }

    // === OP19 Sửa hồ sơ khách (dòng 20) ===

    /// <summary>Ô D20 ❌: Admin sửa hồ sơ bị từ chối, họ tên giữ nguyên.</summary>
    [SqlServerFact]
    public async Task Cell_D20_OP19_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await ReadTenantAsync(data.FreeTenantId);
        var request = Rename("Đổi tên bởi Admin");
        request.RowVersion = before.RowVersion;

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewTenants(db).UpdateAsync(data.FreeTenantId, request, Admin));
        Assert.Equal(before.FullName, (await ReadTenantAsync(data.FreeTenantId)).FullName);
    }

    /// <summary>Ô E20 ✅: Chủ trọ sửa hồ sơ và giá trị mới nằm lại trong DB.</summary>
    [SqlServerFact]
    public async Task Cell_E20_OP19_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var tenants = NewTenants(db);
        var current = await tenants.GetAsync(data.FreeTenantId, OwnerATenant);

        var request = Rename("Đổi tên bởi Chủ trọ");
        request.RowVersion = current.Tenant!.RowVersion;
        var updated = await tenants.UpdateAsync(data.FreeTenantId, request, OwnerATenant);
        Assert.True(updated.Succeeded, updated.ErrorMessage);

        var stored = await tenants.GetAsync(data.FreeTenantId, OwnerATenant);
        Assert.Equal("Đổi tên bởi Chủ trọ", stored.Tenant!.FullName);
    }

    // === OP20 Xóa hồ sơ khách vào thùng rác (dòng 21) ===

    /// <summary>Ô D21 ❌: Admin xóa hồ sơ bị từ chối, hồ sơ vẫn chưa vào thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_D21_OP20_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await ReadTenantAsync(data.FreeTenantId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewTenants(db).DeleteAsync(data.FreeTenantId, before.RowVersion, Admin));
        Assert.False(await IsDeletedAsync(data.FreeTenantId));
    }

    /// <summary>
    /// Ô E21 — Chủ trọ xóa hồ sơ, kiểm cả hai chiều của cùng một ô: hồ sơ còn hợp đồng hiệu lực thì
    /// service trả TenantInUse và hồ sơ không vào thùng rác; hồ sơ đã kết thúc thì xóa mềm thành công,
    /// rời danh sách và xuất hiện trong thùng rác.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E21_OP20_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var tenants = NewTenants(db);

        var inUse = await ReadTenantAsync(data.TenantInAId);
        var blocked = await tenants.DeleteAsync(data.TenantInAId, inUse.RowVersion, OwnerATenant);
        Assert.False(blocked.Succeeded);
        Assert.Equal(TenantErrorCodes.TenantInUse, blocked.ErrorCode);
        Assert.False(await IsDeletedAsync(data.TenantInAId));

        var before = await ReadTenantAsync(data.FreeTenantId);
        var deleted = await tenants.DeleteAsync(data.FreeTenantId, before.RowVersion, OwnerATenant);
        Assert.True(deleted.Succeeded, deleted.ErrorMessage);

        Assert.True(await IsDeletedAsync(data.FreeTenantId));
        Assert.DoesNotContain((await tenants.ListAsync(new TenantFilterRequest(), OwnerATenant)).Items,
            item => item.Id == data.FreeTenantId);
        Assert.Contains((await tenants.ListTrashAsync(new TenantTrashFilterRequest(), OwnerATenant)).Items,
            item => item.Id == data.FreeTenantId);
    }

    /// <summary>Ô G21 ❌: Khách thuê xóa hồ sơ bị từ chối, hồ sơ vẫn chưa vào thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_G21_OP20_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await ReadTenantAsync(data.FreeTenantId);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewTenants(db).DeleteAsync(data.FreeTenantId, before.RowVersion, TenantA));
        Assert.False(await IsDeletedAsync(data.FreeTenantId));
    }

    // === OP21 Khôi phục hồ sơ từ thùng rác (dòng 22) ===

    /// <summary>Ô D22 ❌: Admin khôi phục hồ sơ bị từ chối, hồ sơ vẫn trong thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_D22_OP21_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var trash = await DeleteFreeTenantAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewTenants(db).RestoreAsync(data.FreeTenantId, trash.RowVersion, Admin));
        Assert.True(await IsDeletedAsync(data.FreeTenantId));
    }

    /// <summary>Ô E22 ✅: Chủ trọ khôi phục hồ sơ xóa nhầm, hồ sơ trở lại danh sách đang dùng.</summary>
    [SqlServerFact]
    public async Task Cell_E22_OP21_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var tenants = NewTenants(db);
        var trash = await DeleteFreeTenantAsync(db);

        var restored = await tenants.RestoreAsync(data.FreeTenantId, trash.RowVersion, OwnerATenant);
        Assert.True(restored.Succeeded, restored.ErrorMessage);

        Assert.False(await IsDeletedAsync(data.FreeTenantId));
        Assert.Contains((await tenants.ListAsync(new TenantFilterRequest(), OwnerATenant)).Items,
            item => item.Id == data.FreeTenantId);
    }

    /// <summary>Ô F22 ❌: Quản lý khôi phục hồ sơ bị từ chối (VHT-99 chỉ Chủ trọ), hồ sơ vẫn trong thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_F22_OP21_QuanLy_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var trash = await DeleteFreeTenantAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewTenants(db).RestoreAsync(data.FreeTenantId, trash.RowVersion, ManagerTenant));
        Assert.True(await IsDeletedAsync(data.FreeTenantId));
    }

    /// <summary>Ô G22 ❌: Khách thuê khôi phục hồ sơ bị từ chối, hồ sơ vẫn trong thùng rác.</summary>
    [SqlServerFact]
    public async Task Cell_G22_OP21_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var trash = await DeleteFreeTenantAsync(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewTenants(db).RestoreAsync(data.FreeTenantId, trash.RowVersion, TenantA));
        Assert.True(await IsDeletedAsync(data.FreeTenantId));
    }

    // === helpers ===

    private TenantService NewTenants(ApplicationDbContext db) =>
        new(fixture, TimeProvider.System, PermissionMatrixWorld.NewRealTogglePolicy(fixture),
            PermissionMatrixWorld.NewRealApprovals(db));

    private static CreateTenantRequest NewTenant(string fullName, string phone, string idNumber) =>
        new() { FullName = fullName, PhoneNumber = phone, IdNumber = idNumber };

    private static UpdateTenantRequest Rename(string fullName) =>
        new() { FullName = fullName, PhoneNumber = "0907777777" };

    /// <summary>Xóa mềm hồ sơ chưa có hợp đồng rồi trả dòng thùng rác (RowVersion sau khi xóa).</summary>
    private async Task<TenantTrashItemDto> DeleteFreeTenantAsync(ApplicationDbContext db)
    {
        var tenants = NewTenants(db);
        var current = await tenants.GetAsync(FreeTenantId(), OwnerATenant);
        Assert.True((await tenants.DeleteAsync(
            current.Tenant!.Id, current.Tenant.RowVersion, OwnerATenant)).Succeeded);

        var trash = await tenants.ListTrashAsync(new TenantTrashFilterRequest(), OwnerATenant);
        return trash.Items.First(item => item.Id == current.Tenant.Id);
    }

    private int FreeTenantId()
    {
        using var read = fixture.CreateContext();
        return read.Tenants.Where(t => t.FullName == FreeTenantName).Select(t => t.Id).First();
    }

    private async Task<(string FullName, string RowVersion)> ReadTenantAsync(int tenantId)
    {
        await using var read = fixture.CreateContext();
        var tenant = await read.Tenants.IgnoreQueryFilters().AsNoTracking().FirstAsync(t => t.Id == tenantId);
        return (tenant.FullName, Convert.ToBase64String(tenant.RowVersion));
    }

    private async Task<bool> IsDeletedAsync(int tenantId)
    {
        await using var read = fixture.CreateContext();
        return await read.Tenants.IgnoreQueryFilters()
            .Where(t => t.Id == tenantId).Select(t => t.IsDeleted).FirstAsync();
    }

    private async Task AssertDeniedAndUnchangedAsync(int expectedCount, Func<Task> action)
    {
        await Assert.ThrowsAsync<UnauthorizedAccessException>(action);
        await using var check = fixture.CreateContext();
        Assert.Equal(expectedCount, await check.Tenants.IgnoreQueryFilters().CountAsync());
    }

    // === nền test ===

    private async Task<(ApplicationDbContext Db, SeedData Data)> SeedAsync()
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

        var db = fixture.CreateContext();
        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = new RoomService(fixture, TimeProvider.System,
            PermissionMatrixWorld.NewRealTogglePolicy(fixture),
            new ActiveContractRoomUsageChecker(fixture, TimeProvider.System));
        var tenants = NewTenants(db);

        var propertyA = await properties.CreateAsync(new PropertySave { Name = "Nhà A" }, OwnerA);
        var propertyB = await properties.CreateAsync(new PropertySave { Name = "Nhà B" }, OwnerB);
        Assert.True(propertyA.Succeeded, propertyA.ErrorMessage);
        Assert.True(propertyB.Succeeded, propertyB.ErrorMessage);

        var roomA = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyA.Property!.Id, Code = "A101", StandardPrice = 1_500_000m }, OwnerA);
        var roomB = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyB.Property!.Id, Code = "B101", StandardPrice = 1_800_000m }, OwnerB);
        Assert.True(roomA.Succeeded, roomA.ErrorMessage);
        Assert.True(roomB.Succeeded, roomB.ErrorMessage);

        await new PropertyManagerService(fixture, TimeProvider.System)
            .AssignAsync(propertyA.Property.Id, "manager-1", OwnerA);

        var freeTenant = await tenants.CreateAsync(NewTenant(FreeTenantName, "0901111111", "079201000001"), OwnerATenant);
        var tenantInA = await tenants.CreateAsync(NewTenant(TenantInAName, "0902222222", "079201000002"), OwnerATenant);
        var tenantInB = await tenants.CreateAsync(NewTenant(TenantInBName, "0903333333", "079201000003"), OwnerBTenant);
        Assert.True(freeTenant.Succeeded, freeTenant.ErrorMessage);
        Assert.True(tenantInA.Succeeded, tenantInA.ErrorMessage);
        Assert.True(tenantInB.Succeeded, tenantInB.ErrorMessage);

        // Hai hợp đồng đã duyệt THẬT: một ở nhà A (Quản lý được giao), một ở nhà B (ngoài phạm vi).
        var contracts = new ContractService(db, TimeProvider.System,
            PermissionMatrixWorld.NewRealTogglePolicy(fixture), PermissionMatrixWorld.NewRealApprovals(db));
        var today = DateTime.UtcNow.Date;
        await ApproveAsync(contracts, roomA.Room!.Id, tenantInA.Tenant!.Id, today, OwnerA);
        await ApproveAsync(contracts, roomB.Room!.Id, tenantInB.Tenant!.Id, today, OwnerB);

        await using (var count = fixture.CreateContext())
        {
            var countBefore = await count.Tenants.IgnoreQueryFilters().CountAsync();
            return (db, new SeedData(freeTenant.Tenant!.Id, tenantInA.Tenant!.Id, tenantInB.Tenant!.Id, countBefore));
        }
    }

    private async Task ApproveAsync(
        ContractService contracts, int roomId, int tenantId, DateTime today, PropertyCaller caller)
    {
        var created = await contracts.CreateContractAsync(
            new CreateContractRequest(roomId, today, today.AddMonths(6), [tenantId]), caller);
        var approved = await contracts.ApproveContractAsync(
            created.ContractId, new ApproveContractRequestDto { IsApproved = true }, caller);
        Assert.Equal(ContractStatus.Approved, approved.Status);
    }

    private sealed record SeedData(int FreeTenantId, int TenantInAId, int TenantInBId, int TenantCountBefore);

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô G18 ⚠️: OP17 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G18_OP17_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G18");

    /// <summary>Ô F19 ⚙️: OP18 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F19_OP18_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F19");

    /// <summary>Ô F20 ⚙️: OP19 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F20_OP19_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F20");

    /// <summary>Ô G20 ⚠️: OP19 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G20_OP19_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G20");

    /// <summary>Ô F21 ⚙️: OP20 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F21_OP20_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F21");
}
