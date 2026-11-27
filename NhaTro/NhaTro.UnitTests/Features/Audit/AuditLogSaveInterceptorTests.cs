using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Infrastructure.Auditing;

namespace NhaTro.UnitTests.Features.Audit;

public class AuditLogSaveInterceptorTests
{
    [Fact]
    public async Task Ghi_nhat_ky_ra_file_khi_SaveChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "nhatro-audit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new AuditLogFileStore(root, 3);
            var interceptor = new AuditLogSaveInterceptor(store, NullLogger<AuditLogSaveInterceptor>.Instance);
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("audit-" + Guid.NewGuid())
                .AddInterceptors(interceptor)
                .Options;

            await using var db = new ApplicationDbContext(options);
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                Category = AuditLogCategory.Business,
                Action = "TestAction",
                ActorUserId = "user-1",
                Metadata = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync();

            var line = await File.ReadAllTextAsync(Path.Combine(root, "owners", "_unscoped.jsonl"));
            Assert.Contains("TestAction", line);
            Assert.Empty(db.AuditLogEntries);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
