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
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Infrastructure.Auditing;

namespace NhaTro.UnitTests.Features.AccessControl;

/// <summary>Hạn lưu dữ liệu cổng 30 ngày: danh sách phiếu ẩn phiếu hết hạn quá lâu.</summary>
public sealed class GateRetentionTests : IDisposable
{
    private const string OwnerId = "owner-1";
    private const int PropertyId = 1;

    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.FromHours(7));

    private readonly DbContextOptions<ApplicationDbContext> _options;
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly string _auditRoot = Path.Combine(Path.GetTempPath(), "NhaTro_GateRetentionTests", Guid.NewGuid().ToString("N"));

    public GateRetentionTests()
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
    public async Task ListAsync_an_phieu_het_han_qua_30_ngay()
    {
        await SeedAsync(
            Pass(1, "Phiếu cũ", Now.AddDays(-40)),
            Pass(2, "Phiếu mới", Now.AddDays(-1)));

        var rows = await Service(Retention(30)).ListAsync(new PropertyCaller(OwnerId, Roles.ChuTro));

        Assert.DoesNotContain(rows, row => row.VisitorName == "Phiếu cũ");
        Assert.Contains(rows, row => row.VisitorName == "Phiếu mới");
    }

    [Fact]
    public async Task ListAsync_van_hien_phieu_con_hieu_luc_du_tao_truoc_30_ngay()
    {
        var old = Pass(1, "Phiếu dài hạn", Now.AddDays(10));
        old.CreatedAt = Now.AddDays(-60);
        await SeedAsync(old);

        var rows = await Service(Retention(30)).ListAsync(new PropertyCaller(OwnerId, Roles.ChuTro));

        Assert.Contains(rows, row => row.VisitorName == "Phiếu dài hạn");
    }

    [Fact]
    public async Task ListAsync_van_hien_phieu_het_han_trong_30_ngay()
    {
        await SeedAsync(Pass(1, "Hết hạn gần đây", Now.AddDays(-10)));

        var rows = await Service(Retention(30)).ListAsync(new PropertyCaller(OwnerId, Roles.ChuTro));

        Assert.Contains(rows, row => row.VisitorName == "Hết hạn gần đây");
    }

    private static GateRetentionOptions Retention(int days) => new() { RetentionDays = days, CleanupIntervalHours = 24 };

    private GatePassService Service(GateRetentionOptions retention)
    {
        var db = NewDb();
        return new GatePassService(new TestDbContextFactory(_options), _clock,
            new GateTokenServiceStub(), new NoopNotificationService(),
            new AuditLogService(new AuditLogFileStore(_auditRoot), db), Options.Create(retention));
    }

    private ApplicationDbContext NewDb() => new(_options);

    private static GatePass Pass(int id, string name, DateTimeOffset expiresAt) => new()
    {
        Id = id,
        PropertyId = PropertyId,
        PassType = GatePassType.Guest,
        StayType = GateStayType.Visit,
        TokenHash = $"hash-{id}",
        VisitorName = name,
        Status = GatePassStatus.Expired,
        IssuerRole = GateIssuerRole.ChuTro,
        IssuedByUserId = OwnerId,
        ExpiresAt = expiresAt,
        CreatedAt = expiresAt.AddHours(-1),
    };

    private async Task SeedAsync(params GatePass[] passes)
    {
        await using var db = NewDb();
        db.Properties.Add(new Property { Id = PropertyId, Name = "Nhà A", OwnerId = OwnerId, Address = "1 Lê Lợi" });
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
