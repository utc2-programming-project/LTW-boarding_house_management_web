using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Settings.Entities;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Entities;

namespace NhaTro.IntegrationTests.Features.Reports;

/// <summary>
/// VHT-46 — integration test trên SQL Server thật cho nguồn khách thuê P8:
/// KPI khớp danh sách P3, không đếm trùng khi một khách có nhiều hợp đồng,
/// phạm vi Chủ trọ/Quản lý, lọc nhà/trạng thái/công tắc và không trả CCCD.
/// </summary>
public sealed class TenantReportServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string OwnerAId = "chu-tro-a";
    private const string OwnerBId = "chu-tro-b";
    private const string ManagerId = "quan-ly-1";
    private const string AdminId = "admin-1";
    private const string TenantUserAId = "khach-thue-a";
    private const string TenantUserBId = "khach-thue-b";
    private const string TenantUserInactiveId = "khach-thue-nghi";

    private static readonly TenantCaller OwnerA = new(OwnerAId, Roles.ChuTro);
    private static readonly TenantCaller OwnerB = new(OwnerBId, Roles.ChuTro);
    private static readonly TenantCaller ManagerA = new(ManagerId, Roles.QuanLy);
    private static readonly TenantCaller Admin = new(AdminId, Roles.Admin);
    private static readonly TenantCaller KhachThue = new(TenantUserAId, Roles.KhachThue);

    private sealed record Seed(
        int PropertyAId,
        int PropertyBId,
        int TenantAId,
        int TenantBId,
        int TenantInactiveId,
        int TenantPoolId,
        int TenantPendingId,
        int TenantDeletedId);

    // ---------- Dựng dữ liệu ----------

    /// <summary>
    /// Nhà A (chủ A): An có 2 hợp đồng đã duyệt + 1 hợp đồng chờ duyệt cho Em.
    /// Nhà B (chủ B): Bình đang thuê, Chi tạm ngưng nhưng còn hợp đồng hiệu lực.
    /// Dũng là kho hồ sơ chưa gắn hợp đồng; Phúc đã xóa mềm.
    /// </summary>
    private async Task<Seed> PrepareAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        // Dùng mặc định công tắc trong danh mục (SW-15* = Cho phép) thay vì dòng seed cũ.
        await db.ManagerToggles.ExecuteDeleteAsync();

        await SqlServerFixture.SeedAccountAsync(db, OwnerAId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OwnerBId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, AdminId, Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, TenantUserAId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, TenantUserBId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, TenantUserInactiveId, Roles.KhachThue);

        var propertyA = new Property { Name = "Nhà A", OwnerId = OwnerAId, IsActive = true };
        var propertyB = new Property { Name = "Nhà B", OwnerId = OwnerBId, IsActive = true };
        db.Properties.AddRange(propertyA, propertyB);
        await db.SaveChangesAsync();

        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = propertyA.Id,
            ManagerUserId = ManagerId,
            AssignedByUserId = OwnerAId,
        });

        var roomA101 = NewRoom(propertyA.Id, "A101");
        var roomA102 = NewRoom(propertyA.Id, "A102");
        var roomB201 = NewRoom(propertyB.Id, "B201");
        var roomB202 = NewRoom(propertyB.Id, "B202");
        db.Rooms.AddRange(roomA101, roomA102, roomB201, roomB202);

        var tenantA = NewTenant("An Nguyễn", "0901000001", TenantUserAId);
        var tenantB = NewTenant("Bình Trần", "0901000002", TenantUserBId);
        var tenantInactive = NewTenant(
            "Chi Lê", "0901000003", TenantUserInactiveId, isActive: false);
        var tenantPool = NewTenant("Dũng Phạm", "0901000004", userId: null);
        var tenantPending = NewTenant("Em Võ", "0901000005", userId: null);
        var tenantDeleted = NewTenant("Phúc Đỗ", "0901000006", userId: null, isDeleted: true);
        db.Tenants.AddRange(
            tenantA, tenantB, tenantInactive, tenantPool, tenantPending, tenantDeleted);
        await db.SaveChangesAsync();

        // An có hai hợp đồng cùng hiệu lực: KPI đang thuê vẫn chỉ tính một khách.
        AddContract(db, "HD-A101", roomA101.Id, tenantA.Id, ContractStatus.Approved);
        AddContract(db, "HD-A102", roomA102.Id, tenantA.Id, ContractStatus.Approved);
        AddContract(db, "HD-B201", roomB201.Id, tenantB.Id, ContractStatus.Approved);
        AddContract(db, "HD-B202", roomB202.Id, tenantInactive.Id, ContractStatus.Approved);
        AddContract(db, "HD-A-CHO", roomA101.Id, tenantPending.Id, ContractStatus.Pending);
        await db.SaveChangesAsync();

        return new Seed(
            propertyA.Id, propertyB.Id, tenantA.Id, tenantB.Id, tenantInactive.Id,
            tenantPool.Id, tenantPending.Id, tenantDeleted.Id);
    }

    private static Room NewRoom(int propertyId, string code) => new()
    {
        PropertyId = propertyId,
        Code = code,
        Floor = 1,
        AreaM2 = 20m,
        StandardPrice = 3_000_000m,
        Status = RoomStatus.Rented,
        IsActive = true,
    };

    private static Tenant NewTenant(
        string fullName, string phone, string? userId, bool isActive = true, bool isDeleted = false) => new()
    {
        FullName = fullName,
        Phone = phone,
        UserId = userId,
        IsActive = isActive,
        IsDeleted = isDeleted,
    };

    private static void AddContract(
        ApplicationDbContext db, string number, int roomId, int tenantId, ContractStatus status)
    {
        var contract = new Contract
        {
            ContractNumber = number,
            RoomId = roomId,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            SnapshotPrice = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = status,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember { ContractId = contract.ContractId, TenantId = tenantId });
    }

    private static IManagerTogglePolicy NewPolicy(ApplicationDbContext db) =>
        new ManagerTogglePolicy(
            new ManagerToggleService(db, TimeProvider.System, NullLogger<ManagerToggleService>.Instance),
            NullLogger<ManagerTogglePolicy>.Instance);

    private TenantReportService NewReportService(ApplicationDbContext db) =>
        new(fixture, TimeProvider.System, NewPolicy(db));

    private TenantService NewTenantService(ApplicationDbContext db) =>
        new(fixture, TimeProvider.System, NewPolicy(db));

    private async Task DenyToggleAsync(string code)
    {
        await using var db = fixture.CreateContext();
        db.ManagerToggles.Add(new ManagerToggle
        {
            Code = code,
            Mode = ManagerToggleMode.Deny,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    // ---------- Case 1: KPI khớp danh sách P3, không đếm trùng hợp đồng ----------

    [SqlServerFact]
    public async Task Chu_tro_A_kpi_khop_danh_sach_P3_va_khong_dem_trung_hop_dong()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db).GetAsync(new TenantReportFilter(), OwnerA);

        // An (2 hợp đồng) + Bình? Không: Bình thuộc nhà B. An + Dũng (kho chung) + Em = 3.
        Assert.Equal(3, report.Kpis.Total);
        Assert.Equal(3, report.Kpis.Active);
        Assert.Equal(1, report.Kpis.Renting);
        Assert.Equal(1, report.Kpis.LinkedAccountCount);
        Assert.Equal(3, report.Tenants.TotalCount);
        Assert.Equal(3, report.Tenants.Items.Count);
        Assert.Equal(
            report.Tenants.Items.Select(item => item.Id).Distinct().OrderBy(id => id),
            report.Tenants.Items.Select(item => item.Id).OrderBy(id => id));

        var p3 = await NewTenantService(db).ListAsync(
            new TenantFilterRequest { PageSize = 100 }, OwnerA);
        Assert.Equal(p3.TotalCount, report.Kpis.Total);
        Assert.Equal(
            p3.Items.Select(item => item.Id).OrderBy(id => id),
            report.Tenants.Items.Select(item => item.Id).OrderBy(id => id));

        // An có 2 hợp đồng cùng hiệu lực: báo cáo chỉ trả một dòng, phòng chọn xác định theo
        // hợp đồng mới nhất; cùng ngày bắt đầu nên chấp nhận một trong hai mã phòng.
        var an = Assert.Single(report.Tenants.Items, item => item.Id == seed.TenantAId);
        Assert.True(an.HasActiveContract);
        Assert.Contains(an.RoomCode, new[] { "A101", "A102" });
        Assert.Contains(report.Tenants.Items, item => item.Id == seed.TenantPendingId && !item.HasActiveContract);
        Assert.DoesNotContain(report.Tenants.Items, item => item.Id == seed.TenantDeletedId);
        Assert.DoesNotContain(report.Tenants.Items, item => item.Id == seed.TenantBId);
    }

    // ---------- Case 2: lọc theo nhà ----------

    [SqlServerFact]
    public async Task Loc_nha_chi_tinh_khach_co_hop_dong_tai_nha_do()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewReportService(db);

        var nhaA = await service.GetAsync(new TenantReportFilter { PropertyId = seed.PropertyAId }, OwnerA);
        Assert.Equal(2, nhaA.Kpis.Total);
        Assert.Equal(2, nhaA.Kpis.Active);
        Assert.Equal(1, nhaA.Kpis.Renting);
        Assert.Equal(1, nhaA.Kpis.LinkedAccountCount);
        Assert.Equal(2, nhaA.Tenants.TotalCount);

        // Nhà B không thuộc phạm vi chủ A: trả rỗng, không lộ khách của chủ B.
        var nhaB = await service.GetAsync(new TenantReportFilter { PropertyId = seed.PropertyBId }, OwnerA);
        Assert.Equal(0, nhaB.Kpis.Total);
        Assert.Empty(nhaB.Tenants.Items);
    }

    [SqlServerFact]
    public async Task Loc_nha_B_tra_dung_khach_va_trang_thai_hoat_dong()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewReportService(db);

        var tatCa = await service.GetAsync(new TenantReportFilter { PropertyId = seed.PropertyBId }, OwnerB);
        // Bình + Chi (tạm ngưng) thuộc nhà B; Dũng ở kho chung không gắn hợp đồng nên không vào nhà B.
        Assert.Equal(2, tatCa.Kpis.Total);
        Assert.Equal(1, tatCa.Kpis.Active);
        Assert.Equal(2, tatCa.Kpis.Renting);

        var tamNgung = await service.GetAsync(
            new TenantReportFilter { PropertyId = seed.PropertyBId, IsActive = false }, OwnerB);
        Assert.Equal(1, tamNgung.Kpis.Total);
        Assert.Equal(0, tamNgung.Kpis.Active);
        Assert.Equal(1, tamNgung.Kpis.Renting);
        Assert.Equal(seed.TenantInactiveId, tamNgung.Tenants.Items.Single().Id);
    }

    [SqlServerFact]
    public async Task Hop_dong_o_nha_ngoai_pham_vi_khong_lo_ten_nha()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewReportService(db);

        // Dũng (kho hồ sơ chung) có hợp đồng chờ duyệt tại nhà A và hợp đồng đang hiệu lực tại nhà B.
        var roomA = await db.Rooms.FirstAsync(room => room.PropertyId == seed.PropertyAId);
        var roomB = await db.Rooms.FirstAsync(room => room.PropertyId == seed.PropertyBId);
        AddContract(db, "HD-A-POOL", roomA.Id, seed.TenantPoolId, ContractStatus.Pending);
        AddContract(db, "HD-B-POOL", roomB.Id, seed.TenantPoolId, ContractStatus.Approved);
        await db.SaveChangesAsync();

        // Chủ A vẫn thấy Dũng (có hợp đồng tại nhà A) nhưng không thấy hợp đồng/nhà B, không tăng Renting.
        var ownerA = await service.GetAsync(new TenantReportFilter(), OwnerA);
        var dungA = Assert.Single(ownerA.Tenants.Items, item => item.Id == seed.TenantPoolId);
        Assert.False(dungA.HasActiveContract);
        Assert.Null(dungA.PropertyName);
        Assert.Null(dungA.RoomCode);
        Assert.Equal(1, ownerA.Kpis.Renting);

        // Chủ B thấy đúng hợp đồng tại nhà B.
        var ownerB = await service.GetAsync(
            new TenantReportFilter { PropertyId = seed.PropertyBId }, OwnerB);
        var dungB = Assert.Single(ownerB.Tenants.Items, item => item.Id == seed.TenantPoolId);
        Assert.True(dungB.HasActiveContract);
        Assert.Equal("Nhà B", dungB.PropertyName);
    }

    // ---------- Case 3: lọc khách đang thuê ----------

    [SqlServerFact]
    public async Task Loc_theo_hop_dong_dang_hieu_luc_va_kpi_khop()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewReportService(db);

        var dangThue = await service.GetAsync(
            new TenantReportFilter { HasActiveContract = true }, OwnerA);
        Assert.Equal(1, dangThue.Kpis.Total);
        Assert.Equal(1, dangThue.Kpis.Renting);
        Assert.Equal(seed.TenantAId, dangThue.Tenants.Items.Single().Id);

        var chuaThue = await service.GetAsync(
            new TenantReportFilter { HasActiveContract = false }, OwnerA);
        Assert.Equal(2, chuaThue.Kpis.Total);
        Assert.Equal(0, chuaThue.Kpis.Renting);
        Assert.DoesNotContain(chuaThue.Tenants.Items, item => item.Id == seed.TenantAId);
    }

    [SqlServerFact]
    public async Task Tu_khoa_va_phan_trang_giu_cung_so_voi_danh_sach()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewReportService(db);

        var loc = await service.GetAsync(
            new TenantReportFilter { Keyword = "An", Page = 1, PageSize = 1 }, OwnerA);
        Assert.Equal(1, loc.Kpis.Total);
        Assert.Equal(1, loc.Tenants.TotalCount);
        Assert.Single(loc.Tenants.Items);
        Assert.Equal("An Nguyễn", loc.Tenants.Items[0].FullName);
    }

    // ---------- Case 4: Quản lý theo phân công, D9 ẩn trạng thái tài khoản ----------

    [SqlServerFact]
    public async Task Quan_ly_chi_thay_pham_vi_duoc_phan_cong_va_an_trang_thai_tai_khoan()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db).GetAsync(new TenantReportFilter(), ManagerA);

        Assert.Equal(3, report.Kpis.Total);
        Assert.Equal(1, report.Kpis.Renting);
        // D9: Quản lý không được xem số hồ sơ đã gắn tài khoản.
        Assert.Null(report.Kpis.LinkedAccountCount);
        Assert.All(report.Tenants.Items, item => Assert.Null(item.HasAccount));
    }

    [SqlServerFact]
    public async Task Quan_ly_loc_theo_tai_khoan_bi_tu_choi()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewReportService(db).GetAsync(
                new TenantReportFilter { HasAccount = true }, ManagerA));
    }

    // ---------- Case 5: công tắc và vai trò ----------

    [SqlServerFact]
    public async Task Chu_tro_khong_bi_anh_huong_boi_cong_tac()
    {
        await PrepareAsync();
        await DenyToggleAsync("SW-15");
        await DenyToggleAsync("SW-15b");
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db).GetAsync(new TenantReportFilter(), OwnerA);

        Assert.Equal(3, report.Kpis.Total);
    }

    // SW-15* theo danh mục chỉ có Cho phép/Không cho phép (CHECK constraint DB),
    // nên chỉ kiểm nhánh Deny; nhánh "lạ" đã fail-closed ở policy.
    [Theory]
    [InlineData("SW-15")]
    [InlineData("SW-15b")]
    public async Task Quan_ly_bi_chan_khi_cong_tac_khong_cho_phep(string code)
    {
        await PrepareAsync();
        await DenyToggleAsync(code);

        await using var db = fixture.CreateContext();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewReportService(db).GetAsync(new TenantReportFilter(), ManagerA));
    }

    [Theory]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.KhachThue)]
    public async Task Admin_va_khach_thue_bi_tu_choi(string role)
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewReportService(db).GetAsync(new TenantReportFilter(), new TenantCaller("user", role)));
    }

    // ---------- Case 6: không trả CCCD ----------

    [Fact]
    public void DTO_bao_cao_khong_co_truong_CCCD()
    {
        var names = typeof(TenantReportItem).GetProperties().Select(property => property.Name).ToArray();

        Assert.DoesNotContain(names, name => name.Contains("Citizen", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("IdNumber", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Cccd", StringComparison.OrdinalIgnoreCase));
    }
}
