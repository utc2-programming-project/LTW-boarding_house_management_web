using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts;
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
/// P4 hợp đồng — sheet Ma trận quyền, dòng 23..26 (OP22 xem, OP23 tạo, OP24 duyệt/từ chối,
/// OP25 gia hạn–thanh lý).
/// Dependency THẬT: ContractService nhận ManagerTogglePolicy đọc SW-07 từ DB + hàng đợi duyệt
/// ManagerApprovalService thật (không còn "ContractService trần" bỏ qua công tắc như bản cũ),
/// dữ liệu là hợp đồng tạo & duyệt qua service thật, không hard-code id.
/// Ô ⚙️ (F26 — 3 chế độ SW-07 cho Quản lý gia hạn/thanh lý) và ô ⚠️ (G23 — khách thuê chỉ xem
/// hợp đồng mình ký) do harness data-driven <see cref="PermissionMatrixAutomatedTests"/> chứng minh.
/// </summary>
public sealed class PermissionMatrixContractsTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static PropertyCaller OwnerA => new("owner-a", Roles.ChuTro);
    private static PropertyCaller OwnerB => new("owner-b", Roles.ChuTro);
    private static PropertyCaller Manager => new("manager-1", Roles.QuanLy);
    private static PropertyCaller TenantA => new("khach-1", Roles.KhachThue);
    private static PropertyCaller Admin => new("admin-1", Roles.Admin);

    private static TenantCaller OwnerATenant => new("owner-a", Roles.ChuTro);
    private static TenantCaller OwnerBTenant => new("owner-b", Roles.ChuTro);

    // === OP22 Xem hợp đồng (dòng 23) ===

    /// <summary>Ô D23 ❌: Admin đọc danh sách hợp đồng bị chặn ngay ở vai trò (D1).</summary>
    [SqlServerFact]
    public async Task Cell_D23_OP22_Admin_IsDenied()
    {
        var (db, _) = await SeedAsync();
        await using var _ = db;
        await Assert.ThrowsAsync<ContractAuthorizationException>(() => NewContracts(db).ListAsync(null, Admin));
    }

    /// <summary>Ô E23 ✅: Chủ trọ A xem hợp đồng nhà mình, không thấy hợp đồng nhà B.</summary>
    [SqlServerFact]
    public async Task Cell_E23_OP22_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var contracts = NewContracts(db);

        var list = await contracts.ListAsync(null, OwnerA);
        Assert.Contains(list, item => item.ContractId == data.ContractAId);
        Assert.DoesNotContain(list, item => item.ContractId == data.ContractBId);
        Assert.Equal(data.ContractAId, (await contracts.GetAsync(data.ContractAId, OwnerA)).ContractId);
    }

    /// <summary>Ô F23 ✅: Quản lý được giao nhà A xem hợp đồng nhà A, không thấy hợp đồng nhà B.</summary>
    [SqlServerFact]
    public async Task Cell_F23_OP22_QuanLy_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var list = await NewContracts(db).ListAsync(null, Manager);

        Assert.Contains(list, item => item.ContractId == data.ContractAId);
        Assert.DoesNotContain(list, item => item.ContractId == data.ContractBId);
    }

    // === OP23 Tạo hợp đồng (dòng 24) ===

    /// <summary>Ô D24 ❌: Admin tạo hợp đồng bị chặn, số hợp đồng trong DB không đổi.</summary>
    [SqlServerFact]
    public async Task Cell_D24_OP23_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await CountContractsAsync();

        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => NewContracts(db).CreateContractAsync(NewRequest(data.RoomFreeId, data.TenantAId), Admin));
        Assert.Equal(before, await CountContractsAsync());
    }

    /// <summary>Ô G24 ❌: Khách thuê tự lập hợp đồng bị chặn, số hợp đồng trong DB không đổi.</summary>
    [SqlServerFact]
    public async Task Cell_G24_OP23_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await CountContractsAsync();

        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => NewContracts(db).CreateContractAsync(NewRequest(data.RoomFreeId, data.TenantAId), TenantA));
        Assert.Equal(before, await CountContractsAsync());
    }

    /// <summary>Ô E24 ✅: Chủ trọ tạo hợp đồng, trạng thái khởi tạo là Chờ duyệt.</summary>
    [SqlServerFact]
    public async Task Cell_E24_OP23_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var created = await NewContracts(db).CreateContractAsync(
            NewRequest(data.RoomFreeId, data.TenantAId), OwnerA);

        Assert.Equal(ContractStatus.Pending, created.Status);
        Assert.Equal(ContractStatus.Pending, await StatusAsync(created.ContractId));
    }

    /// <summary>
    /// Ô F24 ✅: Quản lý luôn tạo được hợp đồng cho phòng nhà được giao — hợp đồng rơi vào
    /// Chờ duyệt, kiểm soát duy nhất nằm ở bước Duyệt (ô #24).
    /// </summary>
    [SqlServerFact]
    public async Task Cell_F24_OP23_QuanLy_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var created = await NewContracts(db).CreateContractAsync(
            NewRequest(data.RoomFreeId, data.TenantAId), Manager);

        Assert.Equal(ContractStatus.Pending, created.Status);
        Assert.Equal(ContractStatus.Pending, await StatusAsync(created.ContractId));
    }

    // === OP24 Duyệt / từ chối hợp đồng (dòng 25) ===

    /// <summary>Ô D25 ❌: Admin duyệt hợp đồng bị chặn, hợp đồng vẫn Chờ duyệt.</summary>
    [SqlServerFact]
    public async Task Cell_D25_OP24_Admin_IsDenied()
    {
        await AssertApproveDeniedAsync(Admin);
    }

    /// <summary>Ô F25 ❌: Quản lý không tự duyệt được hợp đồng mình tạo (ma trận #24).</summary>
    [SqlServerFact]
    public async Task Cell_F25_OP24_QuanLy_IsDenied()
    {
        await AssertApproveDeniedAsync(Manager);
    }

    /// <summary>Ô G25 ❌: Khách thuê duyệt hợp đồng bị chặn, hợp đồng vẫn Chờ duyệt.</summary>
    [SqlServerFact]
    public async Task Cell_G25_OP24_KhachThue_IsDenied()
    {
        await AssertApproveDeniedAsync(TenantA);
    }

    /// <summary>
    /// Ô E25 ✅: Chủ trọ tự tạo rồi tự quyết định được (D4), cả hai nhánh, và hiệu lực ghi thật vào DB.
    /// Nhánh từ chối chạy trước vì hợp đồng Rejected không chiếm phòng (ContractService chỉ chặn
    /// Pending/Approved), nên cùng phòng+khách vẫn tạo được hợp đồng thứ hai để duyệt.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E25_OP24_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var contracts = NewContracts(db);

        var toReject = await contracts.CreateContractAsync(NewRequest(data.RoomFreeId, data.TenantAId), OwnerA);
        var rejected = await contracts.ApproveContractAsync(
            toReject.ContractId,
            new ApproveContractRequestDto { IsApproved = false, RejectReason = "Khách chưa đủ giấy tờ" },
            OwnerA);

        Assert.Equal(ContractStatus.Rejected, rejected.Status);
        Assert.Equal(ContractStatus.Rejected, await StatusAsync(toReject.ContractId));

        var toApprove = await contracts.CreateContractAsync(NewRequest(data.RoomFreeId, data.TenantAId), OwnerA);
        var approved = await contracts.ApproveContractAsync(
            toApprove.ContractId, new ApproveContractRequestDto { IsApproved = true }, OwnerA);

        Assert.Equal(ContractStatus.Approved, approved.Status);
        Assert.Equal(ContractStatus.Approved, await StatusAsync(toApprove.ContractId));
    }

    // === OP25 Gia hạn / thanh lý (dòng 26) ===

    /// <summary>Ô D26 ❌: Admin thanh lý hợp đồng bị chặn, hợp đồng vẫn đang hiệu lực.</summary>
    [SqlServerFact]
    public async Task Cell_D26_OP25_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;

        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => NewContracts(db).LiquidateContractAsync(data.ContractAId, Admin));
        Assert.Equal(ContractStatus.Approved, await StatusAsync(data.ContractAId));
    }

    /// <summary>Ô G26 ❌: Khách thuê tự thanh lý bị chặn, hợp đồng vẫn đang hiệu lực.</summary>
    [SqlServerFact]
    public async Task Cell_G26_OP25_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;

        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => NewContracts(db).LiquidateContractAsync(data.ContractAId, TenantA));
        Assert.Equal(ContractStatus.Approved, await StatusAsync(data.ContractAId));
    }

    /// <summary>
    /// Ô E26 ✅: Chủ trọ thanh lý hợp đồng đang hiệu lực, không cần hàng đợi duyệt (D4) —
    /// trạng thái Liquidated ghi thật vào DB.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E26_OP25_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;

        var result = await NewContracts(db).LiquidateContractAsync(data.ContractAId, OwnerA);

        Assert.Equal(ContractActionOutcome.Applied, result.Outcome);
        Assert.Equal(ContractStatus.Liquidated, await StatusAsync(data.ContractAId));
    }

    // === helpers ===

    /// <summary>Ô ❌ của OP24: vai trò bị chặn không duyệt được và hợp đồng vẫn Chờ duyệt.</summary>
    private async Task AssertApproveDeniedAsync(PropertyCaller caller)
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var contracts = NewContracts(db);
        var created = await contracts.CreateContractAsync(NewRequest(data.RoomFreeId, data.TenantAId), OwnerA);

        await Assert.ThrowsAsync<ContractAuthorizationException>(() => contracts.ApproveContractAsync(
            created.ContractId, new ApproveContractRequestDto { IsApproved = true }, caller));
        Assert.Equal(ContractStatus.Pending, await StatusAsync(created.ContractId));
    }

    private static CreateContractRequest NewRequest(int roomId, int tenantId)
    {
        var today = DateTime.UtcNow.Date;
        return new CreateContractRequest(roomId, today, today.AddMonths(6), [tenantId]);
    }

    private ContractService NewContracts(ApplicationDbContext db) =>
        new(db, TimeProvider.System, PermissionMatrixWorld.NewRealTogglePolicy(fixture),
            PermissionMatrixWorld.NewRealApprovals(db));

    private async Task<ContractStatus> StatusAsync(Guid contractId)
    {
        await using var read = fixture.CreateContext();
        return await read.Contracts.AsNoTracking()
            .Where(c => c.ContractId == contractId)
            .Select(c => c.Status)
            .FirstAsync();
    }

    private async Task<int> CountContractsAsync()
    {
        await using var read = fixture.CreateContext();
        return await read.Contracts.CountAsync();
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
        var tenants = new TenantService(fixture, TimeProvider.System,
            PermissionMatrixWorld.NewRealTogglePolicy(fixture), PermissionMatrixWorld.NewRealApprovals(db));

        var propertyA = await properties.CreateAsync(new PropertySave { Name = "Nhà A" }, OwnerA);
        var propertyB = await properties.CreateAsync(new PropertySave { Name = "Nhà B" }, OwnerB);
        Assert.True(propertyA.Succeeded, propertyA.ErrorMessage);
        Assert.True(propertyB.Succeeded, propertyB.ErrorMessage);

        var roomA = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyA.Property!.Id, Code = "A101", StandardPrice = 1_500_000m }, OwnerA);
        var roomFree = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyA.Property.Id, Code = "A102", StandardPrice = 2_000_000m }, OwnerA);
        var roomB = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyB.Property!.Id, Code = "B101", StandardPrice = 1_800_000m }, OwnerB);
        Assert.True(roomA.Succeeded, roomA.ErrorMessage);
        Assert.True(roomFree.Succeeded, roomFree.ErrorMessage);
        Assert.True(roomB.Succeeded, roomB.ErrorMessage);

        await new PropertyManagerService(fixture, TimeProvider.System)
            .AssignAsync(propertyA.Property.Id, "manager-1", OwnerA);

        var tenantA = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách nhà A",
            PhoneNumber = "0901111111",
            IdNumber = "079201000001",
        }, OwnerATenant);
        var tenantB = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách nhà B",
            PhoneNumber = "0902222222",
            IdNumber = "079201000002",
        }, OwnerBTenant);
        Assert.True(tenantA.Succeeded, tenantA.ErrorMessage);
        Assert.True(tenantB.Succeeded, tenantB.ErrorMessage);

        var contracts = NewContracts(db);
        var contractAId = await CreateApprovedContractAsync(
            contracts, roomA.Room!.Id, tenantA.Tenant!.Id, OwnerA);
        var contractBId = await CreateApprovedContractAsync(
            contracts, roomB.Room!.Id, tenantB.Tenant!.Id, OwnerB);

        return (db, new SeedData(
            contractAId, contractBId, roomFree.Room!.Id, tenantA.Tenant!.Id));
    }

    private async Task<Guid> CreateApprovedContractAsync(
        ContractService contracts, int roomId, int tenantId, PropertyCaller caller)
    {
        var created = await contracts.CreateContractAsync(NewRequest(roomId, tenantId), caller);
        var approved = await contracts.ApproveContractAsync(
            created.ContractId, new ApproveContractRequestDto { IsApproved = true }, caller);
        Assert.Equal(ContractStatus.Approved, approved.Status);
        return created.ContractId;
    }

    private sealed record SeedData(Guid ContractAId, Guid ContractBId, int RoomFreeId, int TenantAId);

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô G23 ⚠️: OP22 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G23_OP22_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G23");

    /// <summary>Ô F26 ⚙️: OP25 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F26_OP25_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F26");

    /// <summary>Ô D27 ❌: OP26 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D27_OP26_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D27");

    /// <summary>Ô E27 ✅: OP26 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E27_OP26_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E27");

    /// <summary>Ô F27 ✅: OP26 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F27_OP26_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F27");

    /// <summary>Ô G27 ⚠️: OP26 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G27_OP26_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G27");

    /// <summary>Ô D28 ❌: OP27 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D28_OP27_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D28");

    /// <summary>Ô E28 ✅: OP27 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E28_OP27_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E28");

    /// <summary>Ô F28 ⚙️: OP27 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F28_OP27_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F28");

    /// <summary>Ô G28 ❌: OP27 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G28_OP27_KhachThue_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G28");
}
