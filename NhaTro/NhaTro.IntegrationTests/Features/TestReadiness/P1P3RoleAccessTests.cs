using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Infrastructure.Storage;

namespace NhaTro.IntegrationTests.Features.TestReadiness;

/// <summary>
/// VHT-22 (đợt 1D) — kiểm thử P1–P3 theo 4 vai trò trên SQL Server thật:
/// Admin bị chặn nghiệp vụ, Chủ trọ chỉ thấy tòa nhà của mình, Quản lý thấy P2/P3,
/// Khách thuê chỉ xem hồ sơ của mình, validation và tải tệp trái quyền.
/// </summary>
/// <remarks>
/// Các test này dùng service thật + LocalDB riêng của <see cref="SqlServerFixture"/>.
/// P1 chưa có service tạo tài khoản theo tòa nhà trên <c>dev</c> (VHT-8 chưa ghép), nên case
/// “Quản lý tạo tài khoản ngoài tòa nhà bị từ chối” được ghi nhận là <b>blocked</b> trong
/// <c>docs/Imp/VHT-22-kiem-thu-p1-p3-va-ghi-tai-lieu.md</c>, không giả lập bằng test giả.
/// </remarks>
public sealed class P1P3RoleAccessTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly PropertyCaller OwnerA = new("owner-a", Roles.ChuTro);
    private static readonly PropertyCaller OwnerB = new("owner-b", Roles.ChuTro);
    private static readonly PropertyCaller Manager = new("manager-1", Roles.QuanLy);

    private static readonly TenantCaller TenantManager = new("manager-1", Roles.QuanLy);
    // Gắn tài khoản khách thuê là thao tác của Chủ trọ (TenantService.EnsureOwner).
    private static readonly TenantCaller TenantOwner = new("owner-a", Roles.ChuTro);
    private static readonly TenantCaller TenantA = new("khach-1", Roles.KhachThue);
    private static readonly TenantCaller TenantB = new("khach-2", Roles.KhachThue);
    private static readonly TenantCaller Admin = new("admin-1", Roles.Admin);

    private static readonly byte[] PdfBytes =
        Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF");

    private static readonly byte[] JpegBytes =
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private static RoomService NewRoomService(IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        new(dbContextFactory, TimeProvider.System, new RoomChangePolicyStub(), new NoActiveContractRoomUsageChecker());

    private static TenantService NewTenantService(IDbContextFactory<ApplicationDbContext> dbContextFactory) =>
        new(dbContextFactory, TimeProvider.System, new AllowAllTogglePolicy());

    [SqlServerFact]
    public async Task Bon_vai_tro_chi_thay_du_lieu_dung_pham_vi_P1_P3()
    {
        var data = await SeedAsync();
        await using var db = fixture.CreateContext();
        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = NewRoomService(fixture);
        var tenants = new TenantService(fixture, TimeProvider.System, new AllowAllTogglePolicy());

        // Chủ trọ A: chỉ thấy nhà/phòng của mình.
        var propertiesA = await properties.ListAsync(new PropertyFilter(), OwnerA);
        Assert.Equal(1, propertiesA.TotalCount);
        Assert.Equal(data.PropertyA, Assert.Single(propertiesA.Items).Id);

        var roomsA = await rooms.ListAsync(new RoomFilter(), OwnerA);
        Assert.Equal(1, roomsA.TotalCount);
        Assert.Equal(data.RoomA, Assert.Single(roomsA.Items).Id);

        // Chủ trọ A không đọc được dữ liệu của chủ trọ B (404, không lộ tồn tại).
        Assert.Equal(
            PropertyErrorCodes.PropertyNotFound,
            (await properties.GetAsync(data.PropertyB, OwnerA)).ErrorCode);
        Assert.Equal(
            RoomErrorCodes.RoomNotFound,
            (await rooms.GetAsync(data.RoomB, OwnerA)).ErrorCode);

        // Không chỉ đọc: ghi/sửa/xóa/khôi phục ID của chủ trọ B cũng phải bị coi là không tồn tại.
        Assert.Equal(
            PropertyErrorCodes.PropertyNotFound,
            (await properties.UpdateAsync(data.PropertyB, new PropertySave { Name = "Nhà B (sửa chéo)" }, OwnerA)).ErrorCode);
        Assert.Equal(
            RoomErrorCodes.RoomNotFound,
            (await rooms.UpdateAsync(data.RoomB, new RoomSave
            {
                PropertyId = data.PropertyB,
                Code = "P101",
                StandardPrice = 1_000_000m,
            }, OwnerA)).ErrorCode);
        Assert.Equal(
            RoomErrorCodes.RoomNotFound,
            (await rooms.DeleteAsync(data.RoomB, OwnerA)).ErrorCode);

        // Xóa bằng chủ trọ B rồi thử khôi phục bằng chủ trọ A: vẫn không thấy phòng ngoài phạm vi.
        var deletedByB = await rooms.DeleteAsync(data.RoomB, OwnerB);
        Assert.True(deletedByB.Succeeded, deletedByB.ErrorMessage);
        Assert.Equal(
            RoomErrorCodes.RoomNotFound,
            (await rooms.RestoreAsync(data.RoomB, OwnerA)).ErrorCode);
        var restoredByB = await rooms.RestoreAsync(data.RoomB, OwnerB);
        Assert.True(restoredByB.Succeeded, restoredByB.ErrorMessage);

        // VHT-24: Quản lý chưa được phân công thì không thấy tòa nhà/phòng nào.
        Assert.Equal(0, (await properties.ListAsync(new PropertyFilter(), Manager)).TotalCount);
        Assert.Equal(0, (await rooms.ListAsync(new RoomFilter(), Manager)).TotalCount);

        // Admin bị chặn ở service P2 và P3, không chỉ ẩn nút trên UI.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => properties.ListAsync(new PropertyFilter(), new PropertyCaller("admin-1", Roles.Admin)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => rooms.ListAsync(new RoomFilter(), new PropertyCaller("admin-1", Roles.Admin)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.ListAsync(new TenantFilterRequest(), Admin));

        // Khách thuê không vào được danh sách quản lý, nhưng xem được đúng hồ sơ của mình.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => properties.ListAsync(new PropertyFilter(), new PropertyCaller("khach-1", Roles.KhachThue)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.ListAsync(new TenantFilterRequest(), TenantA));

        var own = await tenants.GetAsync(data.TenantA, TenantA);
        Assert.True(own.Succeeded, own.ErrorMessage);
        Assert.Equal(data.TenantA, own.Tenant!.Id);

        var other = await tenants.GetAsync(data.TenantB, TenantA);
        Assert.Equal(TenantErrorCodes.TenantNotFound, other.ErrorCode);
    }

    [SqlServerFact]
    public async Task Quan_ly_chi_thay_va_sua_duoc_toa_nha_duoc_phan_cong()
    {
        var data = await SeedAsync();

        var managers = new PropertyManagerService(fixture, TimeProvider.System);
        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = NewRoomService(fixture);

        // Chưa phân công: Quản lý không thấy tòa nhà/phòng nào.
        Assert.Equal(0, (await properties.ListAsync(new PropertyFilter(), Manager)).TotalCount);
        Assert.Equal(0, (await rooms.ListAsync(new RoomFilter(), Manager)).TotalCount);

        // Chủ trọ B không gán được Quản lý vào nhà của Chủ trọ A (coi như không tồn tại).
        var crossAssign = await managers.AssignAsync(data.PropertyA, "manager-1", OwnerB);
        Assert.Equal(PropertyManagerErrorCodes.PropertyNotFound, crossAssign.ErrorCode);

        // Quản lý không tự gán được cho chính mình.
        var selfAssign = await managers.AssignAsync(data.PropertyA, "manager-1", Manager);
        Assert.Equal(PropertyManagerErrorCodes.Forbidden, selfAssign.ErrorCode);

        // Gán một tài khoản không phải Quản lý bị từ chối.
        var wrongRole = await managers.AssignAsync(data.PropertyA, "owner-b", OwnerA);
        Assert.Equal(PropertyManagerErrorCodes.InvalidManagerRole, wrongRole.ErrorCode);

        // Gán manager-1 -> nhà A.
        var assigned = await managers.AssignAsync(data.PropertyA, "manager-1", OwnerA);
        Assert.True(assigned.Succeeded, assigned.ErrorMessage);

        // Quản lý chỉ thấy nhà A/phòng A.
        var props = await properties.ListAsync(new PropertyFilter(), Manager);
        Assert.Equal(1, props.TotalCount);
        Assert.Equal(data.PropertyA, Assert.Single(props.Items).Id);

        var roomList = await rooms.ListAsync(new RoomFilter(), Manager);
        Assert.Equal(1, roomList.TotalCount);
        Assert.Equal(data.RoomA, Assert.Single(roomList.Items).Id);

        // Không xem/sửa được nhà B/phòng B.
        Assert.Equal(
            PropertyErrorCodes.PropertyNotFound,
            (await properties.GetAsync(data.PropertyB, Manager)).ErrorCode);
        Assert.Equal(
            RoomErrorCodes.RoomNotFound,
            (await rooms.GetAsync(data.RoomB, Manager)).ErrorCode);
        Assert.Equal(
            RoomErrorCodes.RoomNotFound,
            (await rooms.UpdateAsync(data.RoomB, new RoomSave
            {
                PropertyId = data.PropertyB,
                Code = "P101",
                StandardPrice = 9_000_000m,
            }, Manager)).ErrorCode);

        // Gán trùng khi đang hiệu lực bị chặn.
        var duplicate = await managers.AssignAsync(data.PropertyA, "manager-1", OwnerA);
        Assert.Equal(PropertyManagerErrorCodes.AlreadyAssigned, duplicate.ErrorCode);

        // Bỏ gán: mất quyền ngay, nhưng dòng lịch sử vẫn còn.
        var revoked = await managers.RevokeAsync(data.PropertyA, "manager-1", OwnerA);
        Assert.True(revoked.Succeeded, revoked.ErrorMessage);
        Assert.Equal(0, (await properties.ListAsync(new PropertyFilter(), Manager)).TotalCount);

        var active = await managers.ListAssignmentsAsync(data.PropertyA, OwnerA);
        Assert.True(active.Succeeded, active.ErrorMessage);
        Assert.Empty(active.Items);

        var history = await managers.ListAssignmentsAsync(data.PropertyA, OwnerA, includeHistory: true);
        Assert.True(history.Succeeded, history.ErrorMessage);
        Assert.Contains(history.Items, item => item.ManagerUserId == "manager-1" && !item.IsActive);

        // Quản lý không đọc được danh sách phân công (chỉ Chủ trọ).
        var forbiddenList = await managers.ListAssignmentsAsync(data.PropertyA, Manager, includeHistory: true);
        Assert.Equal(PropertyManagerErrorCodes.Forbidden, forbiddenList.ErrorCode);
    }

    [SqlServerFact]
    public async Task Admin_bi_chan_o_moi_service_nghiep_vu_P2_P3()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();

        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = NewRoomService(fixture);
        var tenants = new TenantService(fixture, TimeProvider.System, new AllowAllTogglePolicy());

        var adminProperty = new PropertyCaller("admin-1", Roles.Admin);
        var adminTenant = new TenantCaller("admin-1", Roles.Admin);

        var propertySave = new PropertySave { Name = "Nhà Admin" };
        var roomSave = new RoomSave { PropertyId = 1, Code = "P1", StandardPrice = 1_000_000m };
        var tenantSave = new CreateTenantRequest { FullName = "Khách", PhoneNumber = "0901234567" };
        var tenantUpdate = new UpdateTenantRequest
        {
            FullName = "Khách",
            PhoneNumber = "0901234567",
            RowVersion = "AQID",
        };

        // P2: mọi thao tác đọc/ghi đều bị chặn trước khi truy vấn dữ liệu.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => properties.ListAsync(new PropertyFilter(), adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => properties.GetAsync(1, adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => properties.CreateAsync(propertySave, adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => properties.UpdateAsync(1, propertySave, adminProperty));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => rooms.ListAsync(new RoomFilter(), adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => rooms.GetAsync(1, adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => rooms.CreateAsync(roomSave, adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => rooms.UpdateAsync(1, roomSave, adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => rooms.DeleteAsync(1, adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => rooms.RestoreAsync(1, adminProperty));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => rooms.SearchForPickerAsync(new RoomOptionRequest(), adminProperty));

        // P3: mọi thao tác quản lý/hồ sơ đều bị chặn.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.ListAsync(new TenantFilterRequest(), adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.GetAsync(1, adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.GetOwnAsync(adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.CreateAsync(tenantSave, adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.UpdateAsync(1, tenantUpdate, adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.UpdateOwnAsync(
                new UpdateOwnProfileRequest { PhoneNumber = "0901234567", RowVersion = "AQID" },
                adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.SetActiveAsync(1, false, "AQID", adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.LinkAccountAsync(1, "khach-1", "AQID", adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.UnlinkAccountAsync(1, "AQID", adminTenant));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => tenants.SearchForPickerAsync(new TenantPickerRequest(), adminTenant));
    }

    [SqlServerFact]
    public async Task Validation_P2_P3_chan_du_lieu_thieu_va_trung()
    {
        var data = await SeedAsync();
        await using var db = fixture.CreateContext();
        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = NewRoomService(fixture);
        var tenants = new TenantService(fixture, TimeProvider.System, new AllowAllTogglePolicy());

        // P2: thiếu tên nhà, giá âm, mã phòng trùng trong cùng nhà.
        var blankProperty = await properties.CreateAsync(new PropertySave { Name = "   " }, OwnerA);
        Assert.Equal(PropertyErrorCodes.InvalidRequest, blankProperty.ErrorCode);

        var negativePrice = await rooms.CreateAsync(new RoomSave
        {
            PropertyId = data.PropertyA,
            Code = "P102",
            StandardPrice = -1,
        }, OwnerA);
        Assert.Equal(RoomErrorCodes.InvalidRequest, negativePrice.ErrorCode);

        var duplicateCode = await rooms.CreateAsync(new RoomSave
        {
            PropertyId = data.PropertyA,
            Code = " p101 ",
            StandardPrice = 2_000_000m,
        }, OwnerA);
        Assert.Equal(RoomErrorCodes.DuplicateRoomCode, duplicateCode.ErrorCode);

        // P3: thiếu/sai số điện thoại, trùng CCCD.
        var badPhone = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách sai số",
            PhoneNumber = "123",
        }, TenantManager);
        Assert.Equal(TenantErrorCodes.InvalidRequest, badPhone.ErrorCode);

        var duplicateId = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Khách trùng giấy tờ",
            PhoneNumber = "0903333333",
            IdNumber = "079 201 004821",
        }, TenantManager);
        Assert.Equal(TenantErrorCodes.DuplicateTenant, duplicateId.ErrorCode);
    }

    [SqlServerFact]
    public async Task P1_chi_admin_doi_vai_tro_va_quan_ly_khong_tu_nang_quyen()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "admin-1", Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, "ql-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        // Tao san role dich de UserManager.AddToRoleAsync tim thay theo NormalizedName.
        await SqlServerFixture.SeedAccountAsync(db, "owner-1", Roles.ChuTro);

        var roleService = new RoleService(NewUserManager(db));

        // Quản lý không được đổi vai trò (tương đương không được tự cấp quyền tài khoản).
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => roleService.ChangeRoleAsync("ql-1", callerIsAdmin: false, "khach-1", Roles.ChuTro));

        // Admin đổi được vai trò của người khác.
        await roleService.ChangeRoleAsync("admin-1", callerIsAdmin: true, "khach-1", Roles.ChuTro);

        // Admin không được tự đổi vai trò của chính mình; vai trò lạ bị chặn.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => roleService.ChangeRoleAsync("admin-1", callerIsAdmin: true, "admin-1", Roles.QuanLy));
        await Assert.ThrowsAsync<ArgumentException>(
            () => roleService.ChangeRoleAsync("admin-1", callerIsAdmin: true, "ql-1", "SuperAdmin"));
    }

    [SqlServerFact]
    public async Task Tai_tep_trai_quyen_bi_chan_va_khach_chi_tai_duoc_tep_cua_minh()
    {
        var data = await SeedAsync();
        var storage = new InMemoryFileStorage();
        var files = NewFileService(storage);

        var khach1 = Caller("khach-1", Roles.KhachThue);
        var khach2 = Caller("khach-2", Roles.KhachThue);
        var admin = Caller("admin-1", Roles.Admin);

        // Khách thuê không được upload ảnh phòng (chỉ Chủ trọ/Quản lý).
        var roomPhoto = await files.UploadAsync(Upload(
            FileCategory.RoomPhoto, RelatedEntityType.Room, data.RoomA, "phong.jpg", JpegBytes), khach1);
        Assert.False(roomPhoto.Succeeded);
        Assert.Equal(StorageErrorCodes.Forbidden, roomPhoto.ErrorCode);

        // Khách thuê không được gắn giấy tờ vào hồ sơ của khách khác.
        var otherTenant = await files.UploadAsync(Upload(
            FileCategory.IdentityDocument, RelatedEntityType.Tenant, data.TenantB, "cccd.pdf", PdfBytes), khach1);
        Assert.False(otherTenant.Succeeded);
        Assert.Equal(StorageErrorCodes.Forbidden, otherTenant.ErrorCode);

        // Đúng hồ sơ của mình: upload và tải lại được.
        var own = await files.UploadAsync(Upload(
            FileCategory.IdentityDocument, RelatedEntityType.Tenant, data.TenantA, "cccd.pdf", PdfBytes), khach1);
        Assert.True(own.Succeeded, own.ErrorMessage);
        Assert.NotNull(own.File);

        var downloadOwn = await files.DownloadAsync(own.File!.Id, khach1);
        Assert.NotNull(downloadOwn);
        await using (downloadOwn!.FileStream)
        {
            Assert.True(downloadOwn.FileSizeBytes > 0);
        }

        // Khách khác tải tệp đó: 404 (null), không lộ tệp tồn tại.
        Assert.Null(await files.DownloadAsync(own.File.Id, khach2));

        // Admin bị chặn ngay ở cổng nghiệp vụ, không phụ thuộc quyền theo loại tệp.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => files.UploadAsync(Upload(
                FileCategory.IdentityDocument, RelatedEntityType.Tenant, data.TenantA, "cccd.pdf", PdfBytes), admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => files.DownloadAsync(own.File.Id, admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => files.ListAsync(RelatedEntityType.Tenant, data.TenantA.ToString(), admin));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => files.DeleteAsync(own.File.Id, admin));
    }

    private async Task<SeedData> SeedAsync()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();

        await SqlServerFixture.SeedAccountAsync(db, "admin-1", Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "owner-b", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "manager-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, "khach-2", Roles.KhachThue);

        var properties = new PropertyService(fixture, TimeProvider.System);
        var rooms = NewRoomService(fixture);
        var tenants = new TenantService(fixture, TimeProvider.System, new AllowAllTogglePolicy());

        var propertyA = await properties.CreateAsync(new PropertySave { Name = "Nhà A" }, OwnerA);
        var propertyB = await properties.CreateAsync(new PropertySave { Name = "Nhà B" }, OwnerB);
        Assert.True(propertyA.Succeeded, propertyA.ErrorMessage);
        Assert.True(propertyB.Succeeded, propertyB.ErrorMessage);

        var roomA = await rooms.CreateAsync(new RoomSave
        {
            PropertyId = propertyA.Property!.Id,
            Code = "P101",
            StandardPrice = 1_500_000m,
        }, OwnerA);
        var roomB = await rooms.CreateAsync(new RoomSave
        {
            PropertyId = propertyB.Property!.Id,
            Code = "P101",
            StandardPrice = 2_000_000m,
        }, OwnerB);
        Assert.True(roomA.Succeeded, roomA.ErrorMessage);
        Assert.True(roomB.Succeeded, roomB.ErrorMessage);

        var tenantA = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Nguyễn Văn An",
            PhoneNumber = "0901111111",
            IdNumber = "079201004821",
        }, TenantManager);
        var tenantB = await tenants.CreateAsync(new CreateTenantRequest
        {
            FullName = "Trần Thị Bình",
            PhoneNumber = "0902222222",
            IdNumber = "079201004822",
        }, TenantManager);
        Assert.True(tenantA.Succeeded, tenantA.ErrorMessage);
        Assert.True(tenantB.Succeeded, tenantB.ErrorMessage);

        var linkedA = await tenants.LinkAccountAsync(
            tenantA.Tenant!.Id, "khach-1", tenantA.Tenant.RowVersion, TenantOwner);
        var linkedB = await tenants.LinkAccountAsync(
            tenantB.Tenant!.Id, "khach-2", tenantB.Tenant.RowVersion, TenantOwner);
        Assert.True(linkedA.Succeeded, linkedA.ErrorMessage);
        Assert.True(linkedB.Succeeded, linkedB.ErrorMessage);

        return new SeedData(
            propertyA.Property.Id,
            propertyB.Property.Id,
            roomA.Room!.Id,
            roomB.Room!.Id,
            tenantA.Tenant.Id,
            tenantB.Tenant.Id);
    }

    private FileService NewFileService(IFileStorage storage)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(fixture.ConnectionString)
            .Options;

        return new FileService(
            new TestDbContextFactory(options),
            storage,
            Options.Create(new Web.Configuration.Options.StorageOptions()),
            TimeProvider.System,
            NullLogger<FileService>.Instance);
    }

    private static UserManager<ApplicationUser> NewUserManager(ApplicationDbContext db)
    {
        var store = new UserStore<ApplicationUser>(db);
        return new UserManager<ApplicationUser>(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private static ClaimsPrincipal Caller(string userId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role),
        ], authenticationType: "Test"));

    private static UploadFileRequest Upload(
        FileCategory category,
        RelatedEntityType relatedEntityType,
        int relatedEntityId,
        string fileName,
        byte[] content) =>
        new()
        {
            File = new FormFile(new MemoryStream(content), 0, content.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = category is FileCategory.RoomPhoto ? "image/jpeg" : "application/pdf",
            },
            FileCategory = category,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId.ToString(),
        };

    private sealed record SeedData(
        int PropertyA,
        int PropertyB,
        int RoomA,
        int RoomB,
        int TenantA,
        int TenantB);

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class InMemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public Task<StoredFileWriteResult> SaveAsync(
            ReadOnlyMemory<byte> content,
            string extension,
            DateTimeOffset uploadedAt,
            CancellationToken cancellationToken = default)
        {
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var relativePath = $"{uploadedAt:yyyy}/{uploadedAt:MM}/{storedFileName}";
            _files[relativePath] = content.ToArray();
            return Task.FromResult(new StoredFileWriteResult(storedFileName, relativePath, content.Length));
        }

        public Stream? TryOpenRead(string relativePath) =>
            _files.TryGetValue(relativePath, out var bytes) ? new MemoryStream(bytes, writable: false) : null;

        public bool Exists(string relativePath) => _files.ContainsKey(relativePath);

        public void Delete(string relativePath) => _files.Remove(relativePath);
    }

    /// <summary>P1–P3 chưa cần kiểm công tắc nên cho phép hết để giữ nguyên hành vi cũ.</summary>
    private sealed class AllowAllTogglePolicy : IManagerTogglePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(
            ManagerAction action, string role, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.Allowed);

        public Task<RoomChangeDecision> EvaluateAsync(
            string code, string role, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.Allowed);
    }
}
