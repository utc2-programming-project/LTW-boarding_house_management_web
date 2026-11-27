using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.AccessControl.Enums;
using Web.Features.AccessControl.Entities;
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

/// <summary>Khách thuê thấy phiếu cấp cho phòng mình đang ở, kể cả phiếu do Chủ trọ/Quản lý tạo.</summary>
public sealed class GateTenantScopeTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private const string TenantUserId = "tenant-user-1";
    private const int PropertyId = 1;
    private const int MyRoomId = 10;
    private const int OtherRoomId = 11;
    private const int TenantId = 5;

    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.FromHours(7));

    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly string _auditRoot = Path.Combine(Path.GetTempPath(), "NhaTro_GateTenantScopeTests", Guid.NewGuid().ToString("N"));

    public GateTenantScopeTests()
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
    public async Task ListAsync_hien_phieu_chu_tro_cap_cho_phong_minh()
    {
        await SeedAsync(AddContract: true,
            Pass(1, "owner cấp phòng tôi", MyRoomId, OwnerId),
            Pass(2, "owner cấp phòng khác", OtherRoomId, OwnerId),
            Pass(3, "tôi tự tạo cho phòng khác", OtherRoomId, TenantUserId));

        var rows = await Service().ListAsync(new PropertyCaller(TenantUserId, Roles.KhachThue));

        Assert.Contains(rows, row => row.VisitorName == "owner cấp phòng tôi");
        Assert.Contains(rows, row => row.VisitorName == "tôi tự tạo cho phòng khác");
        Assert.DoesNotContain(rows, row => row.VisitorName == "owner cấp phòng khác");
    }

    [Fact]
    public async Task ListAsync_khach_thue_chi_thu_hoi_phieu_minh_tao()
    {
        await SeedAsync(AddContract: true,
            Pass(1, "owner cấp", MyRoomId, OwnerId),
            Pass(2, "tự tạo", OtherRoomId, TenantUserId));

        var rows = await Service().ListAsync(new PropertyCaller(TenantUserId, Roles.KhachThue));

        Assert.False(rows.Single(row => row.VisitorName == "owner cấp").CanRevoke);
        Assert.True(rows.Single(row => row.VisitorName == "tự tạo").CanRevoke);
    }

    [Fact]
    public async Task ListAsync_khong_hien_phieu_phong_cu_khi_hop_dong_het_han()
    {
        await SeedAsync(AddContract: false,
            Pass(1, "owner cấp phòng tôi", MyRoomId, OwnerId));

        var rows = await Service().ListAsync(new PropertyCaller(TenantUserId, Roles.KhachThue));

        Assert.DoesNotContain(rows, row => row.VisitorName == "owner cấp phòng tôi");
    }

    private GatePassService Service()
    {
        var db = NewDb();
        return new GatePassService(new TestDbContextFactory(_options), _clock,
            new GateTokenServiceStub(), new NoopNotificationService(),
            new AuditLogService(new AuditLogFileStore(_auditRoot), db),
            Options.Create(new GateRetentionOptions { RetentionDays = 30, CleanupIntervalHours = 24 }));
    }

    private ApplicationDbContext NewDb() => new(_options);

    private static GatePass Pass(int id, string name, int roomId, string issuedBy) => new()
    {
        Id = id,
        PropertyId = PropertyId,
        RoomId = roomId,
        PassType = GatePassType.Guest,
        StayType = GateStayType.Visit,
        TokenHash = $"hash-{id}",
        VisitorName = name,
        Status = GatePassStatus.Pending,
        IssuerRole = issuedBy == OwnerId ? GateIssuerRole.ChuTro : GateIssuerRole.KhachThue,
        IssuedByUserId = issuedBy,
        ExpiresAt = Now.AddHours(2),
        CreatedAt = Now,
    };

    private async Task SeedAsync(bool AddContract, params GatePass[] passes)
    {
        await using var db = NewDb();
        db.Properties.Add(new Property { Id = PropertyId, Name = "Nhà A", OwnerId = OwnerId, Address = "1 Lê Lợi" });
        db.Rooms.AddRange(
            new Room { Id = MyRoomId, PropertyId = PropertyId, Code = "A1" },
            new Room { Id = OtherRoomId, PropertyId = PropertyId, Code = "A2" });
        db.Tenants.Add(new Tenant { Id = TenantId, FullName = "Khách A", Phone = "0900000000", UserId = TenantUserId });

        if (AddContract)
        {
            var contractId = Guid.NewGuid();
            db.Contracts.Add(new Contract
            {
                ContractId = contractId,
                ContractNumber = "HD-1",
                RoomId = MyRoomId,
                StartDate = Now.UtcDateTime.AddDays(-10),
                EndDate = Now.UtcDateTime.AddDays(60),
                Status = ContractStatus.Approved,
            });
            db.ContractMembers.Add(new ContractMember { ContractId = contractId, TenantId = TenantId, IsRepresentative = true });
        }

        db.GatePasses.AddRange(passes);
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

    private sealed class NoopNotificationService : INotificationService
    {
        public Task NotifyAsync(string recipientUserId, string title, string body, string? link = null) => Task.CompletedTask;
        public Task<int> UnreadCountAsync(string userId) => Task.FromResult(0);
        public Task<IReadOnlyList<AppNotification>> ListAsync(string userId, int take = 30) =>
            Task.FromResult<IReadOnlyList<AppNotification>>([]);
        public Task MarkReadAsync(string userId, int notificationId) => Task.CompletedTask;
        public Task MarkAllReadAsync(string userId) => Task.CompletedTask;
    }
}
