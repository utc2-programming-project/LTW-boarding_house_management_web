using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;

namespace Web.Infrastructure.Security;

/// <summary>
/// Bộ đếm trong bộ nhớ cho đăng nhập sai. Cửa sổ đếm và ngưỡng chặn lấy từ
/// <see cref="LoginGuardOptions"/> nên chỉnh được qua appsettings mà không sửa code.
/// </summary>
public sealed class LoginAttemptGuard(IOptions<SecurityOptions> options, TimeProvider clock)
    : ILoginAttemptGuard
{
    private readonly LoginGuardOptions _options = options.Value.LoginGuard;
    private readonly ConcurrentDictionary<string, Counter> _accountFailures =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Counter> _ipFailures =
        new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _ipBlocks =
        new(StringComparer.Ordinal);

    private int _writeCount;

    public TimeSpan? GetIpBlockRemaining(string ip)
    {
        if (string.IsNullOrWhiteSpace(ip) || !_ipBlocks.TryGetValue(ip, out var blockedUntil))
        {
            return null;
        }

        var remaining = blockedUntil - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero)
        {
            _ipBlocks.TryRemove(ip, out _);
            return null;
        }

        return remaining;
    }

    public int GetAccountFailures(string accountKey)
    {
        if (string.IsNullOrWhiteSpace(accountKey) || !_accountFailures.TryGetValue(accountKey, out var counter))
        {
            return 0;
        }

        if (IsExpired(counter))
        {
            _accountFailures.TryRemove(accountKey, out _);
            return 0;
        }

        return counter.Count;
    }

    public int RegisterFailure(string ip, string accountKey)
    {
        var now = clock.GetUtcNow();
        var accountCount = 0;

        if (!string.IsNullOrWhiteSpace(accountKey))
        {
            accountCount = Increment(_accountFailures, accountKey, now);
        }

        if (!string.IsNullOrWhiteSpace(ip))
        {
            var ipCount = Increment(_ipFailures, ip, now);
            if (ipCount >= Math.Max(1, _options.IpBlockAfterFailedAttempts))
            {
                _ipBlocks[ip] = now.AddMinutes(Math.Max(1, _options.IpBlockMinutes));
                _ipFailures.TryRemove(ip, out _);
            }
        }

        PruneOccasionally(now);
        return accountCount;
    }

    public void RegisterSuccess(string ip, string accountKey)
    {
        if (!string.IsNullOrWhiteSpace(accountKey))
        {
            _accountFailures.TryRemove(accountKey, out _);
        }
    }

    private int Increment(ConcurrentDictionary<string, Counter> counters, string key, DateTimeOffset now)
    {
        var window = Window;
        var counter = counters.AddOrUpdate(
            key,
            _ => new Counter(1, now),
            (_, existing) => existing.WindowStart + window <= now
                ? new Counter(1, now)
                : existing with { Count = existing.Count + 1 });

        return counter.Count;
    }

    private bool IsExpired(Counter counter) => counter.WindowStart + Window <= clock.GetUtcNow();

    private TimeSpan Window => TimeSpan.FromMinutes(Math.Max(1, _options.AttemptWindowMinutes));

    /// <summary>Dọn bộ đếm hết hạn định kỳ để IP lạ không làm phình bộ nhớ.</summary>
    private void PruneOccasionally(DateTimeOffset now)
    {
        if (Interlocked.Increment(ref _writeCount) % 512 != 0)
        {
            return;
        }

        foreach (var pair in _ipBlocks)
        {
            if (pair.Value <= now)
            {
                _ipBlocks.TryRemove(pair.Key, out _);
            }
        }

        foreach (var pair in _ipFailures)
        {
            if (pair.Value.WindowStart + Window <= now)
            {
                _ipFailures.TryRemove(pair.Key, out _);
            }
        }
    }

    private readonly record struct Counter(int Count, DateTimeOffset WindowStart);
}
