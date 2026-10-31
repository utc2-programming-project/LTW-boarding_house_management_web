using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Reports;
using Web.Features.Reports.DTOs;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Settings.Entities;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;

namespace NhaTro.IntegrationTests.Features.Reports;

/// <summary>
/// VHT-41 — integration test trên SQL Server thật cho nguồn phòng P8:
/// KPI khớp danh sách cùng filter, phòng tạm ngưng/xóa mềm, nhà trống, phạm vi, công tắc.
/// </summary>
public sealed class RoomReportServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string OwnerAId = "chu-tro-a";
    private const string OwnerBId = "chu-tro-b";
    private const string ManagerId = "quan-ly-1";
    private const string AdminId = "admin-1";
    private const string TenantUserId = "khach-thue-1";

    private static readonly PropertyCaller ChuTroA = new(OwnerAId, Roles.ChuTro);
    private static readonly PropertyCaller QuanLy = new(ManagerId, Roles.QuanLy);
    private static readonly PropertyCaller Admin = new(AdminId, Roles.Admin);
    private static readonly PropertyCaller KhachThue = new(TenantUserId, Roles.KhachThue);

    private sealed record Seed(int PropertyAId, int PropertyBId, int PropertyCId, int RoomA101Id);

    // ---------- Dựng dữ liệu ----------

    /// <summary>Nhà A của chủ A: 3 phòng active + 1 tạm ngưng + 1 xóa mềm. Nhà B: 1 phòng. Nhà C: 0 phòng.</summary>
    private async Task<Seed> PrepareAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        await db.ManagerToggles.ExecuteDeleteAsync();

        await SqlServerFixture.SeedAccountAsync(db, OwnerAId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OwnerBId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, AdminId, Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, TenantUserId, Roles.KhachThue);

        var propertyA = new Property { Name = "Nhà A", OwnerId = OwnerAId, IsActive = true };
        var propertyB = new Property { Name = "Nhà B", OwnerId = OwnerBId, IsActive = true };
        var propertyC = new Property { Name = "Nhà C trống", OwnerId = OwnerAId, IsActive = true };
        db.Properties.AddRange(propertyA, propertyB, propertyC);
        await db.SaveChangesAsync();

        var a101 = NewRoom(propertyA.Id, "A101", RoomStatus.Available);
        db.Rooms.AddRange(
            a101,
            NewRoom(propertyA.Id, "A102", RoomStatus.Rented),
            NewRoom(propertyA.Id, "A103", RoomStatus.Maintenance),
            NewRoom(propertyA.Id, "A104", RoomStatus.Available, isActive: false),
            NewRoom(propertyA.Id, "A105", RoomStatus.Rented, isDeleted: true),
            NewRoom(propertyB.Id, "B201", RoomStatus.Available));
        await db.SaveChangesAsync();

        return new Seed(propertyA.Id, propertyB.Id, propertyC.Id, a101.Id);
    }

    private static Room NewRoom(
        int propertyId, string code, RoomStatus status, bool isActive = true, bool isDeleted = false) =>
        new()
        {
            PropertyId = propertyId,
            Code = code,
            Floor = 1,
            AreaM2 = 20m,
            StandardPrice = 3_000_000m,
            Status = status,
            IsActive = isActive,
            IsDeleted = isDeleted,
        };

    private static IManagerTogglePolicy NewPolicy(ApplicationDbContext db) =>
        new ManagerTogglePolicy(
            new ManagerToggleService(db, TimeProvider.System, NullLogger<ManagerToggleService>.Instance),
            NullLogger<ManagerTogglePolicy>.Instance);

    private RoomReportService NewReportService(ApplicationDbContext db) => new(fixture, NewPolicy(db));

    private RoomService NewRoomService(ApplicationDbContext db) => new(
        fixture,
        TimeProvider.System,
        new RoomChangePolicyStub(),
        new NoActiveContractRoomUsageChecker());

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

    // ---------- Case 1: KPI khớp danh sách và phạm vi Chủ trọ ----------

    [SqlServerFact]
    public async Task Chu_tro_thay_phong_cua_minh_va_kpi_khop_danh_sach()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db).GetAsync(new RoomReportFilter(), ChuTroA);

        Assert.Equal(3, report.Kpis.Total);
        Assert.Equal(1, report.Kpis.Available);
        Assert.Equal(1, report.Kpis.Rented);
        Assert.Equal(1, report.Kpis.Maintenance);
        Assert.Equal(3, report.Rooms.TotalCount);
        Assert.Equal(3, report.Rooms.Items.Count);
        Assert.All(report.Rooms.Items, item => Assert.Equal(seed.PropertyAId, item.PropertyId));
        Assert.DoesNotContain(report.Rooms.Items, item => item.Code is "A104" or "A105" or "B201");
    }

    [SqlServerFact]
    public async Task Kpi_khop_danh_sach_p2_cung_bo_loc()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db).GetAsync(new RoomReportFilter(), ChuTroA);
        var p2 = await NewRoomService(db).ListAsync(new RoomFilter { IsActive = true }, ChuTroA);

        Assert.Equal(p2.TotalCount, report.Kpis.Total);
        Assert.Equal(p2.TotalCount, report.Rooms.TotalCount);
        Assert.Equal(
            p2.Items.Select(item => item.Id).OrderBy(id => id),
            report.Rooms.Items.Select(item => item.Id).OrderBy(id => id));
    }

    // ---------- Case 2: lọc theo trạng thái ----------

    [SqlServerFact]
    public async Task Loc_theo_trang_thai_tra_dung_so_va_danh_sach()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db)
            .GetAsync(new RoomReportFilter { Status = RoomStatus.Rented }, ChuTroA);

        Assert.Equal(1, report.Kpis.Total);
        Assert.Equal(0, report.Kpis.Available);
        Assert.Equal(1, report.Kpis.Rented);
        Assert.Equal(0, report.Kpis.Maintenance);
        Assert.Single(report.Rooms.Items);
        Assert.Equal("A102", report.Rooms.Items[0].Code);
    }

    // ---------- Case 3: nhà trọ trống ----------

    [SqlServerFact]
    public async Task Nha_tro_khong_co_phong_tra_khong_khong_loi()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db)
            .GetAsync(new RoomReportFilter { PropertyId = seed.PropertyCId }, ChuTroA);

        Assert.Equal(0, report.Kpis.Total);
        Assert.Equal(0, report.Kpis.Available);
        Assert.Equal(0, report.Kpis.Rented);
        Assert.Equal(0, report.Kpis.Maintenance);
        Assert.Equal(0, report.Rooms.TotalCount);
        Assert.Empty(report.Rooms.Items);
    }

    // ---------- Case 4: Quản lý xem toàn bộ khi công tắc cho phép ----------

    [SqlServerFact]
    public async Task Quan_ly_thay_toan_bo_phong_khi_cong_tac_cho_phep()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db).GetAsync(new RoomReportFilter(), QuanLy);

        // A101/A102/A103 + B201; A104 tạm ngưng và A105 xóa mềm không tính.
        Assert.Equal(4, report.Kpis.Total);
        Assert.Equal(2, report.Kpis.Available);
        Assert.Equal(1, report.Kpis.Rented);
        Assert.Equal(1, report.Kpis.Maintenance);
        Assert.Equal(4, report.Rooms.TotalCount);
    }

    // ---------- Case 5: phòng tạm ngưng ----------

    [SqlServerFact]
    public async Task Phong_tam_ngung_chi_vao_bao_cao_khi_loc_rieng()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();

        var macDinh = await NewReportService(db).GetAsync(new RoomReportFilter(), ChuTroA);
        Assert.DoesNotContain(macDinh.Rooms.Items, item => item.Code == "A104");

        var tamNgung = await NewReportService(db)
            .GetAsync(new RoomReportFilter { IsActive = false }, ChuTroA);
        Assert.Equal(1, tamNgung.Kpis.Total);
        Assert.Equal(1, tamNgung.Kpis.Available);
        Assert.Single(tamNgung.Rooms.Items);
        Assert.Equal("A104", tamNgung.Rooms.Items[0].Code);
    }

    // ---------- Case 7: không đếm trùng khi phòng có nhiều hợp đồng ----------

    [SqlServerFact]
    public async Task Khong_dem_trung_khi_phong_co_nhieu_hop_dong()
    {
        var seed = await PrepareAsync();
        await using (var db = fixture.CreateContext())
        {
            db.Contracts.AddRange(
                NewContract("HD-A101-1", seed.RoomA101Id, ContractStatus.Approved),
                NewContract("HD-A101-2", seed.RoomA101Id, ContractStatus.Pending));
            await db.SaveChangesAsync();
        }

        await using var readDb = fixture.CreateContext();
        var report = await NewReportService(readDb).GetAsync(new RoomReportFilter(), ChuTroA);

        Assert.Equal(3, report.Kpis.Total);
        Assert.Equal(3, report.Rooms.TotalCount);
    }

    private static Contract NewContract(string number, int roomId, ContractStatus status) =>
        new()
        {
            ContractNumber = number,
            RoomId = roomId,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            SnapshotPrice = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = status,
        };

    // ---------- Case 8-11: công tắc và vai trò ----------

    [SqlServerFact]
    public async Task Quan_ly_bi_chan_khi_tat_muc_phong()
    {
        await PrepareAsync();
        await DenyToggleAsync("SW-15a");
        await using var db = fixture.CreateContext();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewReportService(db).GetAsync(new RoomReportFilter(), QuanLy));
    }

    [SqlServerFact]
    public async Task Quan_ly_bi_chan_khi_tat_xem_bao_cao()
    {
        await PrepareAsync();
        await DenyToggleAsync("SW-15");
        await using var db = fixture.CreateContext();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewReportService(db).GetAsync(new RoomReportFilter(), QuanLy));
    }

    [SqlServerFact]
    public async Task Chu_tro_khong_bi_anh_huong_boi_cong_tac()
    {
        await PrepareAsync();
        await DenyToggleAsync("SW-15a");
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db).GetAsync(new RoomReportFilter(), ChuTroA);

        Assert.Equal(3, report.Kpis.Total);
    }

    [SqlServerFact]
    public async Task Admin_va_khach_thue_bi_tu_choi()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();
        var service = NewReportService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetAsync(new RoomReportFilter(), Admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetAsync(new RoomReportFilter(), KhachThue));
    }
}
