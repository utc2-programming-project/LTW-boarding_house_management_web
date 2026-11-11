using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.DTOs;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Services;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Services;
using Web.Features.Invoices;
using Web.Features.Invoices.DTOs;
using Web.Features.Invoices.Enums;
using Web.Features.Invoices.Services;
using Web.Features.Reports.Services;
using Web.Features.Maintenance;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Services;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Properties.Services;
using Web.Features.Reports;
using Web.Features.Rooms;
using Web.Features.Rooms.DTOs;
using Web.Features.Settings;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Services;
using Web.Infrastructure.Storage;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Services;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// Dữ liệu nền cho bộ test ma trận quyền: hai nhà trọ của hai Chủ trọ khác nhau, một Quản lý
/// chỉ được giao nhà A, hai khách thuê có hợp đồng đã duyệt ở hai nhà khác nhau.
/// Cần hai nhà để test chứng minh được "truy cập chéo" (ô ⚠️ phải từ chối ngoài phạm vi).
/// </summary>
public sealed class PermissionMatrixWorld : IAsyncLifetime
{
    public const string AdminId = "pm-admin-1";
    public const string OwnerAId = "pm-owner-a";
    public const string OwnerBId = "pm-owner-b";
    public const string ManagerAId = "pm-manager-a";
    public const string TenantUserAId = "pm-khach-1";
    public const string TenantUserBId = "pm-khach-2";

    /// <summary>Mật khẩu dùng riêng cho tài khoản test của ma trận (không phải credential thật).</summary>
    public const string AccountPassword = "PmTest123!";

    public static string EmailForAccount(string userId) => $"{userId}@test.local";

    private readonly SqlServerFixture _fixture;
    private ApplicationDbContext? _sharedDb;
    private ManagerTogglePolicy? _togglePolicy;

    public PermissionMatrixWorld(SqlServerFixture fixture) => _fixture = fixture;

    public WorldData Data { get; private set; } = null!;

    /// <summary>Policy công tắc THẬT: đọc chế độ từ SQL nên đổi công tắc là đổi hành vi nghiệp vụ.</summary>
    public IManagerTogglePolicy TogglePolicy => _togglePolicy!;

    public IRoomChangePolicy RoomChangePolicy => _togglePolicy!;

    public SqlServerFixture Fixture => _fixture;

    public async Task InitializeAsync()
    {
        if (!SqlServerFixture.Available) return;

        // Reader tạo DbContext mới cho mỗi lần đọc: không cache, giống app thật (VHT-94),
        // nên SetSwitchAsync của test có hiệu lực ngay với mọi service cùng DB.
        _togglePolicy = new ManagerTogglePolicy(
            new PerCallToggleReader(_fixture),
            NullLogger<ManagerTogglePolicy>.Instance);

        _sharedDb = _fixture.CreateContext();
        Data = await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sharedDb is not null)
        {
            await _sharedDb.DisposeAsync();
            _sharedDb = null;
        }
    }

    // === Callers theo vai trò trong ma trận ===

    public PropertyCaller OwnerA => new(OwnerAId, Roles.ChuTro);
    public PropertyCaller OwnerB => new(OwnerBId, Roles.ChuTro);
    public PropertyCaller Manager => new(ManagerAId, Roles.QuanLy);
    public PropertyCaller AdminCaller => new(AdminId, Roles.Admin);
    public PropertyCaller TenantAAsPropertyCaller => new(TenantUserAId, Roles.KhachThue);
    public PropertyCaller TenantBAsPropertyCaller => new(TenantUserBId, Roles.KhachThue);

    public TenantCaller OwnerATenant => new(OwnerAId, Roles.ChuTro);
    public TenantCaller OwnerBTenant => new(OwnerBId, Roles.ChuTro);
    public TenantCaller ManagerTenant => new(ManagerAId, Roles.QuanLy);
    public TenantCaller AdminTenant => new(AdminId, Roles.Admin);
    public TenantCaller TenantATenant => new(TenantUserAId, Roles.KhachThue);
    public TenantCaller TenantBTenant => new(TenantUserBId, Roles.KhachThue);

    public ApprovalCaller ManagerApproval => new(ManagerAId, Roles.QuanLy);
    public ApprovalCaller OwnerAApproval => new(OwnerAId, Roles.ChuTro);
    public ApprovalCaller AdminApproval => new(AdminId, Roles.Admin);
    public ApprovalCaller TenantAApproval => new(TenantUserAId, Roles.KhachThue);

    public ManagerToggleCaller OwnerAToggle => new(OwnerAId, Roles.ChuTro);
    public ManagerToggleCaller ManagerToggle => new(ManagerAId, Roles.QuanLy);
    public ManagerToggleCaller AdminToggle => new(AdminId, Roles.Admin);

    public PropertyCaller CallerFor(string role) => role switch
    {
        Roles.Admin => AdminCaller,
        Roles.ChuTro => OwnerA,
        Roles.QuanLy => Manager,
        Roles.KhachThue => TenantAAsPropertyCaller,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Vai trò không có trong ma trận."),
    };

    public TenantCaller TenantCallerFor(string role) => role switch
    {
        Roles.Admin => AdminTenant,
        Roles.ChuTro => OwnerATenant,
        Roles.QuanLy => ManagerTenant,
        Roles.KhachThue => TenantATenant,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Vai trò không có trong ma trận."),
    };

    public string EmailFor(string role) => role switch
    {
        Roles.Admin => $"{AdminId}@test.local",
        Roles.ChuTro => "chutro@nhatro.local",
        Roles.QuanLy => "quanly@nhatro.local",
        Roles.KhachThue => "khachthue@nhatro.local",
        _ => string.Empty,
    };

    // === Service factories: service mới trên DbContext của cùng một DB test ===

    public ApplicationDbContext NewDb() => _fixture.CreateContext();

    public ApplicationDbContext SharedDb => _sharedDb
        ?? throw new InvalidOperationException("Thế giới ma trận chưa khởi tạo (SQL Server không khả dụng?).");

    public UserManager<ApplicationUser> NewUserManager(ApplicationDbContext db) =>
        new(
            new UserStore<ApplicationUser>(db),
            Options.Create(new IdentityOptions
            {
                Password = { RequiredLength = 8, RequireDigit = true, RequireLowercase = true, RequireUppercase = true },
                User = { RequireUniqueEmail = true },
            }),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            NullLogger<UserManager<ApplicationUser>>.Instance);

    public AccountService NewAccounts() => new(
        NewUserManager(SharedDb),
        _fixture,
        NullLogger<AccountService>.Instance);

    public RoomService NewRooms() =>
        new(_fixture, TimeProvider.System, RoomChangePolicy, new ActiveContractRoomUsageChecker(_fixture, TimeProvider.System));

    public PropertyService NewProperties() => new(_fixture, TimeProvider.System);

    public PropertyManagerService NewAssignments() => new(_fixture, TimeProvider.System);

    public TenantService NewTenants() => new(_fixture, TimeProvider.System, TogglePolicy, NewApprovals());

    public ContractService NewContracts() => new(SharedDb, TimeProvider.System, TogglePolicy, NewApprovals());

    public InvoiceQueryService NewInvoiceQuery() => new(SharedDb);

    public InvoiceService NewInvoices() => new(
        SharedDb,
        new InvoiceQueryService(SharedDb),
        TimeProvider.System,
        TogglePolicy,
        NewApprovals());

    public AuditLogService NewAuditLogs() => new(SharedDb);

    public UtilityReadingService NewUtilityReadings() => new(
        SharedDb, TimeProvider.System, TogglePolicy, NewApprovals());

    public PaymentService NewPayments() => new(SharedDb, TimeProvider.System);

    public RevenueReportService NewRevenueReports() => new(SharedDb);

    public ReportExportService NewReportExports() => new(
        SharedDb, TogglePolicy, NewApprovals());

    public IncidentService NewIncidents() => new(_fixture, TimeProvider.System, TogglePolicy, NewApprovals());

    public IncidentFileScope NewIncidentFileScope() => new(_fixture, TimeProvider.System);

    /// <summary>Đường upload ảnh THẬT (OP43/SW-14): chỉ khác app thật ở chỗ byte nằm trong bộ nhớ.</summary>
    public IncidentPhotoService NewIncidentPhotos() => new(
        _fixture,
        new InMemoryFileStorage(),
        NewIncidentFileScope(),
        TogglePolicy,
        NewApprovals(),
        Options.Create(new StorageOptions
        {
            RootPath = "memory",
            MaxFilesPerEntity = new Dictionary<string, int?> { [nameof(FileCategory.IncidentPhoto)] = 3 },
        }),
        TimeProvider.System,
        new FileAccessAuditWriter(_fixture, TimeProvider.System, NullLogger<FileAccessAuditWriter>.Instance),
        NullLogger<IncidentPhotoService>.Instance);

    public ServiceCatalogService NewServiceCatalog() => new(SharedDb, TimeProvider.System);

    public RoomReportService NewRoomReports() => new(_fixture, TogglePolicy);

    public TenantReportService NewTenantReports() => new(_fixture, TimeProvider.System, TogglePolicy);

    public ManagerApprovalService NewApprovals() =>
        new(SharedDb, TimeProvider.System, Array.Empty<IApprovalApplier>(), NullLogger<ManagerApprovalService>.Instance);

    public ManagerToggleService NewToggles() =>
        new(SharedDb, TimeProvider.System, NullLogger<ManagerToggleService>.Instance);

    /// <summary>Đổi chế độ công tắc SW-xx đúng như Chủ trọ làm trên /manager-switches.</summary>
    public async Task SetSwitchAsync(string code, ManagerToggleMode mode)
    {
        var result = await NewToggles().SetAsync(code, mode, OwnerAToggle);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Không đặt được công tắc {code} = {mode}: {result.ErrorCode}");
        }
    }

    public async Task<ManagerToggleMode> GetSwitchAsync(string code) => await NewToggles().GetModeAsync(code);

    /// <summary>Tạo phòng mới trong nhà A để probe P5 không dùng chung dữ liệu/khóa duy nhất.</summary>
    public async Task<int> CreateRoomAsync(string code)
    {
        var result = await NewRooms().CreateAsync(
            new RoomSave
            {
                PropertyId = Data.PropertyAId,
                Code = code,
                StandardPrice = 1_000_000m,
            },
            OwnerA);

        if (!result.Succeeded || result.Room is null)
        {
            throw new InvalidOperationException(
                $"Không tạo được phòng probe '{code}': {result.ErrorCode}");
        }

        return result.Room.Id;
    }

    /// <summary>
    /// Policy công tắc THẬT cho các test phân hệ: đọc chế độ trực tiếp từ SQL trên chính DB của
    /// fixture nên hành vi giống app thật. Test phân hệ không được dùng <c>RoomChangePolicyStub</c>
    /// hay policy tự chế trả lời cố định — làm vậy là mock đúng bộ phân quyền đang bị kiểm thử.
    /// </summary>
    public static ManagerTogglePolicy NewRealTogglePolicy(SqlServerFixture fixture) =>
        new(new PerCallToggleReader(fixture), NullLogger<ManagerTogglePolicy>.Instance);

    /// <summary>Hàng đợi duyệt THẬT trên DB test, dùng chung với policy ở trên.</summary>
    public static ManagerApprovalService NewRealApprovals(ApplicationDbContext db) =>
        new(db, TimeProvider.System, Array.Empty<IApprovalApplier>(), NullLogger<ManagerApprovalService>.Instance);

    /// <summary>
    /// Luồng ảnh sự cố THẬT (OP43) cho test phân hệ: cùng IncidentPhotoService, IncidentFileScope,
    /// audit writer và StorageOptions như app thật; khác ở chỗ byte nằm trong bộ nhớ, không ghi đĩa.
    /// </summary>
    public static IncidentPhotoService NewRealIncidentPhotos(SqlServerFixture fixture, ApplicationDbContext db) => new(
        fixture,
        new InMemoryFileStorage(),
        new IncidentFileScope(fixture, TimeProvider.System),
        NewRealTogglePolicy(fixture),
        NewRealApprovals(db),
        Options.Create(new StorageOptions
        {
            RootPath = "memory",
            MaxFilesPerEntity = new Dictionary<string, int?> { [nameof(FileCategory.IncidentPhoto)] = 3 },
        }),
        TimeProvider.System,
        new FileAccessAuditWriter(fixture, TimeProvider.System, NullLogger<FileAccessAuditWriter>.Instance),
        NullLogger<IncidentPhotoService>.Instance);

    private async Task<WorldData> SeedAsync()
    {
        await _fixture.ResetAsync();
        await using (var setup = NewDb())
        {
            await SqlServerFixture.SeedAccountAsync(setup, AdminId, Roles.Admin);
            await SqlServerFixture.SeedAccountAsync(setup, OwnerAId, Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(setup, OwnerBId, Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(setup, ManagerAId, Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(setup, TenantUserAId, Roles.KhachThue);
            await SqlServerFixture.SeedAccountAsync(setup, TenantUserBId, Roles.KhachThue);

            // OP01 cần đăng nhập bằng credential thật, nên tài khoản test phải có mật khẩu đã băm.
            foreach (var userId in new[] { AdminId, OwnerAId, OwnerBId, ManagerAId, TenantUserAId, TenantUserBId })
            {
                var user = await setup.Users.SingleAsync(u => u.Id == userId);
                user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, AccountPassword);
            }
            await setup.SaveChangesAsync();
        }

        var properties = NewProperties();
        var rooms = NewRooms();

        var propertyA = await properties.CreateAsync(new PropertySave { Name = "Nhà A" }, OwnerA);
        var propertyB = await properties.CreateAsync(new PropertySave { Name = "Nhà B" }, OwnerB);
        if (!propertyA.Succeeded || !propertyB.Succeeded)
        {
            throw new InvalidOperationException($"Seed nhà trọ thất bại: {propertyA.ErrorCode} / {propertyB.ErrorCode}");
        }

        var roomA = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyA.Property!.Id, Code = "A101", StandardPrice = 1_500_000m }, OwnerA);
        var roomSpare = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyA.Property.Id, Code = "A102", StandardPrice = 2_000_000m }, OwnerA);
        // Nhà B cố ý dùng lại mã "A101": mã phòng chỉ duy nhất trong từng nhà trọ (ERD VHT-5),
        // nên world của ma trận quyền đồng thời là chốt chặn cho quy tắc đó.
        var roomB = await rooms.CreateAsync(
            new RoomSave { PropertyId = propertyB.Property!.Id, Code = "A101", StandardPrice = 1_800_000m }, OwnerB);
        if (!roomA.Succeeded || !roomSpare.Succeeded || !roomB.Succeeded)
        {
            throw new InvalidOperationException(
                $"Seed phòng thất bại: {roomA.ErrorCode} / {roomSpare.ErrorCode} / {roomB.ErrorCode}");
        }

        await NewAssignments().AssignAsync(propertyA.Property.Id, ManagerAId, OwnerA);

        var tenants = NewTenants();
        var tenantA = await tenants.CreateAsync(
            new CreateTenantRequest { FullName = "Khách A trong phạm vi", PhoneNumber = "0901111111", IdNumber = "079201000001" },
            OwnerATenant);
        var tenantB = await tenants.CreateAsync(
            new CreateTenantRequest { FullName = "Khách B ngoài phạm vi", PhoneNumber = "0902222222", IdNumber = "079201000002" },
            OwnerBTenant);
        var tenantFree = await tenants.CreateAsync(
            new CreateTenantRequest { FullName = "Khách C chưa có hợp đồng", PhoneNumber = "0903333333", IdNumber = "079201000003" },
            OwnerATenant);
        var tenantAssignableA = await tenants.CreateAsync(
            new CreateTenantRequest { FullName = "Khách gán TK Nhà A", PhoneNumber = "0904444444", IdNumber = "079201000004" },
            OwnerATenant);
        var tenantAssignableB = await tenants.CreateAsync(
            new CreateTenantRequest { FullName = "Khách gán TK Nhà B", PhoneNumber = "0905555555", IdNumber = "079201000005" },
            OwnerBTenant);
        if (!tenantA.Succeeded || !tenantB.Succeeded || !tenantFree.Succeeded || !tenantAssignableA.Succeeded || !tenantAssignableB.Succeeded)
        {
            throw new InvalidOperationException($"Seed hồ sơ khách thất bại: tenantA={tenantA.ErrorCode}, tenantB={tenantB.ErrorCode}, tenantFree={tenantFree.ErrorCode}");
        }

        await tenants.LinkAccountAsync(tenantA.Tenant!.Id, TenantUserAId, tenantA.Tenant.RowVersion, OwnerATenant);
        await tenants.LinkAccountAsync(tenantB.Tenant!.Id, TenantUserBId, tenantB.Tenant.RowVersion, OwnerBTenant);

        var today = DateTime.UtcNow.Date;
        var contracts = NewContracts();

        var contractA = await contracts.CreateContractAsync(
            new CreateContractRequest(roomA.Room!.Id, today, today.AddMonths(6), [tenantA.Tenant!.Id, tenantAssignableA.Tenant!.Id]), OwnerA);
        await contracts.ApproveContractAsync(
            contractA.ContractId, new ApproveContractRequestDto { IsApproved = true }, OwnerA);

        var contractB = await contracts.CreateContractAsync(
            new CreateContractRequest(roomB.Room!.Id, today, today.AddMonths(6), [tenantB.Tenant!.Id, tenantAssignableB.Tenant!.Id]), OwnerB);
        await contracts.ApproveContractAsync(
            contractB.ContractId, new ApproveContractRequestDto { IsApproved = true }, OwnerB);

        var invoices = NewInvoices();
        var draft = await invoices.CreateDraftAsync(
            new CreateInvoiceRequest
            {
                ContractId = contractA.ContractId,
                PeriodStart = DateOnly.FromDateTime(today),
                PeriodEnd = DateOnly.FromDateTime(today.AddMonths(1).AddDays(-1)),
                Items = [new InvoiceItemInputDto
                {
                    ItemType = InvoiceItemType.Rent,
                    Description = "Tiền thuê kỳ 1",
                    Quantity = 1,
                    UnitPrice = 1_500_000m,
                }],
            },
            OwnerA);
        var secondDraft = await invoices.CreateDraftAsync(
            new CreateInvoiceRequest
            {
                ContractId = contractA.ContractId,
                PeriodStart = DateOnly.FromDateTime(today.AddMonths(1)),
                PeriodEnd = DateOnly.FromDateTime(today.AddMonths(2).AddDays(-1)),
                Items = [new InvoiceItemInputDto
                {
                    ItemType = InvoiceItemType.Rent,
                    Description = "Tiền thuê kỳ 2",
                    Quantity = 1,
                    UnitPrice = 1_500_000m,
                }],
            },
            OwnerA);
        var issuedInvoice = await invoices.IssueAsync(secondDraft.Id, OwnerA);

        var serviceCatalog = await NewServiceCatalog().CreateOrUpdateServicePriceAsync(
            new CreateServiceCatalogDto
            {
                PropertyId = propertyA.Property.Id,
                Name = "Internet",
                Unit = "tháng",
                CalculationMethod = "FixedMonthly",
                UnitPrice = 150_000m,
                EffectiveDate = DateOnly.FromDateTime(today),
            },
            OwnerA);

        var incidentA = await NewIncidents().CreateAsync(
            new IncidentCreateRequest
            {
                RoomId = roomA.Room.Id,
                Title = "Hỏng điều hòa phòng A101",
                Description = "Máy không lạnh",
                Category = IncidentCategory.Equipment,
                Urgency = IncidentUrgency.Normal,
            },
            OwnerA);

        //OP40/43 ô ⚠️ cần một phiếu ngoài phạm vi để chứng minh vai trò bị chặn đọc chéo.
        var auditLogs = NewAuditLogs();
        await auditLogs.RecordAsync(
            AuditLogCategory.Account, "AccountLocked", AdminId, metadata: "Khóa tài khoản demo");
        await auditLogs.RecordAsync(
            AuditLogCategory.Business, "MeterReadingAdjusted", OwnerAId, propertyA.Property.Id,
            "Điều chỉnh chỉ số phòng A101");
        await auditLogs.RecordAsync(
            AuditLogCategory.Business, "TenantDocumentChanged", OwnerBId, propertyB.Property.Id,
            "Thay giấy tờ khách B");

        await NewUtilityReadings().RecordAsync(
            new RecordUtilityReadingRequest(
                roomA.Room.Id,
                DateOnly.FromDateTime(today.AddMonths(-1)),
                DateOnly.FromDateTime(today.AddDays(-1)),
                ElectricEnd: 120m,
                WaterEnd: 60m),
            OwnerA);

        var incidentB = await NewIncidents().CreateAsync(
            new IncidentCreateRequest
            {
                RoomId = roomB.Room!.Id,
                Title = "Hỏng đèn phòng B-side A101",
                Description = "Đèn nhấp nháy",
                Category = IncidentCategory.Electrical,
                Urgency = IncidentUrgency.Normal,
            },
            OwnerB);

        if (incidentA.Incident is null || incidentB.Incident is null)
        {
            throw new InvalidOperationException($"Seed phiếu sự cố thất bại: {incidentA.ErrorCode} / {incidentB.ErrorCode}");
        }

        return new WorldData(
            propertyA.Property.Id,
            propertyB.Property.Id,
            roomA.Room!.Id,
            roomSpare.Room!.Id,
            roomB.Room!.Id,
            tenantA.Tenant!.Id,
            tenantB.Tenant!.Id,
            tenantFree.Tenant!.Id,
            tenantAssignableA.Tenant!.Id,
            tenantAssignableB.Tenant!.Id,
            contractA.ContractId,
            contractB.ContractId,
            draft.Id,
            issuedInvoice.Id,
            serviceCatalog.Id,
            incidentA.Incident.Id,
            incidentB.Incident.Id,
            today);
    }

    /// <summary>
    /// Reader công tắc không giữ DbContext: mỗi lần đọc mở một context mới nên chế độ vừa ghi
    /// có hiệu lực ngay, giống app thật.
    /// </summary>
    private sealed class PerCallToggleReader(SqlServerFixture fixture) : IManagerToggleReader
    {
        public async Task<ManagerToggleMode> GetModeAsync(string code, CancellationToken ct = default)
        {
            await using var db = fixture.CreateContext();
            return await new ManagerToggleService(db, TimeProvider.System, NullLogger<ManagerToggleService>.Instance)
                .GetModeAsync(code, ct);
        }
    }

    /// <summary>
    /// Storage tệp trong bộ nhớ: giữ nguyên hợp đồng IFileStorage để IncidentPhotoService chạy
    /// toàn bộ luồng thật (kiểm tra loại/size/DB/quyết định SW-14) mà không ghi đĩa.
    /// </summary>
    private sealed class InMemoryFileStorage : IFileStorage
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

        public Task<StoredFileWriteResult> SaveAsync(
            ReadOnlyMemory<byte> content,
            string extension,
            DateTimeOffset uploadedAt,
            CancellationToken cancellationToken = default)
        {
            var name = Guid.NewGuid().ToString("N") + extension;
            var path = $"memory/{name}";
            _files[path] = content.ToArray();
            return Task.FromResult(new StoredFileWriteResult(name, path, content.Length));
        }

        public Stream? TryOpenRead(string relativePath) => _files.TryGetValue(relativePath, out var content)
            ? new MemoryStream(content, writable: false)
            : null;

        public bool Exists(string relativePath) => _files.ContainsKey(relativePath);

        public void Delete(string relativePath) => _files.Remove(relativePath);
    }

    public sealed record WorldData(
        int PropertyAId,
        int PropertyBId,
        int RoomAId,
        int RoomSpareId,
        int RoomBId,
        int TenantAId,
        int TenantBId,
        int TenantFreeId,
        int TenantAssignableAId,
        int TenantAssignableBId,
        Guid ContractAId,
        Guid ContractBId,
        int InvoiceDraftId,
        int InvoiceIssuedId,
        int ServiceCatalogId,
        int IncidentAId,
        int IncidentBId,
        DateTime Today);
}
