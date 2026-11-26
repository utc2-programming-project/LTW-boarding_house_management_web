using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Web.Features.Accounts.Sessions;

/// <summary>
/// PLAN-001 #10: danh sách phiên đăng nhập lưu theo file JSON mỗi tài khoản
/// (không cần thêm bảng/migration). Mỗi cookie nhatro.sid là một phiên;
/// thu hồi = xoá khỏi danh sách, middleware sẽ đăng xuất cookie xác thực tương ứng.
/// </summary>
public sealed class UserSessionStore
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> FileLocks = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _root;
    private readonly TimeProvider _clock;

    public UserSessionStore(IConfiguration? configuration = null, TimeProvider? clock = null)
    {
        var configured = configuration?["Security:SessionsPath"];
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NhaTro",
                "sessions")
            : Environment.ExpandEnvironmentVariables(configured);
        _root = path;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<(string SessionId, bool IsNewDevice)> CreateOrReuseAsync(
        string userId,
        string? existingSessionId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var (result, _) = await WithUserAsync(userId, list =>
        {
            if (!string.IsNullOrWhiteSpace(existingSessionId))
            {
                var existing = list.FirstOrDefault(s => s.SessionId == existingSessionId);
                if (existing is not null)
                {
                    list.Remove(existing);
                    list.Add(existing with
                    {
                        LastSeenAt = now,
                        IpAddress = ipAddress,
                        UserAgent = userAgent,
                    });
                    return ((existing.SessionId, false), true);
                }
            }

            // Lần đăng nhập đầu tiên không phải "thiết bị lạ"; chỉ báo khi đã có phiên trước đó.
            var isNewDevice = list.Count > 0
                && !list.Any(s => Same(s.IpAddress, ipAddress) && Same(s.UserAgent, userAgent));
            var session = new UserSessionInfo(NewId(), userId, ipAddress, userAgent, now, now);
            list.Add(session);
            return ((session.SessionId, isNewDevice), true);
        }, ct);

        return result;
    }

    public async Task<IReadOnlyList<UserSessionInfo>> ListAsync(string userId, CancellationToken ct = default)
    {
        var (rows, _) = await WithUserAsync(userId, list =>
            (list.OrderByDescending(s => s.LastSeenAt).ToList(), false), ct);
        return rows;
    }

    public async Task<bool> IsActiveAsync(string userId, string sessionId, CancellationToken ct = default)
    {
        var (active, _) = await WithUserAsync(userId, list =>
            (list.Any(s => s.SessionId == sessionId), false), ct);
        return active;
    }

    public Task TouchAsync(string userId, string sessionId, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        return WithUserAsync(userId, list =>
        {
            var index = list.FindIndex(s => s.SessionId == sessionId);
            if (index < 0)
            {
                return (true, false);
            }

            list[index] = list[index] with { LastSeenAt = now };
            return (true, true);
        }, ct);
    }

    public Task RevokeAsync(string userId, string sessionId, CancellationToken ct = default) =>
        WithUserAsync(userId, list =>
        {
            var removed = list.RemoveAll(s => s.SessionId == sessionId) > 0;
            return (removed, removed);
        }, ct);

    public Task RevokeAllAsync(string userId, CancellationToken ct = default) =>
        WithUserAsync(userId, list =>
        {
            var changed = list.Count > 0;
            list.Clear();
            return (changed, changed);
        }, ct);

    public Task RevokeOthersAsync(string userId, string? keepSessionId, CancellationToken ct = default) =>
        WithUserAsync(userId, list =>
        {
            var removed = list.RemoveAll(s => !Same(s.SessionId, keepSessionId)) > 0;
            return (removed, removed);
        }, ct);

    private async Task<(T Result, bool Changed)> WithUserAsync<T>(
        string userId, Func<List<UserSessionInfo>, (T Result, bool Changed)> mutate, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (default!, false);
        }

        var path = PathFor(userId);
        var gate = FileLocks.GetOrAdd(path, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(path, ct);
            var before = list.Count;
            var (result, changed) = mutate(list);

            var pruned = Prune(list);
            if (changed || pruned.Count != before)
            {
                await SaveAsync(path, pruned, ct);
            }

            return (result, changed || pruned.Count != before);
        }
        finally
        {
            gate.Release();
        }
    }

    private List<UserSessionInfo> Prune(List<UserSessionInfo> list)
    {
        var cutoff = _clock.GetUtcNow().AddDays(-90);
        return list
            .Where(s => s.LastSeenAt >= cutoff)
            .OrderByDescending(s => s.LastSeenAt)
            .Take(20)
            .ToList();
    }

    private async Task<List<UserSessionInfo>> LoadAsync(string path, CancellationToken ct)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<UserSessionInfo>>(stream, JsonOptions, ct)
                ?? [];
        }
        catch (JsonException)
        {
            // File hỏng: coi như chưa có phiên, lần ghi sau sẽ thay thế.
            return [];
        }
    }

    private async Task SaveAsync(string path, List<UserSessionInfo> sessions, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        var temp = path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, sessions, JsonOptions, ct);
        }

        File.Move(temp, path, overwrite: true);
    }

    private string PathFor(string userId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId))).ToLowerInvariant();
        return Path.Combine(_root, hash[..32] + ".json");
    }

    private static string NewId() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static bool Same(string? a, string? b) =>
        string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.Ordinal);
}
