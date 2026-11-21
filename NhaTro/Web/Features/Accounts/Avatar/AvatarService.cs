using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Storage.Rules;

namespace Web.Features.Accounts.Avatar;

public enum AvatarError
{
    InvalidType,
    TooLarge,
}

/// <summary>
/// PLAN-030 bổ sung: ảnh đại diện cá nhân lưu ở server để đồng bộ mọi thiết bị.
/// Ảnh đã được trình duyệt nén về 256px; server nhận diện bằng magic bytes qua
/// <see cref="FileContentSniffer"/>, không tin MIME do client gửi lên.
/// </summary>
public sealed class AvatarService(ApplicationDbContext db, TimeProvider? timeProvider = null)
{
    // Trần cứng phía server, chỉ chặn client nộp ảnh thô thay vì ảnh đã nén.
    public const int MaxBytes = 256 * 1024;

    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<UserAvatar?> FindAsync(string userId, CancellationToken ct = default) =>
        await db.UserAvatars.FirstOrDefaultAsync(avatar => avatar.UserId == userId, ct);

    /// <summary>Loại + thời điểm cập nhật, không đọc byte — dùng để biết có ảnh và làm mới URL.</summary>
    public async Task<(string ContentType, DateTimeOffset UpdatedAt)?> GetInfoAsync(
        string userId, CancellationToken ct = default)
    {
        var info = await db.UserAvatars
            .AsNoTracking()
            .Where(avatar => avatar.UserId == userId)
            .Select(avatar => ValueTuple.Create(avatar.ContentType, avatar.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        return string.IsNullOrEmpty(info.Item1) ? null : info;
    }

    /// <summary>Nạp ảnh mới cho chính tài khoản. Trả <c>null</c> khi hợp lệ.</summary>
    public async Task<AvatarError?> SaveAsync(string userId, byte[] data, CancellationToken ct = default)
    {
        var contentType = FileContentSniffer.DetectContentType(
            data.AsSpan(0, Math.Min(data.Length, FileContentSniffer.RequiredPrefixLength)));

        if (data.Length == 0 || contentType is null || Array.IndexOf(AllowedImageTypes, contentType) < 0)
        {
            return AvatarError.InvalidType;
        }

        if (data.Length > MaxBytes)
        {
            return AvatarError.TooLarge;
        }

        var avatar = await db.UserAvatars.FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (avatar is null)
        {
            avatar = new UserAvatar { UserId = userId };
            db.UserAvatars.Add(avatar);
        }

        avatar.Data = data;
        avatar.ContentType = contentType;
        avatar.UpdatedAt = _timeProvider.GetUtcNow();

        await db.SaveChangesAsync(ct);
        return null;
    }

    public async Task RemoveAsync(string userId, CancellationToken ct = default)
    {
        var avatar = await db.UserAvatars.FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (avatar is null)
        {
            return;
        }

        db.UserAvatars.Remove(avatar);
        await db.SaveChangesAsync(ct);
    }
}
