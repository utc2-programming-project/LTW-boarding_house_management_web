using System.Collections.Concurrent;

namespace Web.Features.Vehicles.Services;

/// <summary>
/// Chống spam thông báo "khu đỗ gần đầy": mỗi cặp (khu, ngưỡng) chỉ báo tối đa một lần
/// trong khoảng <see cref="Cooldown"/> để tránh dội thông báo khi thêm nhiều xe liên tiếp.
/// </summary>
internal static class ZoneNearFullThrottle
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromHours(6);
    private static readonly ConcurrentDictionary<(int ZoneId, int Threshold), DateTimeOffset> LastSentAt = new();

    public static bool TryAcquire(int zoneId, int thresholdPercent, DateTimeOffset now)
    {
        var key = (zoneId, thresholdPercent);
        if (LastSentAt.TryGetValue(key, out var lastSent) && now - lastSent < Cooldown)
        {
            return false;
        }

        LastSentAt[key] = now;
        return true;
    }
}