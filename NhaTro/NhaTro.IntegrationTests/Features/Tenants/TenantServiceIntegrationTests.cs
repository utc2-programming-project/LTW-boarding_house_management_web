using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Features.Tenants.Enums;

namespace NhaTro.IntegrationTests.Features.Tenants;

/// <summary>
/// VHT-23 (đợt 1C) — integration test trên SQL Server thật, chứng minh 3 tiêu chí nghiệm thu:
/// lưu rồi tải lại đúng; không liên kết nhầm tài khoản; khách không sửa trường ngoài quyền.
/// </summary>
public sealed class TenantServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly TenantCaller Manager = new("manager-1", Roles.ChuTro);

    // ---------- Tiêu chí 1: lưu rồi tải lại đúng ----------

    [SqlServerFact]
    public async Task Luu_roi_tai_lai_dung_moi_truong()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var service = new TenantService(db, TimeProvider.System);

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
        var reloaded = await new TenantService(reloadDb, TimeProvider.System).GetAsync(tenant.Id, Manager);

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
        var service = new TenantService(db, TimeProvider.System);

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
        var service = new TenantService(db, TimeProvider.System);

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

        var created = await new TenantService(db, TimeProvider.System)
            .CreateAsync(NewTenant("An", "0901234567"), Manager);
        var id = created.Tenant!.Id;

        var linked = await new TenantService(db, TimeProvider.System)
            .LinkAccountAsync(id, "khach-1", created.Tenant.RowVersion, Manager);

        Assert.True(linked.Succeeded, linked.ErrorMessage);
        Assert.True(linked.Tenant!.HasAccount);

        await using var ownDb = fixture.CreateContext();
        var own = await new TenantService(ownDb, TimeProvider.System)
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

        var service = new TenantService(db, TimeProvider.System);
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

        var service = new TenantService(db, TimeProvider.System);
        var created = await service.CreateAsync(
            NewTenant("An", "0901234567", idNumber: "079201004821", note: "nội bộ"), Manager);
        var id = created.Tenant!.Id;

        var linked = await service.LinkAccountAsync(id, "khach-1", created.Tenant.RowVersion, Manager);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        await using var ownDb = fixture.CreateContext();
        var ownService = new TenantService(ownDb, TimeProvider.System);
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
        var reloaded = await new TenantService(checkDb, TimeProvider.System).GetAsync(id, Manager);
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
        var service = new TenantService(db, TimeProvider.System);
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
        var service = new TenantService(db, TimeProvider.System);
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
        var service = new TenantService(db, TimeProvider.System);

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
        var service = new TenantService(db, TimeProvider.System);

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
}
