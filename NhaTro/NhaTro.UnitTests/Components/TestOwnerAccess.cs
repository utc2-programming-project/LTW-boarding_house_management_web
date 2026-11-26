using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Data;
using Web.Features.Accounts.Owners;

namespace NhaTro.UnitTests.Components;

/// <summary>
/// Guard cho test component: chạy trên InMemory rỗng nên không có chủ trọ bị khoá;
/// UserManager chỉ dùng ở nhánh cascade lock (không gọi trong test UI).
/// </summary>
internal static class TestOwnerAccess
{
    public static OwnerAccessGuard Create() =>
        new(new InMemoryFactory(), null!, NullLogger<OwnerAccessGuard>.Instance);

    private sealed class InMemoryFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> _options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase($"owner-guard-{Guid.NewGuid():N}")
                .Options;

        public ApplicationDbContext CreateDbContext() => new(_options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
