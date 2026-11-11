using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using Web.Features.Properties.DTOs;

namespace Web.Features.Properties.Services;

/// <summary>
/// Đọc/ghi bản nháp thiết lập nhà trọ ở local của người dùng.
/// Key tách theo user để hai tài khoản dùng chung trình duyệt không thấy nháp của nhau.
/// </summary>
public sealed class PropertySetupDraftStore(ProtectedLocalStorage storage, TimeProvider? timeProvider = null)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    private const string KeyPrefix = "nhatro.property-setup.v1";
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;

    /// <summary>Chỉ gọi được sau render đầu (interactive). Trả null nếu chưa có/hết hạn/không đọc được.</summary>
    public async Task<PropertySetupDraft?> LoadAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        try
        {
            var stored = await storage.GetAsync<string>(BuildKey(userId));
            if (!stored.Success || string.IsNullOrWhiteSpace(stored.Value)) return null;

            var draft = PropertySetupDraftCodec.Decode(stored.Value, _clock.GetUtcNow());
            if (draft is null) await ClearAsync(userId);
            return draft;
        }
        catch (InvalidOperationException)
        {
            // Prerender hoặc circuit chưa sẵn sàng cho JS interop.
            return null;
        }
        catch (JSException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(string userId, PropertySetupDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        if (string.IsNullOrWhiteSpace(userId)) return;

        draft.Version = PropertySetupDraft.CurrentVersion;
        draft.UpdatedAt = _clock.GetUtcNow();

        try
        {
            await storage.SetAsync(BuildKey(userId), PropertySetupDraftCodec.Encode(draft));
        }
        catch (InvalidOperationException)
        {
            // Không có JS interop: bỏ qua, dữ liệu đã lưu thật vẫn an toàn.
        }
        catch (JSException)
        {
        }
    }

    public async Task ClearAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;

        try
        {
            await storage.DeleteAsync(BuildKey(userId));
        }
        catch (InvalidOperationException)
        {
        }
        catch (JSException)
        {
        }
    }

    public static string BuildKey(string userId) => $"{KeyPrefix}.{userId}";
}

/// <summary>Phần thuần (không JS) để test được version + thời hạn nháp.</summary>
public static class PropertySetupDraftCodec
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Encode(PropertySetupDraft draft) => JsonSerializer.Serialize(draft, Options);

    public static PropertySetupDraft? Decode(string? json, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        var draft = JsonSerializer.Deserialize<PropertySetupDraft>(json, Options);
        if (draft is null || draft.Version != PropertySetupDraft.CurrentVersion) return null;
        if (draft.UpdatedAt < now - PropertySetupDraftStore.Lifetime) return null;

        // Đồng hồ máy người dùng lệch về tương lai: kẹp lại để TTL còn hiệu lực.
        if (draft.UpdatedAt > now) draft.UpdatedAt = now;
        return draft;
    }
}
