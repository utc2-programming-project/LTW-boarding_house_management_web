using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Features.PermissionMatrix;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Services;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Maintenance;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Services;
using Web.Features.Settings.Enums;

namespace NhaTro.IntegrationTests.Features.TestReadiness;

/// <summary>
/// VHT-47 đợt 3A — kiểm phạm vi XUYÊN P4/P6/P7 bằng ID chéo, không chỉ kiểm menu:
/// đọc/sửa hợp đồng, hóa đơn, sự cố của tòa khác phải bị chặn và DB không đổi.
/// Bổ sung D1–D17: Quản lý gọi thẳng thao tác công tắc khi "Duyệt"/"Không thể",
/// và yêu cầu duyệt không được rò sang Chủ trọ khác.
/// Dùng lại <see cref="PermissionMatrixWorld"/> (2 tòa, 6 tài khoản, HĐ/hóa đơn/sự cố 2 nhà).
/// </summary>
public sealed class Vht47CrossScopeTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

    private async Task<PermissionMatrixWorld> NewWorldAsync()
    {
        var world = new PermissionMatrixWorld(fixture);
        await world.InitializeAsync();
        return world;
    }

    private static CreateInvoiceRequest InvoiceRequest(Guid contractId, int monthOffset) => new()
    {
        ContractId = contractId,
        PeriodStart = Today.AddMonths(monthOffset),
        PeriodEnd = Today.AddMonths(monthOffset + 1).AddDays(-1),
        Items =
        [
            new InvoiceItemInputDto
            {
                ItemType = InvoiceItemType.Rent,
                Description = "Tiền thuê kiểm scope",
                Quantity = 1,
                UnitPrice = 1_500_000m,
            },
        ],
    };

    private static async Task<int> CreateInvoiceForContractBAsync(PermissionMatrixWorld world)
    {
        var invoice = await world.NewInvoices().CreateDraftAsync(
            InvoiceRequest(world.Data.ContractBId, 0), world.OwnerB);
        return invoice.Id;
    }

    private static async Task<int> CountInvoicesAsync(PermissionMatrixWorld world, Guid contractId)
    {
        await using var db = world.NewDb();
        return await db.Invoices.AsNoTracking().CountAsync(item => item.ContractId == contractId);
    }

    private static async Task<ContractStatus> ContractStatusAsync(PermissionMatrixWorld world, Guid contractId)
    {
        await using var db = world.NewDb();
        return await db.Contracts.AsNoTracking()
            .Where(item => item.ContractId == contractId)
            .Select(item => item.Status)
            .SingleAsync();
    }

    private static async Task<InvoiceStatus> InvoiceStatusAsync(PermissionMatrixWorld world, int invoiceId)
    {
        await using var db = world.NewDb();
        return await db.Invoices.AsNoTracking()
            .Where(item => item.Id == invoiceId)
            .Select(item => item.Status)
            .SingleAsync();
    }

    private static async Task<IncidentStatus> IncidentStatusAsync(PermissionMatrixWorld world, int incidentId)
    {
        await using var db = world.NewDb();
        return await db.MaintenanceIncidents.AsNoTracking()
            .Where(item => item.Id == incidentId)
            .Select(item => item.Status)
            .SingleAsync();
    }

    private static async Task<decimal> DepositAsync(PermissionMatrixWorld world, Guid contractId)
    {
        await using var db = world.NewDb();
        return await db.Contracts.AsNoTracking()
            .Where(item => item.ContractId == contractId)
            .Select(item => item.DepositAmount)
            .SingleAsync();
    }

    // ===== ĐỌC (R): 4 vai trò × 3 phân hệ, gọi thẳng bằng ID chéo =====

    [SqlServerFact]
    public async Task R01_ChuTroB_bi_chan_doc_du_lieu_nha_A()
    {
        await using var world = await NewWorldAsync();

        await Assert.ThrowsAsync<ContractNotFoundException>(
            () => world.NewContracts().GetAsync(world.Data.ContractAId, world.OwnerB));
        Assert.DoesNotContain(
            await world.NewContracts().ListAsync(null, world.OwnerB),
            item => item.ContractId == world.Data.ContractAId);

        var invoices = await world.NewInvoiceQuery().GetInvoicesAsync(
            new InvoiceFilter { ContractId = world.Data.ContractAId }, world.OwnerB);
        Assert.Empty(invoices.Items);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => world.NewInvoiceQuery().GetInvoiceAsync(world.Data.InvoiceDraftId, world.OwnerB));

        var incident = await world.NewIncidents().GetAsync(world.Data.IncidentAId, world.OwnerB);
        Assert.False(incident.Succeeded);
        Assert.Equal(IncidentErrorCodes.NotFound, incident.ErrorCode);
        Assert.DoesNotContain(
            await world.NewIncidents().ListAsync(world.OwnerB),
            item => item.Id == world.Data.IncidentAId);
    }

    [SqlServerFact]
    public async Task R02_QuanLy_khong_duoc_giao_khong_doc_duoc_du_lieu_nha_B()
    {
        await using var world = await NewWorldAsync();
        var invoiceBId = await CreateInvoiceForContractBAsync(world);

        await Assert.ThrowsAsync<ContractNotFoundException>(
            () => world.NewContracts().GetAsync(world.Data.ContractBId, world.Manager));
        Assert.DoesNotContain(
            await world.NewContracts().ListAsync(null, world.Manager),
            item => item.ContractId == world.Data.ContractBId);

        var invoices = await world.NewInvoiceQuery().GetInvoicesAsync(
            new InvoiceFilter { ContractId = world.Data.ContractBId }, world.Manager);
        Assert.Empty(invoices.Items);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => world.NewInvoiceQuery().GetInvoiceAsync(invoiceBId, world.Manager));

        var incident = await world.NewIncidents().GetAsync(world.Data.IncidentBId, world.Manager);
        Assert.False(incident.Succeeded);
        Assert.Equal(IncidentErrorCodes.NotFound, incident.ErrorCode);
        Assert.DoesNotContain(
            await world.NewIncidents().ListAsync(world.Manager),
            item => item.Id == world.Data.IncidentBId);
    }

    [SqlServerFact]
    public async Task R03_KhachThueA_khong_doc_duoc_du_lieu_nguoi_khac()
    {
        await using var world = await NewWorldAsync();
        var invoiceBId = await CreateInvoiceForContractBAsync(world);

        await Assert.ThrowsAsync<ContractNotFoundException>(
            () => world.NewContracts().GetAsync(world.Data.ContractBId, world.TenantAAsPropertyCaller));
        Assert.DoesNotContain(
            await world.NewContracts().ListAsync(null, world.TenantAAsPropertyCaller),
            item => item.ContractId == world.Data.ContractBId);

        var invoices = await world.NewInvoiceQuery().GetInvoicesAsync(
            new InvoiceFilter { ContractId = world.Data.ContractBId }, world.TenantAAsPropertyCaller);
        Assert.Empty(invoices.Items);
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => world.NewInvoiceQuery().GetInvoiceAsync(invoiceBId, world.TenantAAsPropertyCaller));

        var incident = await world.NewIncidents().GetAsync(
            world.Data.IncidentBId, world.TenantAAsPropertyCaller);
        Assert.False(incident.Succeeded);
        Assert.Equal(IncidentErrorCodes.NotFound, incident.ErrorCode);
    }

    [SqlServerFact]
    public async Task R04_Admin_bi_chan_ca_ba_phan_he()
    {
        await using var world = await NewWorldAsync();
        var admin = world.AdminCaller;

        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => world.NewContracts().ListAsync(null, admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => world.NewInvoiceQuery().GetInvoicesAsync(new InvoiceFilter(), admin));

        Assert.Empty(await world.NewIncidents().ListAsync(admin));
        var incident = await world.NewIncidents().GetAsync(world.Data.IncidentAId, admin);
        Assert.False(incident.Succeeded);
        Assert.Equal(IncidentErrorCodes.NotFound, incident.ErrorCode);
    }

    // ===== GHI (W): hợp đồng =====

    [SqlServerFact]
    public async Task W01_ChuTroB_khong_duyet_duoc_hop_dong_nha_A()
    {
        await using var world = await NewWorldAsync();
        var contracts = world.NewContracts();

        var pending = await contracts.CreateContractAsync(
            new CreateContractRequest(
                world.Data.RoomSpareId, Today.ToDateTime(TimeOnly.MinValue),
                Today.AddMonths(6).ToDateTime(TimeOnly.MinValue), [world.Data.TenantFreeId]),
            world.Manager);
        Assert.Equal(ContractStatus.Pending, await ContractStatusAsync(world, pending.ContractId));

        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => contracts.ApproveContractAsync(
                pending.ContractId, new ApproveContractRequestDto { IsApproved = true }, world.OwnerB));

        Assert.Equal(ContractStatus.Pending, await ContractStatusAsync(world, pending.ContractId));
    }

    [SqlServerFact]
    public async Task W02_QuanLyA_khong_sua_duoc_hop_dong_nha_B()
    {
        await using var world = await NewWorldAsync();
        var contracts = world.NewContracts();
        var depositBefore = await DepositAsync(world, world.Data.ContractBId);

        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => contracts.UpdateDepositAsync(world.Data.ContractBId, depositBefore + 100_000m, world.Manager));
        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => contracts.LiquidateContractAsync(world.Data.ContractBId, world.Manager));

        Assert.Equal(depositBefore, await DepositAsync(world, world.Data.ContractBId));
        Assert.Equal(ContractStatus.Approved, await ContractStatusAsync(world, world.Data.ContractBId));
    }

    [SqlServerFact]
    public async Task W03_ChuTroB_khong_sua_duoc_hop_dong_nha_A()
    {
        await using var world = await NewWorldAsync();
        var contracts = world.NewContracts();
        var depositBefore = await DepositAsync(world, world.Data.ContractAId);

        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => contracts.UpdateDepositAsync(world.Data.ContractAId, depositBefore + 100_000m, world.OwnerB));
        await Assert.ThrowsAsync<ContractAuthorizationException>(
            () => contracts.LiquidateContractAsync(world.Data.ContractAId, world.OwnerB));

        Assert.Equal(depositBefore, await DepositAsync(world, world.Data.ContractAId));
        Assert.Equal(ContractStatus.Approved, await ContractStatusAsync(world, world.Data.ContractAId));
    }

    // ===== GHI (W): hóa đơn =====

    [SqlServerFact]
    public async Task W04_ChuTroB_khong_lap_va_phat_hanh_duoc_hoa_don_nha_A()
    {
        await using var world = await NewWorldAsync();
        var invoices = world.NewInvoices();
        var before = await CountInvoicesAsync(world, world.Data.ContractAId);

        var create = await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => invoices.CreateDraftAsync(InvoiceRequest(world.Data.ContractAId, 3), world.OwnerB));
        Assert.Equal(InvoiceErrorCodes.ContractNotFound, create.ErrorCode);
        Assert.Equal(before, await CountInvoicesAsync(world, world.Data.ContractAId));

        var issue = await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => invoices.IssueAsync(world.Data.InvoiceDraftId, world.OwnerB));
        Assert.Equal(InvoiceErrorCodes.InvoiceNotFound, issue.ErrorCode);
        Assert.Equal(InvoiceStatus.Draft, await InvoiceStatusAsync(world, world.Data.InvoiceDraftId));
    }

    [SqlServerFact]
    public async Task W05_QuanLyA_khong_lap_va_phat_hanh_duoc_hoa_don_nha_B()
    {
        await using var world = await NewWorldAsync();
        var invoiceBId = await CreateInvoiceForContractBAsync(world);
        var invoices = world.NewInvoices();
        var before = await CountInvoicesAsync(world, world.Data.ContractBId);

        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => invoices.CreateDraftAsync(InvoiceRequest(world.Data.ContractBId, 1), world.Manager));
        Assert.Equal(before, await CountInvoicesAsync(world, world.Data.ContractBId));

        await Assert.ThrowsAsync<InvoiceNotFoundException>(
            () => invoices.IssueAsync(invoiceBId, world.Manager));
        Assert.Equal(InvoiceStatus.Draft, await InvoiceStatusAsync(world, invoiceBId));
    }

    [SqlServerFact]
    public async Task W06_KhachThue_khong_lap_duoc_hoa_don()
    {
        await using var world = await NewWorldAsync();
        var before = await CountInvoicesAsync(world, world.Data.ContractAId);

        var ex = await Assert.ThrowsAsync<InvoiceAuthorizationException>(
            () => world.NewInvoices().CreateDraftAsync(
                InvoiceRequest(world.Data.ContractAId, 3), world.TenantAAsPropertyCaller));
        Assert.Equal(InvoiceErrorCodes.Forbidden, ex.ErrorCode);
        Assert.Equal(before, await CountInvoicesAsync(world, world.Data.ContractAId));
    }

    // ===== GHI (W): sự cố =====

    [SqlServerFact]
    public async Task W07_ChuTroB_khong_tao_va_sua_duoc_su_co_nha_A()
    {
        await using var world = await NewWorldAsync();
        var incidents = world.NewIncidents();

        var created = await incidents.CreateAsync(new IncidentCreateRequest
        {
            RoomId = world.Data.RoomAId,
            Title = "Tạo chéo từ chủ trọ B",
            Description = "Phải bị chặn",
            Category = IncidentCategory.Equipment,
            Urgency = IncidentUrgency.Normal,
        }, world.OwnerB);
        Assert.False(created.Succeeded);
        Assert.Equal(IncidentErrorCodes.RoomNotFound, created.ErrorCode);

        var update = await incidents.UpdateAsync(
            world.Data.IncidentAId, new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, world.OwnerB);
        Assert.False(update.Succeeded);
        Assert.Equal(IncidentErrorCodes.NotFound, update.ErrorCode);
        Assert.Equal(IncidentStatus.New, await IncidentStatusAsync(world, world.Data.IncidentAId));
    }

    [SqlServerFact]
    public async Task W08_QuanLyA_khong_sua_duoc_su_co_nha_B()
    {
        await using var world = await NewWorldAsync();

        var update = await world.NewIncidents().UpdateAsync(
            world.Data.IncidentBId, new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, world.Manager);

        Assert.False(update.Succeeded);
        Assert.Equal(IncidentErrorCodes.NotFound, update.ErrorCode);
        Assert.Equal(IncidentStatus.New, await IncidentStatusAsync(world, world.Data.IncidentBId));
    }

    [SqlServerFact]
    public async Task W09_KhachThueA_chi_tao_duoc_su_co_phong_dang_thue()
    {
        await using var world = await NewWorldAsync();
        var incidents = world.NewIncidents();

        var crossRoom = await incidents.CreateAsync(new IncidentCreateRequest
        {
            RoomId = world.Data.RoomBId,
            Title = "Báo hỏng phòng người khác",
            Description = "Phải bị chặn",
            Category = IncidentCategory.Equipment,
            Urgency = IncidentUrgency.Normal,
        }, world.TenantAAsPropertyCaller);
        Assert.False(crossRoom.Succeeded);
        Assert.Equal(IncidentErrorCodes.RoomNotFound, crossRoom.ErrorCode);

        var ownRoom = await incidents.CreateAsync(new IncidentCreateRequest
        {
            RoomId = world.Data.RoomAId,
            Title = "Báo hỏng phòng mình",
            Description = "Thiết bị trong phòng mình bị hỏng",
            Category = IncidentCategory.Equipment,
            Urgency = IncidentUrgency.Normal,
        }, world.TenantAAsPropertyCaller);
        Assert.True(ownRoom.Succeeded, ownRoom.ErrorCode);
    }

    [SqlServerFact]
    public async Task W10_KhachThue_khong_sua_duoc_trang_thai_su_co()
    {
        await using var world = await NewWorldAsync();

        var update = await world.NewIncidents().UpdateAsync(
            world.Data.IncidentAId, new IncidentUpdateRequest { Status = IncidentStatus.Completed },
            world.TenantAAsPropertyCaller);

        Assert.False(update.Succeeded);
        Assert.Equal(IncidentErrorCodes.Forbidden, update.ErrorCode);
        Assert.Equal(IncidentStatus.New, await IncidentStatusAsync(world, world.Data.IncidentAId));
    }

    // ===== CÔNG TẮC & HÀNG ĐỢI DUYỆT (bổ sung D1–D17) =====

    [SqlServerFact]
    public async Task S01_QuanLy_goi_thang_SW10_theo_ba_che_do_cong_tac()
    {
        await using var world = await NewWorldAsync();
        var invoices = world.NewInvoices();
        var before = await CountInvoicesAsync(world, world.Data.ContractAId);

        await world.SetSwitchAsync("SW-10", ManagerToggleMode.Approve);
        var approval = await Assert.ThrowsAsync<InvoiceOperationException>(
            () => invoices.CreateDraftAsync(InvoiceRequest(world.Data.ContractAId, 3), world.Manager));
        Assert.Equal(InvoiceErrorCodes.ApprovalRequired, approval.ErrorCode);
        Assert.Equal(before, await CountInvoicesAsync(world, world.Data.ContractAId));
        var mine = await world.NewApprovals().ListMineAsync(world.ManagerApproval);
        Assert.Contains(mine.Items, item => item.OperationCode == "SW-10");

        await world.SetSwitchAsync("SW-10", ManagerToggleMode.Deny);
        var denied = await Assert.ThrowsAsync<InvoiceOperationException>(
            () => invoices.CreateDraftAsync(InvoiceRequest(world.Data.ContractAId, 4), world.Manager));
        Assert.Equal(InvoiceErrorCodes.ApprovalDenied, denied.ErrorCode);
        Assert.Equal(before, await CountInvoicesAsync(world, world.Data.ContractAId));

        await world.SetSwitchAsync("SW-10", ManagerToggleMode.Allow);
        var draft = await invoices.CreateDraftAsync(InvoiceRequest(world.Data.ContractAId, 5), world.Manager);
        Assert.Equal(InvoiceStatus.Draft, await InvoiceStatusAsync(world, draft.Id));
    }

    [SqlServerFact]
    public async Task S02_QuanLy_goi_thang_SW13_theo_ba_che_do_cong_tac()
    {
        await using var world = await NewWorldAsync();
        var incidents = world.NewIncidents();

        await world.SetSwitchAsync("SW-13", ManagerToggleMode.Approve);
        var pendingIncident = world.Data.IncidentAId;
        var pending = await incidents.UpdateAsync(
            pendingIncident, new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, world.Manager);
        Assert.NotNull(pending.ApprovalRequest);
        Assert.Equal(IncidentStatus.New, await IncidentStatusAsync(world, pendingIncident));

        await world.SetSwitchAsync("SW-13", ManagerToggleMode.Deny);
        var denied = await incidents.UpdateAsync(
            world.Data.IncidentAId, new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, world.Manager);
        Assert.False(denied.Succeeded);
        Assert.Equal(IncidentErrorCodes.Forbidden, denied.ErrorCode);
        Assert.Equal(IncidentStatus.New, await IncidentStatusAsync(world, world.Data.IncidentAId));

        await world.SetSwitchAsync("SW-13", ManagerToggleMode.Allow);
        var applied = await incidents.UpdateAsync(
            world.Data.IncidentAId, new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, world.Manager);
        Assert.True(applied.Succeeded, applied.ErrorCode);
        Assert.Equal(IncidentStatus.InProgress, await IncidentStatusAsync(world, world.Data.IncidentAId));
    }

    [SqlServerFact]
    public async Task S03_ChuTroB_khong_duyet_duoc_yeu_cau_cua_nha_A()
    {
        await using var world = await NewWorldAsync();
        await world.SetSwitchAsync("SW-10", ManagerToggleMode.Approve);

        var approval = await Assert.ThrowsAsync<InvoiceOperationException>(
            () => world.NewInvoices().CreateDraftAsync(InvoiceRequest(world.Data.ContractAId, 3), world.Manager));
        Assert.Equal(InvoiceErrorCodes.ApprovalRequired, approval.ErrorCode);

        var request = (await world.NewApprovals().ListMineAsync(world.ManagerApproval)).Items
            .Single(item => item.OperationCode == "SW-10");

        var forbidden = await world.NewApprovals().ApproveAsync(request.Id, new ApprovalCaller(PermissionMatrixWorld.OwnerBId, Roles.ChuTro));
        Assert.False(forbidden.Succeeded);
        Assert.Equal(ApprovalErrorCodes.Forbidden, forbidden.ErrorCode);
        Assert.Equal(2, await CountInvoicesAsync(world, world.Data.ContractAId)); // seed có đúng 2 hóa đơn A, không thêm
    }

    [SqlServerFact]
    public async Task S04_QuanLy_khong_tao_duoc_yeu_cau_gan_PropertyId_toa_khac()
    {
        await using var world = await NewWorldAsync();
        var approvals = world.NewApprovals();

        var result = await approvals.CreateAsync(new CreateApprovalRequest
        {
            OperationCode = "SW-10",
            TargetType = ApprovalTargetType.Contract,
            TargetKey = world.Data.ContractAId.ToString(),
            PropertyId = world.Data.PropertyBId,
            PayloadJson = """{"contractId":"00000000-0000-0000-0000-000000000000","items":[]}""",
        }, world.ManagerApproval);

        Assert.False(result.Succeeded);
        Assert.Equal(ApprovalErrorCodes.Forbidden, result.ErrorCode);

        var inboxB = await approvals.ListInboxAsync(new ApprovalCaller(PermissionMatrixWorld.OwnerBId, Roles.ChuTro));
        Assert.DoesNotContain(
            inboxB.Items, item => item.TargetKey == world.Data.ContractAId.ToString());
    }
}
