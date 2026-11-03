using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.Services;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Enums;
using Web.Features.Tenants.Services;

namespace NhaTro.IntegrationTests.Features.Tenants;

/// <summary>
/// VHT-99 (2E) — thùng rác hồ sơ khách thuê: xóa mềm, chặn khi còn hợp đồng hiệu lực,
/// khôi phục chỉ Chủ trọ, giữ liên kết tài khoản/tệp giấy tờ và đi theo công tắc SW-06.
/// </summary>
public sealed class TenantTrashIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly TenantCaller Owner = new("trash-owner", Roles.ChuTro);
    private static readonly TenantCaller Manager = new("trash-manager", Roles.QuanLy);
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    private TenantService NewService(
        IManagerTogglePolicy? policy = null,
        IManagerApprovalService? approvals = null) =>
        new(fixture, TimeProvider.System, policy ?? new AllowAllTogglePolicy(), approvals);

    private static ManagerTogglePolicy NewTogglePolicy(
        params (string Code, ManagerToggleMode Mode)[] modes) =>
        new(new FakeToggleReader(modes), NullLogger<ManagerTogglePolicy>.Instance);

    [SqlServerFact]
    public async Task Xoa_mem_roi_khoi_phuc_giu_lien_ket_tai_khoan_va_tep_giay_to()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "trash-tenant", Roles.KhachThue);

        var service = NewService();
        var created = await service.CreateAsync(NewTenant("Nguyễn An", "0901234567"), Owner);
        Assert.True(created.Succeeded, created.ErrorMessage);
        var id = created.Tenant!.Id;

        var linked = await service.LinkAccountAsync(
            id, "trash-tenant", created.Tenant.RowVersion, Owner);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        // Tệp giấy tờ là dòng riêng, không được xóa theo hồ sơ.
        db.StoredFiles.Add(new StoredFile
        {
            FileName = "cccd.pdf",
            StoredFileName = $"{Guid.NewGuid():N}.pdf",
            RelativePath = "test/cccd.pdf",
            ContentType = "application/pdf",
            FileCategory = FileCategory.IdentityDocument,
            UploadedByUserId = Owner.UserId,
            UploadedAt = DateTimeOffset.UtcNow,
            RelatedEntityType = RelatedEntityType.Tenant,
            RelatedEntityId = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        await db.SaveChangesAsync();

        var deleted = await service.DeleteAsync(id, linked.Tenant!.RowVersion, Owner);
        Assert.True(deleted.Succeeded, deleted.ErrorMessage);

        // Truy vấn thường không còn thấy hồ sơ; hồ sơ vẫn nằm trong thùng rác.
        Assert.Equal(TenantErrorCodes.TenantNotFound, (await service.GetAsync(id, Owner)).ErrorCode);
        var trash = await service.ListTrashAsync(new TenantTrashFilterRequest(), Owner);
        var item = Assert.Single(trash.Items);
        Assert.Equal(id, item.Id);
        Assert.True(item.HasAccount);
        Assert.Equal(1, item.DocumentCount);
        Assert.NotNull(item.DeletedAt);

        var restored = await service.RestoreAsync(id, item.RowVersion, Owner);
        Assert.True(restored.Succeeded, restored.ErrorMessage);
        Assert.True(restored.Tenant!.HasAccount);

        var reloaded = await service.GetAsync(id, Owner);
        Assert.True(reloaded.Succeeded, reloaded.ErrorMessage);
        Assert.True(reloaded.Tenant!.HasAccount);
        Assert.True(await db.StoredFiles.IgnoreQueryFilters().AnyAsync(
            file => file.RelatedEntityId == id.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Empty((await service.ListTrashAsync(new TenantTrashFilterRequest(), Owner)).Items);
    }

    [SqlServerFact]
    public async Task Chan_xoa_khi_con_hop_dong_hieu_luc()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner.UserId, Roles.ChuTro);

        var service = NewService();
        var created = await service.CreateAsync(NewTenant("Bình", "0902222222"), Owner);
        var id = created.Tenant!.Id;

        db.Properties.Add(new Property { Name = "Nhà A", Address = "1 Lê Lợi", OwnerId = Owner.UserId });
        await db.SaveChangesAsync();
        var property = await db.Properties.SingleAsync(p => p.OwnerId == Owner.UserId);
        var room = new Room
        {
            PropertyId = property.Id,
            Code = "P101",
            AreaM2 = 20m,
            StandardPrice = 1_000_000m,
            Status = RoomStatus.Rented,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        await AddContractAsync(db, id, room.Id, ContractStatus.Approved,
            Today.AddDays(-5), Today.AddDays(5), "HD-ACTIVE");

        var blocked = await service.DeleteAsync(id, created.Tenant.RowVersion, Owner);
        Assert.False(blocked.Succeeded);
        Assert.Equal(TenantErrorCodes.TenantInUse, blocked.ErrorCode);

        // Hợp đồng hết hiệu lực thì xóa mềm được.
        var contract = await db.Contracts.SingleAsync(c => c.ContractNumber == "HD-ACTIVE");
        contract.EndDate = Today.AddDays(-1);
        await db.SaveChangesAsync();

        var afterExpiry = await service.DeleteAsync(id, created.Tenant.RowVersion, Owner);
        Assert.True(afterExpiry.Succeeded, afterExpiry.ErrorMessage);
    }

    [SqlServerFact]
    public async Task Quan_ly_xoa_di_theo_cong_tac_SW06_va_chi_Chu_tro_khoi_phuc()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Manager.UserId, Roles.QuanLy);

        // Phân công để Chủ trọ thấy yêu cầu của Quản lý trên hồ sơ chưa gắn hợp đồng.
        db.Properties.Add(new Property { Name = "Nhà A", Address = "1 Lê Lợi", OwnerId = Owner.UserId });
        await db.SaveChangesAsync();
        var property = await db.Properties.SingleAsync(p => p.OwnerId == Owner.UserId);
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = property.Id,
            ManagerUserId = Manager.UserId,
            AssignedByUserId = Owner.UserId,
        });
        await db.SaveChangesAsync();

        var created = await NewService().CreateAsync(NewTenant("Cường", "0903333333"), Owner);
        var id = created.Tenant!.Id;

        // Không thể -> 403, không ghi.
        var denied = NewService(NewTogglePolicy(("SW-06", ManagerToggleMode.Deny)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => denied.DeleteAsync(id, created.Tenant.RowVersion, Manager));

        // Cho phép -> xóa ngay.
        var allowed = NewService(NewTogglePolicy(("SW-06", ManagerToggleMode.Allow)));
        var direct = await allowed.DeleteAsync(id, created.Tenant.RowVersion, Manager);
        Assert.True(direct.Succeeded, direct.ErrorMessage);

        // Chỉ Chủ trọ khôi phục.
        var trash = await NewService().ListTrashAsync(new TenantTrashFilterRequest(), Owner);
        var item = Assert.Single(trash.Items);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewService().RestoreAsync(id, item.RowVersion, Manager));

        var restored = await NewService().RestoreAsync(id, item.RowVersion, Owner);
        Assert.True(restored.Succeeded, restored.ErrorMessage);

        // Duyệt -> tạo yêu cầu, chưa đổi dữ liệu; Chủ trọ duyệt mới thực sự xóa mềm.
        var approvalDb = fixture.CreateContext();
        var approvalService = new ManagerApprovalService(
            approvalDb,
            TimeProvider.System,
            [new TenantDeleteApprovalApplier(TimeProvider.System)],
            NullLogger<ManagerApprovalService>.Instance);
        var pendingService = NewService(
            NewTogglePolicy(("SW-06", ManagerToggleMode.Approve)), approvalService);

        var pending = await pendingService.DeleteAsync(id, restored.Tenant!.RowVersion, Manager);
        Assert.False(pending.Succeeded);
        Assert.Equal(TenantErrorCodes.ApprovalRequired, pending.ErrorCode);
        var request = await approvalDb.ManagerApprovalRequests.SingleAsync();
        Assert.Equal("SW-06", request.OperationCode);
        Assert.Equal(id.ToString(System.Globalization.CultureInfo.InvariantCulture), request.TargetKey);

        await using var approveDb = fixture.CreateContext();
        var approved = await new ManagerApprovalService(
            approveDb,
            TimeProvider.System,
            [new TenantDeleteApprovalApplier(TimeProvider.System)],
            NullLogger<ManagerApprovalService>.Instance)
            .ApproveAsync(request.Id, new ApprovalCaller(Owner.UserId, Roles.ChuTro));

        Assert.True(approved.Succeeded, approved.ErrorMessage);

        await using var verifyDb = fixture.CreateContext();
        var deletedTenant = await verifyDb.Tenants.IgnoreQueryFilters().SingleAsync(t => t.Id == id);
        Assert.True(deletedTenant.IsDeleted);
        Assert.Equal(Manager.UserId, deletedTenant.DeletedByUserId);
        Assert.NotNull(deletedTenant.DeletedAt);
        await approvalDb.DisposeAsync();
    }

    private static CreateTenantRequest NewTenant(string fullName, string phone) => new()
    {
        FullName = fullName,
        PhoneNumber = phone,
    };

    private static async Task AddContractAsync(
        ApplicationDbContext db, int tenantId, int roomId, ContractStatus status,
        DateTime startDate, DateTime endDate, string contractNumber)
    {
        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = contractNumber,
            RoomId = roomId,
            StartDate = startDate,
            EndDate = endDate,
            SnapshotPrice = 1_000_000m,
            DepositAmount = 200_000m,
            Status = status,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = tenantId,
        });
        await db.SaveChangesAsync();
    }

    private sealed class AllowAllTogglePolicy : IManagerTogglePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(
            ManagerAction action, string role, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.Allowed);

        public Task<RoomChangeDecision> EvaluateAsync(
            string code, string role, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.Allowed);
    }

    private sealed class FakeToggleReader((string Code, ManagerToggleMode Mode)[] modes) : IManagerToggleReader
    {
        private readonly Dictionary<string, ManagerToggleMode> _modes =
            modes.ToDictionary(m => m.Code, m => m.Mode, StringComparer.OrdinalIgnoreCase);

        public Task<ManagerToggleMode> GetModeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(_modes.TryGetValue(code, out var mode) ? mode : ManagerToggleMode.Approve);
    }
}
