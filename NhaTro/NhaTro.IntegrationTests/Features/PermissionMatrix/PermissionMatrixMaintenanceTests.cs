using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Services;
using Web.Features.Maintenance;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// P7 bảo trì — sheet Ma trận quyền, dòng 41..44 (OP40 xem phiếu, OP41 tạo phiếu,
/// OP42 cập nhật trạng thái, OP43 đính kèm ảnh kết quả).
/// Dependency THẬT: IncidentService/IncidentPhotoService nhận ManagerTogglePolicy đọc SW-12/13/14
/// trực tiếp từ DB và hàng đợi duyệt ManagerApprovalService thật — bản cũ dùng
/// <c>AllowAllTogglePolicy</c> và <c>StubApprovals</c> tự chế, tức là mock đúng bộ phân quyền
/// đang bị kiểm thử, nên đã bị bỏ. Dữ liệu nền tạo qua service thật, không hard-code id.
/// Ô ⚙️ (F42 SW-12, F43 SW-13, F44 SW-14 — 3 chế độ cho Quản lý) và ô ⚠️ (G41, G44 — khách thuê
/// chỉ thấy đúng phòng mình đang thuê) do harness data-driven
/// <see cref="PermissionMatrixAutomatedTests"/> chứng minh.
/// Mỗi ô ❌ ngoài việc bị từ chối còn chứng minh DB không đổi (đọc lại trạng thái sau khi gọi).
/// </summary>
public sealed class PermissionMatrixMaintenanceTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static PropertyCaller OwnerA => new("owner-a", Roles.ChuTro);
    private static PropertyCaller OwnerB => new("owner-b", Roles.ChuTro);
    private static PropertyCaller Manager => new("manager-1", Roles.QuanLy);
    private static PropertyCaller TenantA => new("khach-1", Roles.KhachThue);
    private static PropertyCaller Admin => new("admin-1", Roles.Admin);

    /// <summary>Ảnh PNG hợp lệ (chữ ký PNG) — cùng byte mà harness OP43 dùng.</summary>
    private static readonly byte[] PngBytes = Convert.FromHexString("89504E470D0A1A0A00000000");

    // === OP40 Xem phiếu sự cố (dòng 41) ===

    /// <summary>
    /// Ô D41 ❌: Admin không được xem phiếu — danh sách rỗng, đọc trực tiếp báo Không tìm thấy,
    /// và phiếu vẫn còn nguyên trong DB (từ chối chứ không xoá).
    /// </summary>
    [SqlServerFact]
    public async Task Cell_D41_OP40_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var incidents = NewIncidents(db);

        Assert.Empty(await incidents.ListAsync(Admin));
        var read = await incidents.GetAsync(data.IncidentAId, Admin);
        Assert.False(read.Succeeded);
        Assert.Equal(IncidentErrorCodes.NotFound, read.ErrorCode);
        Assert.True(await IncidentExistsAsync(data.IncidentAId));
    }

    /// <summary>Ô E41 ✅: Chủ trọ A xem phiếu nhà A, không thấy phiếu nhà B.</summary>
    [SqlServerFact]
    public async Task Cell_E41_OP40_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var incidents = NewIncidents(db);

        var list = await incidents.ListAsync(OwnerA);
        Assert.Contains(list, item => item.Id == data.IncidentAId);
        Assert.DoesNotContain(list, item => item.Id == data.IncidentBId);

        var detail = await incidents.GetAsync(data.IncidentAId, OwnerA);
        Assert.True(detail.Succeeded, detail.ErrorCode);
        Assert.Equal("A101", detail.Incident!.RoomCode);
        Assert.Equal(IncidentStatus.New, detail.Incident.Status);
    }

    /// <summary>Ô F41 ✅: Quản lý được giao nhà A xem phiếu nhà A, không thấy phiếu nhà B.</summary>
    [SqlServerFact]
    public async Task Cell_F41_OP40_QuanLy_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var incidents = NewIncidents(db);

        var list = await incidents.ListAsync(Manager);
        Assert.Contains(list, item => item.Id == data.IncidentAId);
        Assert.DoesNotContain(list, item => item.Id == data.IncidentBId);
        Assert.True((await incidents.GetAsync(data.IncidentAId, Manager)).Succeeded);
    }

    // === OP41 Tạo phiếu bảo trì (dòng 42) ===

    /// <summary>Ô D42 ❌: Admin tạo phiếu bị từ chối, số phiếu trong DB không đổi.</summary>
    [SqlServerFact]
    public async Task Cell_D42_OP41_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await CountIncidentsAsync();

        var result = await NewIncidents(db).CreateAsync(NewRequest(data.RoomAId, "Admin tạo phiếu"), Admin);

        Assert.False(result.Succeeded);
        Assert.Equal(IncidentErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await CountIncidentsAsync());
    }

    /// <summary>
    /// Ô E42 ✅: Chủ trọ tạo phiếu cho phòng nhà mình, phiếu ở trạng thái Mới và ghi thật vào DB.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E42_OP41_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;

        var result = await NewIncidents(db).CreateAsync(NewRequest(data.RoomAId, "Chủ trọ báo hỏng"), OwnerA);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Null(result.ApprovalRequest);
        Assert.Equal(IncidentStatus.New, await StatusAsync(result.Incident!.Id));
    }

    /// <summary>
    /// Ô G42 ✅: Khách thuê tạo được phiếu cho chính phòng mình đang thuê (hợp đồng đã duyệt,
    /// còn hiệu lực) — người báo là tài khoản khách, không phải Chủ trọ.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_G42_OP41_KhachThue_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;

        var result = await NewIncidents(db).CreateAsync(
            NewRequest(data.RoomAId, "Khách báo điều hòa không lạnh"), TenantA);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal("khach-1", result.Incident!.ReportedByUserId);
        Assert.Equal(IncidentStatus.New, await StatusAsync(result.Incident.Id));
    }

    // === OP42 Cập nhật trạng thái xử lý (dòng 43) ===

    /// <summary>Ô D43 ❌: Admin chuyển trạng thái bị từ chối, phiếu vẫn ở trạng thái cũ.</summary>
    [SqlServerFact]
    public async Task Cell_D43_OP42_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await StatusAsync(data.IncidentAId);

        var result = await NewIncidents(db).UpdateAsync(
            data.IncidentAId, new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, Admin);

        Assert.False(result.Succeeded);
        Assert.Equal(IncidentErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await StatusAsync(data.IncidentAId));
    }

    /// <summary>
    /// Ô G43 ❌: Khách thuê tự chuyển trạng thái bị từ chối, phiếu vẫn ở trạng thái cũ.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_G43_OP42_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await StatusAsync(data.IncidentAId);

        var result = await NewIncidents(db).UpdateAsync(
            data.IncidentAId, new IncidentUpdateRequest { Status = IncidentStatus.Completed }, TenantA);

        Assert.False(result.Succeeded);
        Assert.Equal(IncidentErrorCodes.Forbidden, result.ErrorCode);
        Assert.Equal(before, await StatusAsync(data.IncidentAId));
    }

    /// <summary>
    /// Ô E43 ✅: Chủ trọ chuyển Mới → Đang xử lý, trạng thái mới ghi thật vào DB.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E43_OP42_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;

        var result = await NewIncidents(db).UpdateAsync(
            data.IncidentAId,
            new IncidentUpdateRequest
            {
                Status = IncidentStatus.InProgress,
                AssigneeName = "Thợ điện lạnh",
                TechnicalNotes = "Đã ghi nhận, đang chờ linh kiện.",
            },
            OwnerA);

        Assert.True(result.Succeeded, result.ErrorCode);
        Assert.Equal(IncidentStatus.InProgress, await StatusAsync(data.IncidentAId));
    }

    // === OP43 Đính kèm ảnh kết quả (dòng 44) ===

    /// <summary>
    /// Ô D44 ❌: Admin upload ảnh kết quả bị từ chối ngay ở vai trò, không có dòng ảnh nào được gắn
    /// thêm vào phiếu (đếm lại attachment trong DB).
    /// </summary>
    [SqlServerFact]
    public async Task Cell_D44_OP43_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await AttachedPhotoCountAsync(data.IncidentAId);

        var upload = await NewPhotos(db).UploadAsync(ResultPhotoRequest(data.IncidentAId), StorageCallerFor(Admin));

        Assert.False(upload.Succeeded);
        Assert.Equal(StorageErrorCodes.Forbidden, upload.ErrorCode);
        Assert.Equal(before, await AttachedPhotoCountAsync(data.IncidentAId));
    }

    /// <summary>
    /// Ô E44 ✅: Chủ trọ đính kèm ảnh kết quả thẳng vào phiếu (không qua hàng đợi duyệt — D4) và
    /// attachment có thật trong DB, mang IncidentId của phiếu.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E44_OP43_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await AttachedPhotoCountAsync(data.IncidentAId);

        var upload = await NewPhotos(db).UploadAsync(
            ResultPhotoRequest(data.IncidentAId), StorageCallerFor(OwnerA));

        Assert.True(upload.Succeeded, upload.ErrorCode);
        Assert.Null(upload.ApprovalRequestId);
        Assert.NotNull(upload.File);
        Assert.Equal(before + 1, await AttachedPhotoCountAsync(data.IncidentAId));
    }

    // === helpers ===

    private IncidentService NewIncidents(ApplicationDbContext db) =>
        new(fixture, TimeProvider.System,
            PermissionMatrixWorld.NewRealTogglePolicy(fixture),
            PermissionMatrixWorld.NewRealApprovals(db));

    private IncidentPhotoService NewPhotos(ApplicationDbContext db) =>
        PermissionMatrixWorld.NewRealIncidentPhotos(fixture, db);

    private static StorageCaller StorageCallerFor(PropertyCaller caller) => new(caller.UserId, caller.Role);

    private static IncidentCreateRequest NewRequest(int roomId, string title) => new()
    {
        RoomId = roomId,
        Title = title,
        Description = "Cần kiểm tra thiết bị và chụp ảnh kết quả xử lý.",
        Category = IncidentCategory.Equipment,
        Urgency = IncidentUrgency.Normal,
    };

    private static UploadFileRequest ResultPhotoRequest(int incidentId) => new()
    {
        File = new FormFile(new MemoryStream(PngBytes), 0, PngBytes.Length, "file", "anh-xu-ly.png"),
        FileCategory = FileCategory.IncidentPhoto,
        RelatedEntityType = RelatedEntityType.MaintenanceIncident,
        RelatedEntityId = incidentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        IncidentPhotoKind = IncidentPhotoKind.Result,
        ExpectedPhotoCount = 1,
    };

    private async Task<IncidentStatus> StatusAsync(int incidentId)
    {
        await using var read = fixture.CreateContext();
        return await read.MaintenanceIncidents.AsNoTracking()
            .Where(incident => incident.Id == incidentId)
            .Select(incident => incident.Status)
            .FirstAsync();
    }

    private async Task<bool> IncidentExistsAsync(int incidentId)
    {
        await using var read = fixture.CreateContext();
        return await read.MaintenanceIncidents.AsNoTracking().AnyAsync(incident => incident.Id == incidentId);
    }

    private async Task<int> CountIncidentsAsync()
    {
        await using var read = fixture.CreateContext();
        return await read.MaintenanceIncidents.CountAsync();
    }

    /// <summary>Chỉ đếm ảnh đã gắn chính thức vào phiếu (ảnh chờ duyệt có ApprovalRequestId != null).</summary>
    private async Task<int> AttachedPhotoCountAsync(int incidentId)
    {
        await using var read = fixture.CreateContext();
        return await read.IncidentPhotoAttachments.AsNoTracking()
            .CountAsync(photo => photo.IncidentId == incidentId && photo.ApprovalRequestId == null);
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

        // Khách thuê có tài khoản + hợp đồng đã duyệt ở phòng A101: cần cho ô G42 (tạo phiếu)
        // và để phạm vi đọc/ghi của khách là thật, không phải dữ liệu giả.
        var tenantA = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Nguyễn Văn Khách",
            PhoneNumber = "0901111111",
            IdNumber = "079201000001",
        }, new TenantCaller("owner-a", Roles.ChuTro));
        Assert.True(tenantA.Succeeded, tenantA.ErrorMessage);
        await tenants.LinkAccountAsync(tenantA.Tenant!.Id, "khach-1", tenantA.Tenant.RowVersion,
            new TenantCaller("owner-a", Roles.ChuTro));

        var today = DateTime.UtcNow.Date;
        var contractA = await contracts.CreateContractAsync(
            new CreateContractRequest(roomA.Room!.Id, today, today.AddMonths(6), [tenantA.Tenant.Id]), OwnerA);
        await contracts.ApproveContractAsync(
            contractA.ContractId, new ApproveContractRequestDto { IsApproved = true }, OwnerA);
        Assert.Equal(ContractStatus.Approved, await ContractStatusAsync(contractA.ContractId));

        var incidents = new IncidentService(fixture, TimeProvider.System, togglePolicy, approvals);
        var incidentA = await incidents.CreateAsync(NewRequest(roomA.Room.Id, "Hỏng điều hòa phòng A101"), OwnerA);
        var incidentB = await incidents.CreateAsync(NewRequest(roomB.Room!.Id, "Hỏng đèn hành lang nhà B"), OwnerB);
        Assert.True(incidentA.Succeeded, incidentA.ErrorCode);
        Assert.True(incidentB.Succeeded, incidentB.ErrorCode);
        Assert.NotNull(incidentA.Incident);
        Assert.NotNull(incidentB.Incident);

        return (db, new SeedData(
            roomA.Room.Id, roomB.Room!.Id, incidentA.Incident.Id, incidentB.Incident.Id));
    }

    private async Task<ContractStatus> ContractStatusAsync(Guid contractId)
    {
        await using var read = fixture.CreateContext();
        return await read.Contracts.AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Select(contract => contract.Status)
            .FirstAsync();
    }

    private sealed record SeedData(int RoomAId, int RoomBId, int IncidentAId, int IncidentBId);

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô G41 ⚠️: OP40 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G41_OP40_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G41");

    /// <summary>Ô F42 ⚙️: OP41 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F42_OP41_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F42");

    /// <summary>Ô F43 ⚙️: OP42 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F43_OP42_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F43");

    /// <summary>Ô F44 ⚙️: OP43 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F44_OP43_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F44");

    /// <summary>Ô G44 ⚠️: OP43 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G44_OP43_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G44");
}
