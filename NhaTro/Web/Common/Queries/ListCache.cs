using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Web.Common.Queries;

/// <summary>
/// Bọc IMemoryCache cho reference data/snapshot nhỏ. Không dùng cho list nghiệp vụ per-user.
/// Mỗi key có một cổng riêng để nhiều request đồng thời không cùng chạy factory (chống stampede).
/// </summary>
public static class ListCache
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    public static async Task<T?> GetOrCreateAsync<T>(
        IMemoryCache cache,
        string key,
        TimeSpan ttl,
        Func<CancellationToken, Task<T?>> factory,
        CancellationToken cancellationToken = default)
        where T : class
    {
        if (cache.TryGetValue(key, out T? cached))
        {
            return cached;
        }

        var gate = Gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue(key, out cached))
            {
                return cached;
            }

            var value = await factory(cancellationToken);
            if (value is not null)
            {
                cache.Set(key, value, ttl);
            }

            return value;
        }
        finally
        {
            gate.Release();
            if (gate.CurrentCount == 1)
            {
                Gates.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, gate));
            }
        }
    }
}
