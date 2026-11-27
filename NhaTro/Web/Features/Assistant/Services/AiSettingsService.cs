using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Assistant.DTOs;
using Web.Features.Assistant.Entities;
using Web.Features.Properties;
using Web.Infrastructure.Time;

namespace Web.Features.Assistant.Services;

public sealed record AiAccess(
    bool Allowed,
    bool Enabled,
    int DailyLimit,
    IReadOnlyList<string> OwnerIds,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AiAccess Deny(string errorCode, string errorMessage) =>
        new(false, false, 0, [], errorCode, errorMessage);
}

/// <summary>
/// Đọc/ghi cài đặt AI theo Chủ trọ và quy đổi ra quyền dùng cho người gọi.
/// Quản lý chịu cài đặt của (các) Chủ trọ sở hữu tòa được phân công: tất cả phải bật,
/// hạn mức lấy mức nhỏ nhất để không vượt chi phí của bất kỳ nhà nào.
/// </summary>
public sealed class AiSettingsService(
    ApplicationDbContext db,
    TimeProvider clock,
    IOptions<AiOptions> options)
{
    public async Task<AiAccess> ResolveAccessAsync(
        PropertyCaller caller, int? propertyId, CancellationToken ct = default)
    {
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            return AiAccess.Deny(AiErrorCodes.Forbidden, "Vai trò hiện tại không dùng được trợ lý AI.");
        }

        if (caller.IsChuTro)
        {
            if (propertyId is { } ownedId)
            {
                var ownerId = await db.Properties
                    .AsNoTracking()
                    .Where(property => property.Id == ownedId)
                    .Select(property => property.OwnerId)
                    .FirstOrDefaultAsync(ct);
                if (ownerId != caller.UserId)
                {
                    return AiAccess.Deny(AiErrorCodes.Forbidden, "Tòa nhà ngoài phạm vi của bạn.");
                }
            }

            return await BuildAccessAsync([caller.UserId], ct);
        }

        var ownerIds = await (
                from assignment in db.PropertyManagers.AsNoTracking()
                join property in db.Properties.AsNoTracking() on assignment.PropertyId equals property.Id
                where assignment.ManagerUserId == caller.UserId
                    && assignment.RevokedAt == null
                    && (propertyId == null || property.Id == propertyId)
                select property.OwnerId)
            .Distinct()
            .ToListAsync(ct);

        if (ownerIds.Count == 0)
        {
            return AiAccess.Deny(AiErrorCodes.Forbidden, "Bạn chưa được phân công tòa nhà nào.");
        }

        return await BuildAccessAsync(ownerIds, ct);
    }

    public async Task<AiSettingsDto> GetAsync(string ownerUserId, CancellationToken ct = default)
    {
        var settings = await db.AiAssistantSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.OwnerUserId == ownerUserId, ct);

        return new AiSettingsDto(
            settings?.Enabled ?? true,
            settings?.DailyRequestLimit ?? options.Value.DefaultDailyLimit,
            await CountTodayAsync(ownerUserId, ct),
            options.Value.DefaultDailyLimit,
            options.Value.Provider,
            options.Value.Model,
            string.Equals(options.Value.Provider, "Demo", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(options.Value.BaseUrl));
    }

    public async Task<AiSettingsResult> SaveAsync(
        string ownerUserId, bool enabled, int dailyLimit, CancellationToken ct = default)
    {
        if (dailyLimit is < 1 or > 1000)
        {
            return AiSettingsResult.Failure(
                AiErrorCodes.InvalidRequest, "Hạn mức mỗi ngày phải trong khoảng 1–1000.");
        }

        var settings = await db.AiAssistantSettings
            .FirstOrDefaultAsync(row => row.OwnerUserId == ownerUserId, ct);
        if (settings is null)
        {
            settings = new AiAssistantSettings { OwnerUserId = ownerUserId };
            db.AiAssistantSettings.Add(settings);
        }

        settings.Enabled = enabled;
        settings.DailyRequestLimit = dailyLimit;
        settings.UpdatedAt = clock.GetUtcNow();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AiSettingsResult.Failure(
                AiErrorCodes.Conflict, "Cài đặt đã bị thay đổi ở nơi khác. Vui lòng tải lại.");
        }

        return AiSettingsResult.Success(await GetAsync(ownerUserId, ct));
    }

    public async Task<int> CountTodayAsync(string userId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(VietnamTime.ToVietnam(clock.GetUtcNow().UtcDateTime));
        var (start, end) = VietnamTime.DayRangeUtc(today);
        return await db.AiUsageLogs
            .AsNoTracking()
            .CountAsync(log => log.UserId == userId && log.CreatedAt >= start && log.CreatedAt < end, ct);
    }

    private async Task<AiAccess> BuildAccessAsync(
        IReadOnlyList<string> ownerIds, CancellationToken ct)
    {
        var rows = await db.AiAssistantSettings
            .AsNoTracking()
            .Where(row => ownerIds.Contains(row.OwnerUserId))
            .ToListAsync(ct);

        var enabled = ownerIds.All(ownerId =>
            rows.FirstOrDefault(row => row.OwnerUserId == ownerId)?.Enabled ?? true);
        var limit = ownerIds.Min(ownerId =>
            rows.FirstOrDefault(row => row.OwnerUserId == ownerId)?.DailyRequestLimit
            ?? options.Value.DefaultDailyLimit);

        return new AiAccess(true, enabled, Math.Max(1, limit), ownerIds, null, null);
    }
}
