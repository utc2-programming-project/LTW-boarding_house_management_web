using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Web.Data;
using Web.Features.AccessControl;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.AccessControl.Services;
using Web.Features.Accounts;
using Web.Features.Audit.Services;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Tenants.Entities;
using Web.Infrastructure.Auditing;

namespace NhaTro.UnitTests.Features.AccessControl;

public sealed class GatePassInviteServiceTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private const string OtherOwnerId = "owner-2";
    private const string TenantUserId = "tenant-1";
    private const int PropertyId = 1;
    private const int OtherPropertyId = 2;
    private const int TenantRoomId = 10;
    private const int SecondRoomId = 11;

    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.FromHours(7));

    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly FakeNotificationService _notifications = new();
    private readonly string _auditRoot = Path.Combine(Path.GetTempPath(), "NhaTro_GateInviteTests", Guid.NewGuid().ToString("N"));

    public GatePassInviteServiceTests()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
    }

    public void Dispose()
    {
        if (Directory.Exists(_auditRoot))
        {
            Directory.Delete(_auditRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Tenant_tao_link_chi_luu_hash_va_gan_dung_phong()
    {
        await SeedAsync(GatePolicyDefaults());

        var result = await Service().CreateAsync(new GateInviteCreateRequest { RoomId = TenantRoomId },
            new PropertyCaller(TenantUserId, Roles.KhachThue));

        Assert.True(result.Succeeded);
        var created = Assert.IsType<GateInviteCreatedDto>(result.Created);
        Assert.False(string.IsNullOrWhiteSpace(created.RawToken));
        Assert.Equal(TenantRoomId, created.Invite.RoomId);

        await using var db = NewDb();
        var invite = await db.GatePassInvites.SingleAsync();
        Assert.NotEqual(created.RawToken, invite.TokenHash);
        Assert.Equal(64, invite.TokenHash.Length);
        Assert.Equal(GateIssuerRole.KhachThue, invite.IssuerRole);
        Assert.Equal(TenantUserId, invite.CreatedByUserId);
        Assert.Equal(Now.AddMinutes(1440), invite.ExpiresAt);
    }

    [Fact]
    public async Task Tao_link_that_bai_khi_chu_tro_chua_bat_tu_dien()
    {
        await SeedAsync(GatePolicyDefaults() with { AllowGuestSelfFill = false });

        var result = await Service().CreateAsync(new GateInviteCreateRequest { RoomId = TenantRoomId },
            new PropertyCaller(TenantUserId, Roles.KhachThue));

        Assert.False(result.Succeeded);
        Assert.Equal(GateErrorCodes.GuestFillDisabled, result.ErrorCode);
    }

    [Fact]
    public async Task Tenant_khong_co_hop_dong_hieu_luc_khong_tao_duoc_link()
    {
        await SeedAsync(GatePolicyDefaults(), withContract: false);

        var result = await Service().CreateAsync(new GateInviteCreateRequest { RoomId = TenantRoomId },
            new PropertyCaller(TenantUserId, Roles.KhachThue));

        Assert.False(result.Succeeded);
        Assert.Equal(GateErrorCodes.TenantNoActiveContract, result.ErrorCode);
    }

    [Fact]
    public async Task Quan_ly_tao_link_phai_chon_phong_trong_pham_vi_duoc_giao()
    {
        await SeedAsync(GatePolicyDefaults());

        var outside = await Service().CreateAsync(new GateInviteCreateRequest { RoomId = 20 },
            new PropertyCaller(OwnerId, Roles.QuanLy));
        Assert.False(outside.Succeeded);
        Assert.Equal(GateErrorCodes.Forbidden, outside.ErrorCode);

        var inside = await Service().CreateAsync(new GateInviteCreateRequest { RoomId = SecondRoomId },
            new PropertyCaller(OwnerId, Roles.QuanLy));
        Assert.True(inside.Succeeded);
    }

    [Fact]
    public async Task Peek_tra_ve_null_sau_khi_link_da_dung()
    {
        await SeedAsync(GatePolicyDefaults());
        var created = await CreateTenantInviteAsync();

        Assert.NotNull(await Service().PeekAsync(created.RawToken));

        var submitted = await Service().SubmitAsync(created.RawToken, DayVisit("0901234567"));
        Assert.True(submitted.Succeeded);

        Assert.Null(await Service().PeekAsync(created.RawToken));
    }

    [Fact]
    public async Task Submit_trong_ngay_duoc_tu_duyet_va_thong_bao_chu_tro()
    {
        await SeedAsync(GatePolicyDefaults());
        var created = await CreateTenantInviteAsync();

        var result = await Service().SubmitAsync(created.RawToken, DayVisit("0901234567"));

        Assert.True(result.Succeeded);
        var submitted = Assert.IsType<GuestPassSubmittedDto>(result.Created);
        Assert.Equal(GatePassStatus.Approved, submitted.Status);
        Assert.True(submitted.AutoApproved);
        Assert.False(string.IsNullOrWhiteSpace(submitted.Code));

        await using var db = NewDb();
        var pass = await db.GatePasses.SingleAsync();
        Assert.Equal(GatePassStatus.Approved, pass.Status);
        Assert.True(pass.AutoApproved);
        Assert.Equal(TenantUserId, pass.IssuedByUserId);
        Assert.Equal(created.Invite.Id, pass.GatePassInviteId);
        Assert.Equal(TenantRoomId, pass.RoomId);
        Assert.Equal("0901234567", pass.VisitorPhone);

        var invite = await db.GatePassInvites.SingleAsync();
        Assert.NotNull(invite.UsedAt);
        Assert.Equal(pass.Id, invite.UsedGatePassId);

        Assert.Contains(_notifications.Items, item => item.RecipientUserId == OwnerId);
    }

    [Fact]
    public async Task Submit_qua_dem_luon_cho_duyet_va_chua_tao_bao_luu_tru()
    {
        await SeedAsync(GatePolicyDefaults());
        var created = await CreateTenantInviteAsync();

        var result = await Service().SubmitAsync(created.RawToken, Overnight());

        Assert.True(result.Succeeded);
        var submitted = Assert.IsType<GuestPassSubmittedDto>(result.Created);
        Assert.Equal(GatePassStatus.Pending, submitted.Status);
        Assert.False(submitted.AutoApproved);

        await using var db = NewDb();
        var pass = await db.GatePasses.SingleAsync();
        Assert.Equal(GatePassStatus.Pending, pass.Status);
        Assert.Null(pass.StayReportId);
        Assert.Empty(db.StayReports);
    }

    [Fact]
    public async Task Submit_qua_dem_bi_chan_khi_chu_tro_khong_cho_khach_thue_dat()
    {
        await SeedAsync(GatePolicyDefaults() with { TenantMayIssueOvernight = false });
        var created = await CreateTenantInviteAsync();

        var result = await Service().SubmitAsync(created.RawToken, Overnight());

        Assert.False(result.Succeeded);
        Assert.Equal(GateErrorCodes.TenantOvernightDisabled, result.ErrorCode);
        await using var db = NewDb();
        Assert.Empty(db.GatePasses);
    }

    [Fact]
    public async Task Submit_chan_khi_vuot_quota_tu_dien_theo_so_dien_thoai()
    {
        await SeedAsync(GatePolicyDefaults() with { GuestSelfMaxPassesPerDay = 1 });
        var first = await CreateTenantInviteAsync();
        var second = await CreateTenantInviteAsync();

        Assert.True((await Service().SubmitAsync(first.RawToken, DayVisit("0901234567"))).Succeeded);

        var blocked = await Service().SubmitAsync(second.RawToken, DayVisit("0901234567"));
        Assert.False(blocked.Succeeded);
        Assert.Equal(GateErrorCodes.GuestQuotaExceeded, blocked.ErrorCode);
    }

    [Fact]
    public async Task Submit_chan_phieu_trung_so_dien_thoai_dang_cho_hoac_da_duyet()
    {
        await SeedAsync(GatePolicyDefaults());
        var first = await CreateTenantInviteAsync();
        var second = await CreateTenantInviteAsync();

        Assert.True((await Service().SubmitAsync(first.RawToken, DayVisit("0901234567"))).Succeeded);

        var blocked = await Service().SubmitAsync(second.RawToken, DayVisit("0901234567"));
        Assert.False(blocked.Succeeded);
        Assert.Equal(GateErrorCodes.DuplicateGuestRequest, blocked.ErrorCode);
    }

    [Fact]
    public async Task Link_chi_dung_duoc_mot_lan()
    {
        await SeedAsync(GatePolicyDefaults());
        var created = await CreateTenantInviteAsync();

        Assert.True((await Service().SubmitAsync(created.RawToken, DayVisit("0901234567"))).Succeeded);

        var again = await Service().SubmitAsync(created.RawToken, DayVisit("0909999999"));
        Assert.False(again.Succeeded);
        Assert.Equal(GateErrorCodes.InviteInvalid, again.ErrorCode);
    }

    [Fact]
    public async Task Trang_thai_cong_khai_khong_lo_so_dien_thoai_va_cccd()
    {
        await SeedAsync(GatePolicyDefaults());
        var created = await CreateTenantInviteAsync();
        var submitted = await Service().SubmitAsync(created.RawToken, DayVisit("0901234567"));

        var publicPass = await Service().GetPublicPassAsync(submitted.Created!.RawToken);

        Assert.NotNull(publicPass);
        Assert.Equal("Nguyễn Văn Khách", publicPass.VisitorName);
        Assert.Equal("Nhà A", publicPass.PropertyName);
        Assert.Equal("A101", publicPass.RoomCode);
        Assert.Equal(GatePassStatus.Approved, publicPass.Status);
        Assert.DoesNotContain("0901234567", publicPass.ToString());
    }

    private async Task<GateInviteCreatedDto> CreateTenantInviteAsync(int roomId = TenantRoomId)
    {
        var result = await Service().CreateAsync(new GateInviteCreateRequest { RoomId = roomId },
            new PropertyCaller(TenantUserId, Roles.KhachThue));
        Assert.True(result.Succeeded, result.ErrorCode);
        return result.Created!;
    }

    private static GuestPassSubmission DayVisit(string phone) => new()
    {
        VisitorName = "Nguyễn Văn Khách",
        VisitorPhone = phone,
        StayType = GateStayType.Visit,
        Note = "Đến chơi buổi chiều",
    };

    private static GuestPassSubmission Overnight() => new()
    {
        VisitorName = "Trần Thị Qua Đêm",
        VisitorPhone = "0912345678",
        VisitorIdNumber = "079123456789",
        VisitorDob = new DateOnly(1995, 3, 2),
        StayReason = "Thăm người thân, ở lại một đêm",
        StayType = GateStayType.Overnight,
    };

    private static GatePolicyDto GatePolicyDefaults() => GatePolicyDto.Defaults(PropertyId) with
    {
        AllowGuestSelfFill = true,
        AllowTenantIssuePass = true,
        TenantMayIssueOvernight = true,
        AutoApproveDayVisit = true,
        DayVisitStartHour = 6,
        DayVisitEndHour = 22,
        GuestSelfMaxPassesPerDay = 3,
        TenantMaxPassesPerDay = 5,
        TenantMaxTtlMinutes = 60,
        PassTtlMinutes = 60,
    };

    private GatePassInviteService Service()
    {
        var db = NewDb();
        return new GatePassInviteService(new TestDbContextFactory(_options), _clock,
            new GateTokenServiceStub(), _notifications,
            new AuditLogService(new AuditLogFileStore(_auditRoot), db));
    }

    private ApplicationDbContext NewDb() => new(_options);

    private async Task SeedAsync(GatePolicyDto policy, bool withContract = true)
    {
        await using var db = NewDb();
        db.Properties.AddRange(
            new Property { Id = PropertyId, Name = "Nhà A", OwnerId = OwnerId, Address = "1 Lê Lợi" },
            new Property { Id = OtherPropertyId, Name = "Nhà B", OwnerId = OtherOwnerId });
        db.Rooms.AddRange(
            new Room { Id = TenantRoomId, PropertyId = PropertyId, Code = "A101" },
            new Room { Id = SecondRoomId, PropertyId = PropertyId, Code = "A102" },
            new Room { Id = 20, PropertyId = OtherPropertyId, Code = "B201" });
        // Quản lý chỉ được giao nhà A.
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = PropertyId,
            ManagerUserId = OwnerId,
            AssignedByUserId = OwnerId,
        });

        if (withContract)
        {
            var contract = new Contract
            {
                ContractId = Guid.NewGuid(),
                ContractNumber = "HD-01",
                RoomId = TenantRoomId,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                Status = ContractStatus.Approved,
            };
            db.Contracts.Add(contract);
            db.Tenants.Add(new Tenant { Id = 100, FullName = "Lê Thị Thuê", Phone = "0888888888", UserId = TenantUserId });
            db.ContractMembers.Add(new ContractMember { ContractId = contract.ContractId, TenantId = 100, IsRepresentative = true });
        }

        db.GatePolicies.Add(new GatePolicy
        {
            PropertyId = PropertyId,
            AllowGuestSelfFill = policy.AllowGuestSelfFill,
            AllowTenantIssuePass = policy.AllowTenantIssuePass,
            TenantMayIssueOvernight = policy.TenantMayIssueOvernight,
            AutoApproveDayVisit = policy.AutoApproveDayVisit,
            DayVisitStartHour = policy.DayVisitStartHour,
            DayVisitEndHour = policy.DayVisitEndHour,
            GuestSelfMaxPassesPerDay = policy.GuestSelfMaxPassesPerDay,
            TenantMaxPassesPerDay = policy.TenantMaxPassesPerDay,
            TenantMaxTtlMinutes = policy.TenantMaxTtlMinutes,
            PassTtlMinutes = policy.PassTtlMinutes,
            TenantMaxUses = 1,
            UpdatedAt = Now,
        });
        await db.SaveChangesAsync();
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }

    private sealed class GateTokenServiceStub : IGateTokenService
    {
        private readonly GateTokenService _inner = new(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());

        public string NewToken() => _inner.NewToken();
        public string NewCode() => _inner.NewCode();
        public string Hash(string value) => _inner.Hash(value);
        public bool FixedTimeEquals(string a, string b) => _inner.FixedTimeEquals(a, b);
        public int CurrentWindow(DateTimeOffset at) => _inner.CurrentWindow(at);
        public string IssueResidentToken(string userId, int propertyId, DateTimeOffset at) =>
            _inner.IssueResidentToken(userId, propertyId, at);
        public bool TryVerifyResidentToken(string token, DateTimeOffset at, out string userId, out int propertyId) =>
            _inner.TryVerifyResidentToken(token, at, out userId, out propertyId);
    }

    private sealed class FakeNotificationService : INotificationService
    {
        public List<(string RecipientUserId, string Title, string Body, string? Link)> Items { get; } = [];

        public Task NotifyAsync(string recipientUserId, string title, string body, string? link = null)
        {
            Items.Add((recipientUserId, title, body, link));
            return Task.CompletedTask;
        }

        public Task<int> UnreadCountAsync(string userId) => Task.FromResult(0);
        public Task<IReadOnlyList<AppNotification>> ListAsync(string userId, int take = 30) =>
            Task.FromResult<IReadOnlyList<AppNotification>>([]);
        public Task MarkReadAsync(string userId, int notificationId) => Task.CompletedTask;
        public Task MarkAllReadAsync(string userId) => Task.CompletedTask;
    }
}
