using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Services;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// P6 hóa đơn — sheet Ma trận quyền, dòng 35..37 (OP34 xem, OP35 lập, OP36 phát hành).
/// Mọi id (hợp đồng, hóa đơn, phòng) đều lấy từ dữ liệu tạo qua service thật, không hard-code;
/// ô ❌ dùng CHÍNH hợp đồng/hóa đơn nằm trong phạm vi Chủ trọ để chứng minh lý do bị chặn là
/// VAI TRÒ chứ không phải "không tìm thấy", và đọc lại DB để chắc chắn không có hiệu lực nào.
/// Ô ⚙️ (F36 lập, F37 phát hành theo công tắc Quản lý) do harness chứng minh đủ 3 chế độ
/// (Cho phép/Duyệt/Không thể); InvoiceService đã gọi ManagerTogglePolicy cho SW-10/SW-11.
/// Ô ⚠️ (G35 — khách thuê chỉ xem hóa đơn hợp đồng mình) cũng do harness chứng minh.
/// </summary>
public sealed class PermissionMatrixInvoicesTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static PropertyCaller OwnerA => new("owner-a", Roles.ChuTro);
    private static PropertyCaller OwnerB => new("owner-b", Roles.ChuTro);
    private static PropertyCaller Manager => new("manager-1", Roles.QuanLy);
    private static PropertyCaller TenantA => new("khach-1", Roles.KhachThue);
    private static PropertyCaller Admin => new("admin-1", Roles.Admin);

    // === OP34 Xem hóa đơn (dòng 35) ===

    /// <summary>
    /// Ô D35 ❌: Admin xem hóa đơn bị chặn ngay ở vai trò (không có nhánh Admin trong ScopeContracts),
    /// cả danh sách lẫn chi tiết; hóa đơn vẫn còn nguyên trong DB.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_D35_OP34_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var query = new InvoiceQueryService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => query.GetInvoicesAsync(new InvoiceFilter(), Admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => query.GetInvoiceAsync(data.InvoiceAId, Admin));
        Assert.True(await InvoiceExistsAsync(data.InvoiceAId));
    }

    /// <summary>Ô E35 ✅: Chủ trọ A xem hóa đơn nhà A, không thấy hóa đơn nhà B.</summary>
    [SqlServerFact]
    public async Task Cell_E35_OP34_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var query = new InvoiceQueryService(db);

        var list = await query.GetInvoicesAsync(new InvoiceFilter(), OwnerA);
        Assert.Contains(list.Items, item => item.Id == data.InvoiceAId);
        Assert.DoesNotContain(list.Items, item => item.Id == data.InvoiceBId);

        var detail = await query.GetInvoiceAsync(data.InvoiceAId, OwnerA);
        Assert.Equal("A101", detail.RoomCode);
        Assert.Equal(InvoiceStatus.Draft, detail.Status);
    }

    /// <summary>
    /// Ô F35 ✅: Quản lý được giao nhà A xem hóa đơn nhà A, không thấy hóa đơn nhà B.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_F35_OP34_QuanLy_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var list = await new InvoiceQueryService(db).GetInvoicesAsync(new InvoiceFilter(), Manager);

        Assert.Contains(list.Items, item => item.Id == data.InvoiceAId);
        Assert.DoesNotContain(list.Items, item => item.Id == data.InvoiceBId);
    }

    // === OP35 Tạo / lập hóa đơn (dòng 36) ===

    /// <summary>
    /// Ô D36 ❌: Admin lập hóa đơn cho hợp đồng ĐANG nằm trong phạm vi Chủ trọ vẫn bị chặn vì vai trò,
    /// số hóa đơn trong DB không đổi.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_D36_OP35_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await CountInvoicesAsync();

        var ex = await Assert.ThrowsAsync<InvoiceAuthorizationException>(
            () => NewInvoices(db).CreateDraftAsync(NewRequest(data.ContractAId, NextPeriod()), Admin));
        Assert.Equal(InvoiceErrorCodes.Forbidden, ex.ErrorCode);
        Assert.Equal(before, await CountInvoicesAsync());
    }

    /// <summary>
    /// Ô G36 ❌: Khách thuê tự lập hóa đơn cho chính hợp đồng mình bị chặn, DB không thêm hóa đơn.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_G36_OP35_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await CountInvoicesAsync();

        var ex = await Assert.ThrowsAsync<InvoiceAuthorizationException>(
            () => NewInvoices(db).CreateDraftAsync(NewRequest(data.ContractAId, NextPeriod()), TenantA));
        Assert.Equal(InvoiceErrorCodes.Forbidden, ex.ErrorCode);
        Assert.Equal(before, await CountInvoicesAsync());
    }

    /// <summary>
    /// Ô E36 ✅: Chủ trọ lập hóa đơn nháp từ hợp đồng đã duyệt, tổng tiền do service tính và ghi thật
    /// vào DB với trạng thái Đang lập.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E36_OP35_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;

        var draft = await NewInvoices(db).CreateDraftAsync(NewRequest(data.ContractAId, NextPeriod()), OwnerA);

        Assert.Equal(InvoiceStatus.Draft, draft.Status);
        Assert.True(draft.TotalAmount > 0m);
        Assert.Equal(InvoiceStatus.Draft, await InvoiceStatusAsync(draft.Id));
    }

    // === OP36 Phát hành hóa đơn (dòng 37) ===

    /// <summary>Ô D37 ❌: Admin phát hành bị chặn, nháp vẫn là nháp.</summary>
    [SqlServerFact]
    public async Task Cell_D37_OP36_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await InvoiceStatusAsync(data.InvoiceAId);

        var ex = await Assert.ThrowsAsync<InvoiceAuthorizationException>(
            () => NewInvoices(db).IssueAsync(data.InvoiceAId, Admin));

        Assert.Equal(InvoiceErrorCodes.Forbidden, ex.ErrorCode);
        Assert.Equal(before, await InvoiceStatusAsync(data.InvoiceAId));
    }

    /// <summary>Ô G37 ❌: Khách thuê tự phát hành hóa đơn của mình bị chặn, trạng thái không đổi.</summary>
    [SqlServerFact]
    public async Task Cell_G37_OP36_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await InvoiceStatusAsync(data.InvoiceAId);

        var ex = await Assert.ThrowsAsync<InvoiceAuthorizationException>(
            () => NewInvoices(db).IssueAsync(data.InvoiceAId, TenantA));

        Assert.Equal(InvoiceErrorCodes.Forbidden, ex.ErrorCode);
        Assert.Equal(before, await InvoiceStatusAsync(data.InvoiceAId));
    }

    /// <summary>
    /// Ô E37 ✅: Chủ trọ phát hành nháp → Đã phát hành, hiệu lực ghi thật vào DB
    /// (kèm IssuedAt) nên lần gọi thứ hai không tạo hóa đơn mới.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E37_OP36_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var invoices = NewInvoices(db);
        var before = await CountInvoicesAsync();

        var issued = await invoices.IssueAsync(data.InvoiceAId, OwnerA);

        Assert.Equal(InvoiceStatus.Issued, issued.Status);
        Assert.NotNull(issued.IssuedAt);
        Assert.Equal(InvoiceStatus.Issued, await InvoiceStatusAsync(data.InvoiceAId));
        Assert.Equal(before, await CountInvoicesAsync());
    }

    // === helpers ===

    private static InvoiceService NewInvoices(ApplicationDbContext db) =>
        new(db, new InvoiceQueryService(db), TimeProvider.System);

    private static CreateInvoiceRequest NewRequest(Guid contractId, (DateOnly Start, DateOnly End) period) => new()
    {
        ContractId = contractId,
        PeriodStart = period.Start,
        PeriodEnd = period.End,
        Items =
        [
            new InvoiceItemInputDto
            {
                ItemType = InvoiceItemType.Rent,
                Description = "Tiền thuê phòng",
                Unit = "tháng",
                Quantity = 1m,
                UnitPrice = 1_500_000m,
            },
        ],
    };

    /// <summary>Kỳ tiếp theo, vẫn nằm trong thời hạn hợp đồng 6 tháng của nền test.</summary>
    private static (DateOnly Start, DateOnly End) NextPeriod()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddMonths(1));
        return (start, start.AddDays(15));
    }

    private static (DateOnly Start, DateOnly End) CurrentPeriod()
    {
        var start = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        return (start, start.AddDays(15));
    }

    private async Task<InvoiceStatus> InvoiceStatusAsync(int invoiceId)
    {
        await using var read = fixture.CreateContext();
        return await read.Invoices.AsNoTracking()
            .Where(invoice => invoice.Id == invoiceId)
            .Select(invoice => invoice.Status)
            .FirstAsync();
    }

    private async Task<bool> InvoiceExistsAsync(int invoiceId)
    {
        await using var read = fixture.CreateContext();
        return await read.Invoices.AsNoTracking().AnyAsync(invoice => invoice.Id == invoiceId);
    }

    private async Task<int> CountInvoicesAsync()
    {
        await using var read = fixture.CreateContext();
        return await read.Invoices.CountAsync();
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
        var togglePolicy = PermissionMatrixWorld.NewRealTogglePolicy(fixture);
        var approvals = PermissionMatrixWorld.NewRealApprovals(db);
        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = new RoomService(fixture, TimeProvider.System, togglePolicy,
            new ActiveContractRoomUsageChecker(fixture, TimeProvider.System));
        var tenants = new TenantService(fixture, TimeProvider.System, togglePolicy, approvals);
        var contracts = new ContractService(db, TimeProvider.System, togglePolicy, approvals);

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

        var tenantA = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Nguyễn Văn Khách",
            PhoneNumber = "0901111111",
            IdNumber = "079201000001",
        }, new TenantCaller("owner-a", Roles.ChuTro));
        var tenantB = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Trần Thị Khách",
            PhoneNumber = "0902222222",
            IdNumber = "079201000002",
        }, new TenantCaller("owner-b", Roles.ChuTro));
        Assert.True(tenantA.Succeeded, tenantA.ErrorMessage);
        Assert.True(tenantB.Succeeded, tenantB.ErrorMessage);
        Assert.NotNull(tenantB.Tenant);
        await tenants.LinkAccountAsync(tenantA.Tenant!.Id, "khach-1", tenantA.Tenant.RowVersion,
            new TenantCaller("owner-a", Roles.ChuTro));

        var today = DateTime.UtcNow.Date;
        var contractA = await contracts.CreateContractAsync(
            new CreateContractRequest(roomA.Room!.Id, today, today.AddMonths(6), [tenantA.Tenant.Id]), OwnerA);
        var contractB = await contracts.CreateContractAsync(
            new CreateContractRequest(roomB.Room!.Id, today, today.AddMonths(6), [tenantB.Tenant.Id]), OwnerB);
        await contracts.ApproveContractAsync(
            contractA.ContractId, new ApproveContractRequestDto { IsApproved = true }, OwnerA);
        await contracts.ApproveContractAsync(
            contractB.ContractId, new ApproveContractRequestDto { IsApproved = true }, OwnerB);
        Assert.Equal(ContractStatus.Approved, await ContractStatusAsync(contractA.ContractId));
        Assert.Equal(ContractStatus.Approved, await ContractStatusAsync(contractB.ContractId));

        // Hóa đơn nền tạo qua chính InvoiceService: nháp của nhà A (dùng cho OP36),
        // đã phát hành của nhà B (dùng để chứng minh chặn truy cập chéo ở OP34).
        var invoices = NewInvoices(db);
        var invoiceA = await invoices.CreateDraftAsync(NewRequest(contractA.ContractId, CurrentPeriod()), OwnerA);
        var invoiceB = await invoices.CreateAndIssueAsync(NewRequest(contractB.ContractId, CurrentPeriod()), OwnerB);

        return (db, new SeedData(
            contractA.ContractId,
            contractB.ContractId,
            invoiceA.Id,
            invoiceB.Id));
    }

    private async Task<ContractStatus> ContractStatusAsync(Guid contractId)
    {
        await using var read = fixture.CreateContext();
        return await read.Contracts.AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Select(contract => contract.Status)
            .FirstAsync();
    }

    private sealed record SeedData(
        Guid ContractAId,
        Guid ContractBId,
        int InvoiceAId,
        int InvoiceBId);

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô G35 ⚠️: OP34 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G35_OP34_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G35");

    /// <summary>Ô F36 ⚙️: OP35 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F36_OP35_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F36");

    /// <summary>Ô F37 ⚙️: OP36 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F37_OP36_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F37");

    /// <summary>Ô G38 ✅: OP37 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G38_OP37_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G38");

    /// <summary>Ô D39 ❌: OP38 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D39_OP38_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D39");

    /// <summary>Ô E39 ✅: OP38 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E39_OP38_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E39");

    /// <summary>Ô F39 ✅: OP38 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F39_OP38_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F39");

    /// <summary>Ô G39 ❌: OP38 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G39_OP38_KhachThue_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G39");

    /// <summary>Ô D40 ❌: OP39 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D40_OP39_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D40");

    /// <summary>Ô E40 ✅: OP39 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E40_OP39_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E40");

    /// <summary>Ô F40 ❌: OP39 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F40_OP39_QuanLy_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F40");

    /// <summary>Ô G40 ❌: OP39 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G40_OP39_KhachThue_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G40");
}
