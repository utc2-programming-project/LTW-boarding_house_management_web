using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using NhaTro.IntegrationTests.TestSupport;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Entities;

namespace NhaTro.IntegrationTests.Features.Tenants;

/// <summary>
/// VHT-119 Q1-A: phạm vi hồ sơ khách = hồ sơ có hợp đồng tại tòa người gọi ∪ kho chưa gắn hợp đồng.
/// Kiểm cả đọc lẫn ghi, cả Chủ trọ lẫn Quản lý, và quyền giấy tờ của Quản lý theo phân công thật.
/// </summary>
public sealed class TenantScopeIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly TenantCaller OwnerA = new("owner-a", Roles.ChuTro);
    private static readonly TenantCaller OwnerB = new("owner-b", Roles.ChuTro);
    private static readonly TenantCaller ManagerA = new("ql-a", Roles.QuanLy);

    private TenantService NewService() =>
        new(fixture, TimeProvider.System, new AllowAllManagerTogglePolicy());

    [SqlServerFact]
    public async Task Chu_tro_chi_thay_ho_so_minh_so_huu_hoac_gan_toa_minh()
    {
        var seed = await SeedAsync();

        var page = await NewService().ListAsync(
            new TenantFilterRequest { PageSize = 100 }, OwnerA);

        // PLAN-005: hồ sơ "kho chung" (OwnerId null) không còn hiện cho chủ trọ.
        Assert.Equal(1, page.TotalCount);
        Assert.Contains(page.Items, item => item.Id == seed.TenantAId);
        Assert.DoesNotContain(page.Items, item => item.Id == seed.PoolTenantId);
        Assert.DoesNotContain(page.Items, item => item.Id == seed.TenantBId);

        var summary = await NewService().GetSummaryAsync(OwnerA);
        Assert.Equal(1, summary.TotalCount);
        Assert.Equal(1, summary.ActiveCount);
    }

    [SqlServerFact]
    public async Task Quan_ly_chi_thay_ho_so_toa_duoc_phan_cong()
    {
        var seed = await SeedAsync();

        var page = await NewService().ListAsync(
            new TenantFilterRequest { PageSize = 100 }, ManagerA);

        Assert.Equal(1, page.TotalCount);
        Assert.Contains(page.Items, item => item.Id == seed.TenantAId);
        Assert.DoesNotContain(page.Items, item => item.Id == seed.PoolTenantId);
        Assert.DoesNotContain(page.Items, item => item.Id == seed.TenantBId);

        // D9: Quản lý xem chi tiết nhưng số CCCD luôn bị che.
        var detail = await NewService().GetAsync(seed.TenantAId, ManagerA);
        Assert.True(detail.Succeeded, detail.ErrorMessage);
        Assert.NotEqual(seed.TenantACitizenId, detail.Tenant!.IdNumber);
        Assert.Contains('*', detail.Tenant.IdNumber!);
    }

    [SqlServerFact]
    public async Task Thu_hoi_phan_cong_mat_quyen_ngay()
    {
        var seed = await SeedAsync();

        await using (var db = fixture.CreateContext())
        {
            var assignment = await db.PropertyManagers.FirstAsync(a => a.Id == seed.AssignmentId);
            assignment.RevokedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var page = await NewService().ListAsync(
            new TenantFilterRequest { PageSize = 100 }, ManagerA);
        Assert.Equal(0, page.TotalCount);

        var detail = await NewService().GetAsync(seed.TenantAId, ManagerA);
        Assert.False(detail.Succeeded);
        Assert.Equal(TenantErrorCodes.TenantNotFound, detail.ErrorCode);
    }

    [SqlServerFact]
    public async Task Doc_cheo_tra_TenantNotFound()
    {
        var seed = await SeedAsync();
        var service = NewService();

        var ownerCross = await service.GetAsync(seed.TenantBId, OwnerA);
        Assert.Equal(TenantErrorCodes.TenantNotFound, ownerCross.ErrorCode);

        var managerCross = await service.GetAsync(seed.TenantBId, ManagerA);
        Assert.Equal(TenantErrorCodes.TenantNotFound, managerCross.ErrorCode);

        var reverse = await service.GetAsync(seed.TenantAId, OwnerB);
        Assert.Equal(TenantErrorCodes.TenantNotFound, reverse.ErrorCode);
    }

    [SqlServerFact]
    public async Task Ghi_cheo_tra_TenantNotFound_va_khong_doi_du_lieu()
    {
        var seed = await SeedAsync();
        var service = NewService();

        var update = await service.UpdateAsync(seed.TenantBId, new UpdateTenantRequest
        {
            FullName = "Bị sửa chéo",
            PhoneNumber = seed.TenantBPhone,
            RowVersion = await RowVersionAsync(seed.TenantBId),
        }, OwnerA);
        Assert.Equal(TenantErrorCodes.TenantNotFound, update.ErrorCode);

        var setActive = await service.SetActiveAsync(
            seed.TenantBId, false, await RowVersionAsync(seed.TenantBId), OwnerA);
        Assert.Equal(TenantErrorCodes.TenantNotFound, setActive.ErrorCode);

        var link = await service.LinkAccountAsync(
            seed.TenantBId, "khach-b", await RowVersionAsync(seed.TenantBId), OwnerA);
        Assert.Equal(TenantErrorCodes.TenantNotFound, link.ErrorCode);

        await using (var db = fixture.CreateContext())
        {
            var unchanged = await db.Tenants.AsNoTracking().FirstAsync(t => t.Id == seed.TenantBId);
            Assert.Equal("Khách B", unchanged.FullName);
            Assert.True(unchanged.IsActive);
            Assert.Null(unchanged.UserId);
        }

        // Chủ trọ B (hợp đồng Pending) vẫn gắn được; A không gỡ được tài khoản đó.
        var linked = await service.LinkAccountAsync(
            seed.TenantBId, "khach-b", await RowVersionAsync(seed.TenantBId), OwnerB);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        var unlink = await service.UnlinkAccountAsync(seed.TenantBId, linked.Tenant!.RowVersion, OwnerA);
        Assert.Equal(TenantErrorCodes.TenantNotFound, unlink.ErrorCode);

        await using var checkDb = fixture.CreateContext();
        Assert.Equal(
            "khach-b",
            (await checkDb.Tenants.AsNoTracking().FirstAsync(t => t.Id == seed.TenantBId)).UserId);
    }

    [SqlServerFact]
    public async Task Picker_va_summary_theo_pham_vi()
    {
        var seed = await SeedAsync();
        var service = NewService();

        var pickerA = await service.SearchForPickerAsync(new TenantPickerRequest(), OwnerA);
        Assert.Single(pickerA);
        Assert.Contains(pickerA, item => item.Id == seed.TenantAId);
        Assert.DoesNotContain(pickerA, item => item.Id == seed.PoolTenantId);
        Assert.DoesNotContain(pickerA, item => item.Id == seed.TenantBId);

        var pickerManager = await service.SearchForPickerAsync(new TenantPickerRequest(), ManagerA);
        Assert.Single(pickerManager);

        var pickerB = await service.SearchForPickerAsync(new TenantPickerRequest(), OwnerB);
        Assert.Contains(pickerB, item => item.Id == seed.TenantBId);
        Assert.DoesNotContain(pickerB, item => item.Id == seed.PoolTenantId);
        Assert.DoesNotContain(pickerB, item => item.Id == seed.TenantAId);
    }

    [SqlServerFact]
    public async Task Trung_cccd_ho_so_ngoai_pham_vi_khong_lo_PossibleDuplicates()
    {
        var seed = await SeedAsync();
        var service = NewService();

        var outOfScope = await service.CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách mới",
            PhoneNumber = "0901000091",
            IdNumber = seed.TenantBCitizenId,
        }, OwnerA);

        Assert.False(outOfScope.Succeeded);
        Assert.Equal(TenantErrorCodes.DuplicateTenant, outOfScope.ErrorCode);
        Assert.Null(outOfScope.PossibleDuplicates);

        // Hồ sơ trong "kho chung" (OwnerId null) nay ngoài phạm vi mọi chủ trọ: không gợi ý.
        var inScope = await service.CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách mới 2",
            PhoneNumber = "0901000092",
            IdNumber = seed.PoolCitizenId,
        }, OwnerA);

        Assert.False(inScope.Succeeded);
        Assert.Equal(TenantErrorCodes.DuplicateTenant, inScope.ErrorCode);
        Assert.Null(inScope.PossibleDuplicates);
    }

    [SqlServerFact]
    public async Task Trung_sdt_chi_can_nhac_trong_pham_vi()
    {
        var seed = await SeedAsync();
        var service = NewService();

        var outOfScope = await service.CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách ngoài phạm vi",
            PhoneNumber = seed.TenantBPhone,
            IdNumber = "079201000091",
        }, OwnerA);
        Assert.True(outOfScope.Succeeded, outOfScope.ErrorMessage);

        var inScope = await service.CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách trùng kho",
            PhoneNumber = seed.PoolPhone,
            IdNumber = "079201000092",
        }, OwnerA);

        // PLAN-005: hồ sơ kho chung ngoài phạm vi nên không chặn trùng số điện thoại.
        Assert.True(inScope.Succeeded, inScope.ErrorMessage);
    }

    [SqlServerFact]
    public async Task Hop_dong_Pending_van_tinh_la_gan_ket()
    {
        var seed = await SeedAsync();

        var detail = await NewService().GetAsync(seed.TenantBId, OwnerB);
        Assert.True(detail.Succeeded, detail.ErrorMessage);
        Assert.Equal(seed.TenantBId, detail.Tenant!.Id);
    }

    [SqlServerFact]
    public async Task Phong_xoa_mem_khong_orphan_ho_so()
    {
        var seed = await SeedAsync();

        await using (var db = fixture.CreateContext())
        {
            var room = await db.Rooms.IgnoreQueryFilters().FirstAsync(r => r.Id == seed.RoomAId);
            room.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        // Hồ sơ đã có ContractMember nên không rơi về kho chung; vẫn phải thấy được để chủ sửa.
        var detail = await NewService().GetAsync(seed.TenantAId, OwnerA);
        Assert.True(detail.Succeeded, detail.ErrorMessage);
        Assert.Equal(seed.TenantAId, detail.Tenant!.Id);

        var managerDetail = await NewService().GetAsync(seed.TenantAId, ManagerA);
        Assert.True(managerDetail.Succeeded, managerDetail.ErrorMessage);
    }

    [SqlServerFact]
    public async Task Ho_so_xoa_mem_khong_lot_vao_kho_chung()
    {
        var seed = await SeedAsync();

        await using (var db = fixture.CreateContext())
        {
            var poolTenant = await db.Tenants.IgnoreQueryFilters()
                .FirstAsync(t => t.Id == seed.PoolTenantId);
            poolTenant.IsDeleted = true;
            await db.SaveChangesAsync();
        }

        // IgnoreQueryFilters trong chuỗi hợp đồng không được kéo hồ sơ thùng rác vào phạm vi.
        var page = await NewService().ListAsync(new TenantFilterRequest { PageSize = 100 }, OwnerA);
        Assert.Equal(1, page.TotalCount);
        Assert.DoesNotContain(page.Items, item => item.Id == seed.PoolTenantId);

        var detail = await NewService().GetAsync(seed.PoolTenantId, OwnerA);
        Assert.False(detail.Succeeded);
        Assert.Equal(TenantErrorCodes.TenantNotFound, detail.ErrorCode);

        var managerDetail = await NewService().GetAsync(seed.PoolTenantId, ManagerA);
        Assert.Equal(TenantErrorCodes.TenantNotFound, managerDetail.ErrorCode);
    }

    [SqlServerFact]
    public async Task Ho_so_khong_thuoc_chu_tro_nao_thi_chu_tro_khong_thay()
    {
        var seed = await SeedAsync();

        var detail = await NewService().GetAsync(seed.PoolTenantId, OwnerA);
        Assert.False(detail.Succeeded);
        Assert.Equal(TenantErrorCodes.TenantNotFound, detail.ErrorCode);

        // Hồ sơ có chủ nhưng chưa có hợp đồng vẫn phải thấy: đây là điểm khác cốt lõi so với trước.
        var ownerWithoutContract = await CreateOwnedTenantAsync(OwnerA);
        var visible = await NewService().GetAsync(ownerWithoutContract, OwnerA);
        Assert.True(visible.Succeeded, visible.ErrorMessage);

        var crossOwner = await NewService().GetAsync(ownerWithoutContract, OwnerB);
        Assert.Equal(TenantErrorCodes.TenantNotFound, crossOwner.ErrorCode);
    }

    private async Task<int> CreateOwnedTenantAsync(TenantCaller caller)
    {
        var created = await NewService().CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách chưa hợp đồng",
            PhoneNumber = "0901000099",
            IdNumber = "079201000099",
        }, caller);

        Assert.True(created.Succeeded, created.ErrorMessage);
        return created.Tenant!.Id;
    }

    [SqlServerFact]
    public async Task Ho_so_chua_gan_khong_con_hien_giay_to_van_None()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext();
        var scope = new TenantDocumentScopeService(db, new ContractManagerTenantScope(db));

        Assert.Equal(
            TenantDocumentAccessLevel.None,
            await scope.ResolveAsync(seed.PoolTenantId, OwnerA));
        Assert.Equal(
            TenantDocumentAccessLevel.Owner,
            await scope.ResolveAsync(seed.TenantAId, OwnerA));
        Assert.Equal(
            TenantDocumentAccessLevel.None,
            await scope.ResolveAsync(seed.PoolTenantId, ManagerA));
    }

    [SqlServerFact]
    public async Task Admin_va_vai_tro_la_khong_thay_gi()
    {
        await SeedAsync();
        var service = NewService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ListAsync(new TenantFilterRequest(), new TenantCaller("admin-1", Roles.Admin)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ListAsync(new TenantFilterRequest(), new TenantCaller("la", "SuperAdmin")));
    }

    [SqlServerFact]
    public async Task ContractManagerTenantScope_dung_pham_vi_phan_cong_va_khong_mo_upload_xoa_xuat()
    {
        var seed = await SeedAsync();
        await using var db = fixture.CreateContext();
        var scope = new ContractManagerTenantScope(db);

        Assert.True(await scope.IsTenantInScopeAsync(ManagerA.UserId, seed.TenantAId));
        Assert.False(await scope.IsTenantInScopeAsync(ManagerA.UserId, seed.TenantBId));
        Assert.False(await scope.IsTenantInScopeAsync(ManagerA.UserId, seed.PoolTenantId));
        Assert.False(await scope.IsTenantInScopeAsync("ql-khac", seed.TenantAId));

        var access = TenantDocumentAccessLevel.Manager;
        Assert.True(TenantDocumentPermissionRules.CanView(access));
        Assert.True(TenantDocumentPermissionRules.CanDownload(access));
        Assert.False(TenantDocumentPermissionRules.CanUpload(access));
        Assert.False(TenantDocumentPermissionRules.CanDelete(access));
        Assert.False(TenantDocumentPermissionRules.CanReplace(access));
        Assert.False(TenantDocumentPermissionRules.CanExport(access));
    }

    private async Task<string> RowVersionAsync(int tenantId)
    {
        await using var db = fixture.CreateContext();
        var rowVersion = await db.Tenants.AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.RowVersion)
            .FirstAsync();
        return Convert.ToBase64String(rowVersion);
    }

    private async Task<ScopeSeed> SeedAsync(bool assignManager = true)
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();

        await SqlServerFixture.SeedAccountAsync(db, OwnerA.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OwnerB.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerA.UserId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-b", Roles.KhachThue);

        var propertyA = new Property { Name = "Nhà A", OwnerId = OwnerA.UserId };
        var propertyB = new Property { Name = "Nhà B", OwnerId = OwnerB.UserId };
        db.Properties.AddRange(propertyA, propertyB);
        await db.SaveChangesAsync();

        var roomA = new Room { PropertyId = propertyA.Id, Code = "A101", StandardPrice = 1_000_000m };
        var roomB = new Room { PropertyId = propertyB.Id, Code = "B101", StandardPrice = 1_000_000m };
        db.Rooms.AddRange(roomA, roomB);
        await db.SaveChangesAsync();

        const string tenantACitizenId = "079201000001";
        const string tenantBCitizenId = "079201000002";
        const string tenantBPhone = "0901000002";
        const string poolCitizenId = "079201000003";
        const string poolPhone = "0901000003";

        var tenantA = new Tenant
        {
            FullName = "Khách A",
            Phone = "0901000001",
            CitizenId = tenantACitizenId,
        };
        var tenantB = new Tenant
        {
            FullName = "Khách B",
            Phone = tenantBPhone,
            CitizenId = tenantBCitizenId,
        };
        // "Kho chung": hồ sơ chưa liên kết chủ trọ nào (PLAN-005: chỉ người tạo/mời mới thấy).
        var poolTenant = new Tenant { FullName = "Khách kho", Phone = poolPhone, CitizenId = poolCitizenId };
        db.Tenants.AddRange(tenantA, tenantB, poolTenant);
        await db.SaveChangesAsync();

        db.TenantOwnerLinks.AddRange(
            new TenantOwnerLink { TenantId = tenantA.Id, OwnerId = OwnerA.UserId },
            new TenantOwnerLink { TenantId = tenantB.Id, OwnerId = OwnerB.UserId });
        await db.SaveChangesAsync();

        AddContract(db, tenantA.Id, roomA.Id, ContractStatus.Approved, "HD-A");
        AddContract(db, tenantB.Id, roomB.Id, ContractStatus.Pending, "HD-B");

        PropertyManager? assignment = null;
        if (assignManager)
        {
            assignment = new PropertyManager
            {
                PropertyId = propertyA.Id,
                ManagerUserId = ManagerA.UserId,
                AssignedByUserId = OwnerA.UserId,
            };
            db.PropertyManagers.Add(assignment);
        }

        await db.SaveChangesAsync();

        return new ScopeSeed(
            tenantA.Id,
            tenantB.Id,
            poolTenant.Id,
            roomA.Id,
            roomB.Id,
            assignment?.Id ?? 0,
            tenantACitizenId,
            tenantBCitizenId,
            tenantBPhone,
            poolCitizenId,
            poolPhone);
    }

    private static void AddContract(
        ApplicationDbContext db,
        int tenantId,
        int roomId,
        ContractStatus status,
        string contractNumber)
    {
        var contract = new Contract
        {
            ContractNumber = contractNumber,
            RoomId = roomId,
            StartDate = DateTime.UtcNow.Date.AddDays(-10),
            EndDate = DateTime.UtcNow.Date.AddDays(20),
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
    }

    private sealed record ScopeSeed(
        int TenantAId,
        int TenantBId,
        int PoolTenantId,
        int RoomAId,
        int RoomBId,
        int AssignmentId,
        string TenantACitizenId,
        string TenantBCitizenId,
        string TenantBPhone,
        string PoolCitizenId,
        string PoolPhone);
}
