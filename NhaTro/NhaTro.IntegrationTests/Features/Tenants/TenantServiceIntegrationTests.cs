using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Rooms;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Enums;

namespace NhaTro.IntegrationTests.Features.Tenants;

/// <summary>
/// VHT-23 (đợt 1C) — integration test trên SQL Server thật, chứng minh 3 tiêu chí nghiệm thu:
/// lưu rồi tải lại đúng; không liên kết nhầm tài khoản; khách không sửa trường ngoài quyền.
/// VHT-94 bổ sung: thao tác ⚙️ của Quản lý trên hồ sơ khách thuê phải đi qua công tắc.
/// </summary>
public sealed class TenantServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly TenantCaller Manager = new("manager-1", Roles.ChuTro);

    // Mặc định cho phép hết để các test cũ giữ nguyên hành vi; test công tắc truyền policy riêng.
    private static TenantService NewTenantService(
        ApplicationDbContext db, IManagerTogglePolicy? policy = null) =>
        new(db, TimeProvider.System, policy ?? new AllowAllTogglePolicy());

    // Policy thật + reader giả: kiểm đúng D4 (Chủ trọ luôn Allowed) và ánh xạ mã công tắc.
    private static ManagerTogglePolicy NewTogglePolicy(
        params (string Code, ManagerToggleMode Mode)[] modes) =>
        new(new FakeToggleReader(modes), NullLogger<ManagerTogglePolicy>.Instance);

    // ---------- Tiêu chí 1: lưu rồi tải lại đúng ----------

    [SqlServerFact]
    public async Task Luu_roi_tai_lai_dung_moi_truong()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewTenantService(db);

        var created = await service.CreateAsync(new CreateTenantRequest
        {
            FullName = "  Nguyễn   Văn An ",
            PhoneNumber = "+84 901 234 567",
            Email = " an@example.com ",
            IdNumber = "079 201 004821",
            DateOfBirth = new DateOnly(1995, 3, 4),
            Gender = Gender.Male,
            PermanentAddress = " 12 Lê Lợi ",
            IdIssuedDate = new DateOnly(2021, 1, 2),
            IdIssuedPlace = " Cục Cảnh sát ",
            Note = " ghi chú nội bộ ",
        }, Manager);

        Assert.True(created.Succeeded, created.ErrorMessage);
        var tenant = created.Tenant!;
        Assert.True(tenant.Id > 0);
        Assert.Equal("Nguyễn Văn An", tenant.FullName);
        Assert.Equal("0901234567", tenant.PhoneNumber);
        Assert.Equal("an@example.com", tenant.Email);
        Assert.Equal("079201004821", tenant.IdNumber);
        Assert.Equal(new DateOnly(1995, 3, 4), tenant.DateOfBirth);
        Assert.Equal(Gender.Male, tenant.Gender);
        Assert.Equal("12 Lê Lợi", tenant.PermanentAddress);
        Assert.Equal("Cục Cảnh sát", tenant.IdIssuedPlace);
        Assert.Equal("ghi chú nội bộ", tenant.Note);
        Assert.True(tenant.IsActive);
        Assert.False(tenant.HasAccount);
        Assert.NotEmpty(tenant.RowVersion);

        // Đọc lại bằng context mới (không dùng cache của context vừa ghi).
        await using var reloadDb = fixture.CreateContext();
        var reloaded = await NewTenantService(reloadDb).GetAsync(tenant.Id, Manager);

        Assert.True(reloaded.Succeeded, reloaded.ErrorMessage);
        Assert.Equal(tenant.Id, reloaded.Tenant!.Id);
        Assert.Equal("Nguyễn Văn An", reloaded.Tenant.FullName);
        Assert.Equal("079201004821", reloaded.Tenant.IdNumber);
        Assert.Equal(new DateOnly(1995, 3, 4), reloaded.Tenant.DateOfBirth);
        Assert.Equal(DateTimeKind.Utc, reloaded.Tenant.CreatedAt.UtcDateTime.Kind);
    }

    [SqlServerFact]
    public async Task RowVersion_cu_tra_ConcurrencyConflict()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewTenantService(db);

        var created = await service.CreateAsync(NewTenant("An", "0901234567"), Manager);
        var id = created.Tenant!.Id;
        var staleVersion = created.Tenant.RowVersion;

        var first = await service.UpdateAsync(id, new UpdateTenantRequest
        {
            FullName = "An lần 1",
            PhoneNumber = "0901234567",
            RowVersion = staleVersion,
        }, Manager);
        Assert.True(first.Succeeded, first.ErrorMessage);

        var second = await service.UpdateAsync(id, new UpdateTenantRequest
        {
            FullName = "An lần 2",
            PhoneNumber = "0901234567",
            RowVersion = staleVersion,
        }, Manager);

        Assert.False(second.Succeeded);
        Assert.Equal(TenantErrorCodes.ConcurrencyConflict, second.ErrorCode);
    }

    [SqlServerFact]
    public async Task Trung_giay_to_tra_DuplicateTenant_kem_ho_so_trung()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewTenantService(db);

        var first = await service.CreateAsync(
            NewTenant("An", "0901111111", idNumber: "079201004821"), Manager);
        Assert.True(first.Succeeded, first.ErrorMessage);

        var second = await service.CreateAsync(
            NewTenant("Bình", "0902222222", idNumber: "079 201 004821"), Manager);

        Assert.False(second.Succeeded);
        Assert.Equal(TenantErrorCodes.DuplicateTenant, second.ErrorCode);
        Assert.NotNull(second.PossibleDuplicates);
        Assert.Contains(second.PossibleDuplicates!, d => d.Id == first.Tenant!.Id);
    }

    // ---------- Tiêu chí 2: không liên kết nhầm tài khoản ----------

    [SqlServerFact]
    public async Task Lien_ket_tai_khoan_khach_thue_roi_tu_doc_dung_ho_so()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountAsync(db, "khach-1", Roles.KhachThue);

        var created = await NewTenantService(db)
            .CreateAsync(NewTenant("An", "0901234567"), Manager);
        var id = created.Tenant!.Id;

        var linked = await NewTenantService(db)
            .LinkAccountAsync(id, "khach-1", created.Tenant.RowVersion, Manager);

        Assert.True(linked.Succeeded, linked.ErrorMessage);
        Assert.True(linked.Tenant!.HasAccount);

        await using var ownDb = fixture.CreateContext();
        var own = await NewTenantService(ownDb)
            .GetOwnAsync(new TenantCaller("khach-1", Roles.KhachThue));

        Assert.True(own.Succeeded, own.ErrorMessage);
        Assert.Equal(id, own.Tenant!.Id);
        Assert.Null(own.Tenant.Note); // khách không thấy ghi chú nội bộ
    }

    [SqlServerFact]
    public async Task Khong_lien_ket_nham_tai_khoan()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        await SeedAccountAsync(db, "chu-tro-1", Roles.ChuTro);

        var service = NewTenantService(db);
        var a = await service.CreateAsync(NewTenant("An", "0901111111"), Manager);
        var b = await service.CreateAsync(NewTenant("Bình", "0902222222"), Manager);
        var idA = a.Tenant!.Id;
        var idB = b.Tenant!.Id;

        // Tài khoản không tồn tại.
        var missing = await service.LinkAccountAsync(idA, "khong-ton-tai", a.Tenant.RowVersion, Manager);
        Assert.Equal(TenantErrorCodes.InvalidRequest, missing.ErrorCode);

        // Tài khoản tồn tại nhưng không phải khách thuê.
        var wrongRole = await service.LinkAccountAsync(idA, "chu-tro-1", a.Tenant.RowVersion, Manager);
        Assert.Equal(TenantErrorCodes.InvalidRequest, wrongRole.ErrorCode);

        // Gắn đúng tài khoản khách thuê.
        var ok = await service.LinkAccountAsync(idA, "khach-1", a.Tenant.RowVersion, Manager);
        Assert.True(ok.Succeeded, ok.ErrorMessage);

        // Cùng tài khoản gắn cho hồ sơ thứ hai.
        var duplicated = await service.LinkAccountAsync(idB, "khach-1", b.Tenant.RowVersion, Manager);
        Assert.Equal(TenantErrorCodes.AccountAlreadyLinked, duplicated.ErrorCode);

        // Hồ sơ đã có tài khoản: không cho ghi đè.
        var reLink = await service.LinkAccountAsync(idA, "khach-1", ok.Tenant!.RowVersion, Manager);
        Assert.Equal(TenantErrorCodes.AccountAlreadyLinked, reLink.ErrorCode);

        // Gỡ rồi gắn sang hồ sơ B được.
        var unlinked = await service.UnlinkAccountAsync(idA, ok.Tenant.RowVersion, Manager);
        Assert.True(unlinked.Succeeded, unlinked.ErrorMessage);
        Assert.False(unlinked.Tenant!.HasAccount);

        var moved = await service.LinkAccountAsync(idB, "khach-1", b.Tenant.RowVersion, Manager);
        Assert.True(moved.Succeeded, moved.ErrorMessage);
    }

    // ---------- Tiêu chí 3: khách không sửa trường ngoài quyền ----------

    [SqlServerFact]
    public async Task Khach_chi_sua_duoc_3_truong_cua_minh()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountAsync(db, "khach-1", Roles.KhachThue);

        var service = NewTenantService(db);
        var created = await service.CreateAsync(
            NewTenant("An", "0901234567", idNumber: "079201004821", note: "nội bộ"), Manager);
        var id = created.Tenant!.Id;

        var linked = await service.LinkAccountAsync(id, "khach-1", created.Tenant.RowVersion, Manager);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        await using var ownDb = fixture.CreateContext();
        var ownService = NewTenantService(ownDb);
        var tenantCaller = new TenantCaller("khach-1", Roles.KhachThue);

        var updated = await ownService.UpdateOwnAsync(new UpdateOwnProfileRequest
        {
            PhoneNumber = "090 999 8888",
            Email = "moi@example.com",
            PermanentAddress = "99 Trần Phú",
            RowVersion = linked.Tenant!.RowVersion,
        }, tenantCaller);
        Assert.True(updated.Succeeded, updated.ErrorMessage);

        await using var checkDb = fixture.CreateContext();
        var reloaded = await NewTenantService(checkDb).GetAsync(id, Manager);
        var detail = reloaded.Tenant!;

        // Ba trường khách được sửa.
        Assert.Equal("0909998888", detail.PhoneNumber);
        Assert.Equal("moi@example.com", detail.Email);
        Assert.Equal("99 Trần Phú", detail.PermanentAddress);

        // Các trường ngoài quyền giữ nguyên.
        Assert.Equal("An", detail.FullName);
        Assert.Equal("079201004821", detail.IdNumber);
        Assert.Equal("nội bộ", detail.Note);
        Assert.True(detail.IsActive);
        Assert.True(detail.HasAccount);

        // Tài khoản khác không sửa được hồ sơ của người khác.
        var stranger = await ownService.UpdateOwnAsync(new UpdateOwnProfileRequest
        {
            PhoneNumber = "0900000000",
            RowVersion = "AQID",
        }, new TenantCaller("khach-2", Roles.KhachThue));
        Assert.Equal(TenantErrorCodes.TenantNotFound, stranger.ErrorCode);
    }

    // ---------- Kiểm quyền (mục 4): Admin và Khách thuê không gọi được nghiệp vụ quản lý ----------

    [SqlServerFact]
    public async Task Admin_bi_chan_khoi_moi_nghiep_vu_khach_thue()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewTenantService(db);
        var admin = new TenantCaller("admin-1", Roles.Admin);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ListAsync(new TenantFilterRequest(), admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(NewTenant("An", "0901234567"), admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetAsync(1, admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetOwnAsync(admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SearchForPickerAsync(new TenantPickerRequest(), admin));
    }

    [SqlServerFact]
    public async Task Khach_thue_khong_goi_duoc_nghiep_vu_quan_ly()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewTenantService(db);
        var khach = new TenantCaller("khach-1", Roles.KhachThue);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ListAsync(new TenantFilterRequest(), khach));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CreateAsync(NewTenant("An", "0901234567"), khach));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.UpdateAsync(1, new UpdateTenantRequest
            {
                FullName = "An",
                PhoneNumber = "0901234567",
                RowVersion = "AQID",
            }, khach));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SetActiveAsync(1, false, "AQID", khach));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SearchForPickerAsync(new TenantPickerRequest(), khach));
    }

    [SqlServerFact]
    public async Task Khach_thue_chi_xem_duoc_ho_so_cua_minh()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        var service = NewTenantService(db);

        var created = await service.CreateAsync(
            NewTenant("An", "0901234567", note: "nội bộ"), Manager);
        var id = created.Tenant!.Id;
        var linked = await service.LinkAccountAsync(id, "khach-1", created.Tenant.RowVersion, Manager);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        var khach = new TenantCaller("khach-1", Roles.KhachThue);

        // Hồ sơ của mình: xem được nhưng không thấy ghi chú nội bộ.
        var own = await service.GetAsync(id, khach);
        Assert.True(own.Succeeded, own.ErrorMessage);
        Assert.Equal(id, own.Tenant!.Id);
        Assert.Null(own.Tenant.Note);

        // Hồ sơ khác: coi như không tồn tại, không lộ là có tồn tại.
        var other = await service.GetAsync(id + 1, khach);
        Assert.Equal(TenantErrorCodes.TenantNotFound, other.ErrorCode);
    }

    // ---------- Kiểm độ dài sau chuẩn hóa (khớp cột DB, tránh truncation 500) ----------

    [SqlServerFact]
    public async Task Qua_dai_sau_chuan_hoa_tra_InvalidRequest()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewTenantService(db);

        var tooLongName = await service.CreateAsync(
            NewTenant(new string('A', 201), "0901234567"), Manager);
        Assert.False(tooLongName.Succeeded);
        Assert.Equal(TenantErrorCodes.InvalidRequest, tooLongName.ErrorCode);

        // Chuỗi thô dài nhưng sau Normalize (gộp khoảng trắng) vẫn hợp lệ -> phải lưu được.
        var manySpaces = await service.CreateAsync(
            NewTenant("An" + new string(' ', 400), "0901234567"), Manager);
        Assert.True(manySpaces.Succeeded, manySpaces.ErrorMessage);
        Assert.Equal("An", manySpaces.Tenant!.FullName);
    }

    // ---------- D9: Quản lý chỉ thấy số CCCD bị che ----------

    [SqlServerFact]
    public async Task Quan_ly_bi_che_so_cccd_va_khong_ghi_de_bang_chuoi_che()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewTenantService(db);
        var quanLy = new TenantCaller("ql-1", Roles.QuanLy);

        var created = await service.CreateAsync(
            NewTenant("An", "0901234567", idNumber: "079201004821"), Manager);
        var id = created.Tenant!.Id;
        Assert.Equal("079201004821", created.Tenant.IdNumber); // Chủ trọ thấy đầy đủ

        // Quản lý nhận chuỗi che ở chi tiết.
        var asQuanLy = await service.GetAsync(id, quanLy);
        Assert.True(asQuanLy.Succeeded, asQuanLy.ErrorMessage);
        Assert.Equal("********4821", asQuanLy.Tenant!.IdNumber);

        // Quản lý sửa SĐT; form gửi lại chuỗi che => CCCD giữ nguyên, không bị ghi đè bằng mask.
        var updated = await service.UpdateAsync(id, new UpdateTenantRequest
        {
            FullName = "An",
            PhoneNumber = "0909999999",
            IdNumber = "********4821",
            RowVersion = asQuanLy.Tenant.RowVersion,
        }, quanLy);
        Assert.True(updated.Succeeded, updated.ErrorMessage);
        Assert.Equal("********4821", updated.Tenant!.IdNumber);

        await using var checkDb = fixture.CreateContext();
        var reloaded = await NewTenantService(checkDb).GetAsync(id, Manager);
        Assert.Equal("0909999999", reloaded.Tenant!.PhoneNumber);
        Assert.Equal("079201004821", reloaded.Tenant.IdNumber);

        // Quản lý gõ lại số mới đầy đủ: đổi được (ma trận: Quản lý sửa giấy tờ, SĐT).
        var changed = await service.UpdateAsync(id, new UpdateTenantRequest
        {
            FullName = "An",
            PhoneNumber = "0909999999",
            IdNumber = "079 201 004 999",
            RowVersion = reloaded.Tenant.RowVersion,
        }, quanLy);
        Assert.True(changed.Succeeded, changed.ErrorMessage);
        Assert.Equal("********4999", changed.Tenant!.IdNumber);

        var final = await service.GetAsync(id, Manager);
        Assert.Equal("079201004999", final.Tenant!.IdNumber);
    }

    // ---------- VHT-94: thao tác ⚙️ của Quản lý phải đi qua công tắc ----------

    [SqlServerFact]
    public async Task Quan_ly_tao_ho_so_phai_qua_cong_tac_SW04()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var quanLy = new TenantCaller("ql-1", Roles.QuanLy);

        // Không thể -> 403, không ghi.
        var denied = NewTenantService(db, NewTogglePolicy(("SW-04", ManagerToggleMode.Deny)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => denied.CreateAsync(NewTenant("An", "0901234567"), quanLy));
        Assert.False(await db.Tenants.AnyAsync());

        // Duyệt -> trả ApprovalRequired, không ghi; hàng đợi duyệt thuộc VHT-96.
        var pending = NewTenantService(db, NewTogglePolicy(("SW-04", ManagerToggleMode.Approve)));
        var blocked = await pending.CreateAsync(NewTenant("An", "0901234567"), quanLy);
        Assert.False(blocked.Succeeded);
        Assert.Equal(TenantErrorCodes.ApprovalRequired, blocked.ErrorCode);
        Assert.False(await db.Tenants.AnyAsync());

        // Cho phép -> Quản lý tạo được.
        var allowed = NewTenantService(db, NewTogglePolicy(("SW-04", ManagerToggleMode.Allow)));
        var created = await allowed.CreateAsync(NewTenant("An", "0901234567"), quanLy);
        Assert.True(created.Succeeded, created.ErrorMessage);
    }

    [SqlServerFact]
    public async Task Quan_ly_sua_va_ngung_ho_so_phai_qua_cong_tac_SW05_SW06()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var quanLy = new TenantCaller("ql-1", Roles.QuanLy);

        var created = await NewTenantService(db).CreateAsync(NewTenant("An", "0901234567"), Manager);
        var id = created.Tenant!.Id;

        // Sửa hồ sơ: Không thể -> 403.
        var deniedUpdate = NewTenantService(db, NewTogglePolicy(("SW-05", ManagerToggleMode.Deny)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => deniedUpdate.UpdateAsync(id, new UpdateTenantRequest
            {
                FullName = "Bị chặn",
                PhoneNumber = "0909999999",
                RowVersion = created.Tenant.RowVersion,
            }, quanLy));

        // Sửa hồ sơ: Duyệt -> ApprovalRequired.
        var pendingUpdate = NewTenantService(db, NewTogglePolicy(("SW-05", ManagerToggleMode.Approve)));
        var pending = await pendingUpdate.UpdateAsync(id, new UpdateTenantRequest
        {
            FullName = "Chờ duyệt",
            PhoneNumber = "0909999999",
            RowVersion = created.Tenant.RowVersion,
        }, quanLy);
        Assert.Equal(TenantErrorCodes.ApprovalRequired, pending.ErrorCode);

        // Chưa có thay đổi nào được ghi.
        var reloaded = await NewTenantService(db).GetAsync(id, Manager);
        Assert.Equal("An", reloaded.Tenant!.FullName);

        // Ngừng sử dụng: Duyệt cũng phải chờ duyệt (SW-06).
        var pendingStop = NewTenantService(db, NewTogglePolicy(("SW-06", ManagerToggleMode.Approve)));
        var stop = await pendingStop.SetActiveAsync(id, false, reloaded.Tenant.RowVersion, quanLy);
        Assert.Equal(TenantErrorCodes.ApprovalRequired, stop.ErrorCode);

        var stillActive = await NewTenantService(db).GetAsync(id, Manager);
        Assert.True(stillActive.Tenant!.IsActive);
    }

    [SqlServerFact]
    public async Task Chu_tro_khong_bi_cong_tac_chan_D4()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();

        // Cả 3 công tắc đều "Không thể" nhưng Chủ trọ vẫn có hiệu lực ngay.
        var service = NewTenantService(db, NewTogglePolicy(
            ("SW-04", ManagerToggleMode.Deny),
            ("SW-05", ManagerToggleMode.Deny),
            ("SW-06", ManagerToggleMode.Deny)));

        var created = await service.CreateAsync(NewTenant("An", "0901234567"), Manager);
        Assert.True(created.Succeeded, created.ErrorMessage);

        var updated = await service.UpdateAsync(created.Tenant!.Id, new UpdateTenantRequest
        {
            FullName = "An",
            PhoneNumber = "0909999999",
            RowVersion = created.Tenant.RowVersion,
        }, Manager);
        Assert.True(updated.Succeeded, updated.ErrorMessage);

        var stopped = await service.SetActiveAsync(
            created.Tenant.Id, false, updated.Tenant!.RowVersion, Manager);
        Assert.True(stopped.Succeeded, stopped.ErrorMessage);
        Assert.False(stopped.Tenant!.IsActive);
    }

    private static CreateTenantRequest NewTenant(
        string fullName, string phone, string? idNumber = null, string? note = null) => new()
        {
            FullName = fullName,
            PhoneNumber = phone,
            IdNumber = idNumber,
            Note = note,
        };

    private static async Task SeedAccountAsync(ApplicationDbContext db, string userId, string roleName)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
        if (role is null)
        {
            role = new IdentityRole(roleName);
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }

        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString(),
            ConcurrencyStamp = Guid.NewGuid().ToString(),
            EmailConfirmed = true,
        });
        await db.SaveChangesAsync();

        db.UserRoles.Add(new IdentityUserRole<string> { UserId = userId, RoleId = role.Id });
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

    // Thiếu mã thì trả Duyệt, đúng chế độ mặc định của danh mục (fail closed).
    private sealed class FakeToggleReader((string Code, ManagerToggleMode Mode)[] modes) : IManagerToggleReader
    {
        private readonly Dictionary<string, ManagerToggleMode> _modes =
            modes.ToDictionary(m => m.Code, m => m.Mode, StringComparer.OrdinalIgnoreCase);

        public Task<ManagerToggleMode> GetModeAsync(string code, CancellationToken ct = default) =>
            Task.FromResult(_modes.TryGetValue(code, out var mode) ? mode : ManagerToggleMode.Approve);
    }
}
