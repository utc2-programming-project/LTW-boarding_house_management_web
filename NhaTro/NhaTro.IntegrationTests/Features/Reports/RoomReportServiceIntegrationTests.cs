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
using Web.Infrastructure.Time;

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

    private sealed record Seed(
        int PropertyAId,
        int PropertyBId,
        int PropertyCId,
        int RoomA101Id,
        int RoomA102Id,
        int RoomB201Id);

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

        // VHT-141: phòng phải được tạo trước tháng báo cáo để tính vào lấp đầy lịch sử.
        var a101 = NewRoom(propertyA.Id, "A101", RoomStatus.Available);
        var a102 = NewRoom(propertyA.Id, "A102", RoomStatus.Rented);
        var a103 = NewRoom(propertyA.Id, "A103", RoomStatus.Maintenance);
        var a104 = NewRoom(propertyA.Id, "A104", RoomStatus.Available, isActive: false);
        var a105 = NewRoom(propertyA.Id, "A105", RoomStatus.Rented, isDeleted: true);
        var b201 = NewRoom(propertyB.Id, "B201", RoomStatus.Available);
        foreach (var room in new[] { a101, a102, a103, a104, a105, b201 })
        {
            room.CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        db.Rooms.AddRange(a101, a102, a103, a104, a105, b201);
        await db.SaveChangesAsync();

        return new Seed(
            propertyA.Id, propertyB.Id, propertyC.Id, a101.Id, a102.Id, b201.Id);
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

    private RoomReportService NewReportService(ApplicationDbContext db, TimeProvider? time = null) =>
        new(fixture, NewPolicy(db), time);

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

    // ---------- Case 4: Quản lý chỉ xem phòng thuộc nhà được phân công ----------

    [SqlServerFact]
    public async Task Quan_ly_chi_thay_phong_thuoc_nha_duoc_phan_cong()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = seed.PropertyAId,
            ManagerUserId = ManagerId,
            AssignedByUserId = OwnerAId,
        });
        await db.SaveChangesAsync();

        var report = await NewReportService(db).GetAsync(new RoomReportFilter(), QuanLy);

        // Chỉ A101/A102/A103; loại phòng nhà B, phòng tạm ngưng và phòng xóa mềm.
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
    public async Task Quan_ly_chua_duoc_phan_cong_khong_thay_phong()
    {
        await PrepareAsync();
        await using var db = fixture.CreateContext();

        var report = await NewReportService(db).GetAsync(new RoomReportFilter(), QuanLy);

        Assert.Equal(0, report.Kpis.Total);
        Assert.Equal(0, report.Rooms.TotalCount);
        Assert.Empty(report.Rooms.Items);
    }

    [SqlServerFact]
    public async Task Quan_ly_bi_thu_hoi_phan_cong_khong_thay_phong()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = seed.PropertyAId,
            ManagerUserId = ManagerId,
            AssignedByUserId = OwnerAId,
            RevokedAt = DateTime.UtcNow,
            RevokedByUserId = OwnerAId,
        });
        await db.SaveChangesAsync();

        var report = await NewReportService(db).GetAsync(new RoomReportFilter(), QuanLy);

        Assert.Equal(0, report.Kpis.Total);
        Assert.Equal(0, report.Rooms.TotalCount);
        Assert.Empty(report.Rooms.Items);
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

    private static Contract NewContract(
        string number,
        int roomId,
        ContractStatus status,
        DateTime? start = null,
        DateTime? end = null) =>
        new()
        {
            ContractNumber = number,
            RoomId = roomId,
            StartDate = start ?? new DateTime(2026, 1, 1),
            EndDate = end ?? new DateTime(2026, 12, 31),
            SnapshotPrice = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = status,
        };

    // ---------- VHT-141: lấp đầy lịch sử theo tháng ----------

    [SqlServerFact]
    public async Task Lap_day_mac_dinh_tra_sau_thang_gan_nhat_va_khop_hop_dong()
    {
        var seed = await PrepareAsync();
        await using (var db = fixture.CreateContext())
        {
            db.Contracts.AddRange(
                NewContract("HD-A101-06-08", seed.RoomA101Id, ContractStatus.Approved,
                    new DateTime(2026, 6, 1), new DateTime(2026, 8, 31)),
                NewContract("HD-A102-10-12", seed.RoomA102Id, ContractStatus.Liquidated,
                    new DateTime(2026, 10, 1), new DateTime(2026, 12, 31)));
            await db.SaveChangesAsync();
        }

        await using var readDb = fixture.CreateContext();
        var result = await NewReportService(readDb, new FixedTimeProvider(new DateOnly(2026, 11, 13)))
            .GetOccupancyAsync(new OccupancyFilter(), ChuTroA);

        // Không chọn ngày: 6 tháng gần nhất tính đến tháng hiện tại (06/2026 - 11/2026).
        Assert.Equal(
            ["2026-06", "2026-07", "2026-08", "2026-09", "2026-10", "2026-11"],
            result.Months.Select(month => month.MonthKey));
        Assert.All(result.Months, month => Assert.Equal(3, month.TotalRooms));
        Assert.Equal(
            [1, 1, 1, 0, 1, 1],
            result.Months.Select(month => month.RentedRooms));
        Assert.Equal(
            [2, 2, 2, 3, 2, 2],
            result.Months.Select(month => month.VacantRooms));
        Assert.Equal(33.3m, result.Months[0].OccupancyRate!.Value);
        Assert.Equal(0m, result.Months[3].OccupancyRate!.Value);
        Assert.Equal(
            result.Months.Sum(month => month.RentedRooms),
            result.Months.Sum(month => month.TotalRooms - month.VacantRooms));
    }

    [SqlServerFact]
    public async Task Lap_day_loc_khoang_thang_va_nha_khong_co_phong()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();

        var byRange = await NewReportService(db, new FixedTimeProvider(new DateOnly(2026, 11, 13)))
            .GetOccupancyAsync(
                new OccupancyFilter(new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 20)),
                ChuTroA);

        Assert.Equal(["2026-10", "2026-11"], byRange.Months.Select(month => month.MonthKey));
        Assert.All(byRange.Months, month => Assert.Equal(3, month.TotalRooms));

        // Nhà C không có phòng: tỷ lệ null, không chia cho 0.
        var emptyProperty = await NewReportService(db, new FixedTimeProvider(new DateOnly(2026, 11, 13)))
            .GetOccupancyAsync(
                new OccupancyFilter(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), seed.PropertyCId),
                ChuTroA);

        var emptyMonth = Assert.Single(emptyProperty.Months);
        Assert.Equal(0, emptyMonth.TotalRooms);
        Assert.Equal(0, emptyMonth.RentedRooms);
        Assert.Null(emptyMonth.OccupancyRate);
    }

    [SqlServerFact]
    public async Task Lap_day_theo_pham_vi_quyen_va_cong_tac()
    {
        var seed = await PrepareAsync();
        await using var db = fixture.CreateContext();

        var ownerB = new PropertyCaller(OwnerBId, Roles.ChuTro);
        var propertyB = await NewReportService(db, new FixedTimeProvider(new DateOnly(2026, 11, 13)))
            .GetOccupancyAsync(
                new OccupancyFilter(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), seed.PropertyBId),
                ownerB);
        var month = Assert.Single(propertyB.Months);
        Assert.Equal(1, month.TotalRooms);
        Assert.Equal(0, month.RentedRooms);
        Assert.Equal(0m, month.OccupancyRate!.Value);

        var service = NewReportService(db);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetOccupancyAsync(new OccupancyFilter(), Admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetOccupancyAsync(new OccupancyFilter(), KhachThue));

        await DenyToggleAsync("SW-15a");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetOccupancyAsync(new OccupancyFilter(), QuanLy));
    }

    private sealed class FixedTimeProvider(DateOnly today) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(
            VietnamTime.ToUtc(today.ToDateTime(new TimeOnly(12, 0))), TimeSpan.Zero);
    }

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
