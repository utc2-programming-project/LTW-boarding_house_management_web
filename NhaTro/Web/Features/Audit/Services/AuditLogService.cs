using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Audit.DTOs;
using Web.Features.Audit.Entities;
using Web.Features.Properties;
using Web.Infrastructure.Auditing;

namespace Web.Features.Audit.Services;

public static class AuditLogErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string OutsideScope = nameof(OutsideScope);
}

public sealed class AuditLogAuthorizationException(string message, string errorCode)
    : UnauthorizedAccessException(message)
{
    public string ErrorCode { get; } = errorCode;
}

/// <summary>
/// D16/VHT-104 — hai sổ tách biệt lưu trong file: Admin chỉ đọc sổ hệ thống/tài khoản,
/// Chủ trọ chỉ đọc sổ nghiệp vụ của mình. Quản lý và Khách thuê không có quyền đọc nhật ký.
/// </summary>
public sealed class AuditLogService(AuditLogFileStore store, ApplicationDbContext db)
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    public async Task<AuditLogPageDto> QueryAsync(
        PropertyCaller caller,
        AuditLogQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        query ??= new AuditLogQuery();

        IReadOnlyList<AuditLogFileEntry> rows;
        HashSet<int>? ownedPropertyIds = null;

        if (caller.IsAdmin)
        {
            rows = await store.ReadAsync(AuditLogCategory.Account, ownerId: null, cancellationToken);
        }
        else if (caller.IsChuTro)
        {
            ownedPropertyIds = await OwnedPropertyIdsAsync(caller, cancellationToken);

            if (query.PropertyId is { } requestedPropertyId && !ownedPropertyIds.Contains(requestedPropertyId))
            {
                throw new AuditLogAuthorizationException(
                    "Không được xem nhật ký ngoài phạm vi tòa nhà của mình.",
                    AuditLogErrorCodes.OutsideScope);
            }

            rows = await store.ReadAsync(AuditLogCategory.Business, caller.UserId, cancellationToken);
        }
        else
        {
            throw new AuditLogAuthorizationException(
                "Chỉ Admin hoặc Chủ trọ được xem nhật ký thao tác.",
                AuditLogErrorCodes.Forbidden);
        }

        var filtered = rows.AsEnumerable();

        if (ownedPropertyIds is not null)
        {
            // Phòng trường hợp quyền sở hữu đã đổi: chỉ trả dòng thuộc tòa nhà hiện tại của chủ trọ.
            filtered = filtered.Where(entry => entry.PropertyId is { } propertyId && ownedPropertyIds.Contains(propertyId));
        }

        if (query.From is { } from)
        {
            filtered = filtered.Where(entry => entry.OccurredAt >= from);
        }

        if (query.To is { } to)
        {
            filtered = filtered.Where(entry => entry.OccurredAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.ActorUserId))
        {
            filtered = filtered.Where(entry => string.Equals(entry.ActorUserId, query.ActorUserId, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            filtered = filtered.Where(entry => string.Equals(entry.Action, query.Action, StringComparison.Ordinal));
        }

        if (query.PropertyId is { } propertyIdFilter)
        {
            filtered = filtered.Where(entry => entry.PropertyId == propertyIdFilter);
        }

        var ordered = filtered
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .ToList();

        var pageSize = Math.Clamp(query.PageSize <= 0 ? DefaultPageSize : query.PageSize, 1, MaxPageSize);
        var totalCount = ordered.Count;
        var page = Math.Max(1, query.Page);
        var pageItems = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = await ToDtosAsync(pageItems, cancellationToken);
        return new AuditLogPageDto(items, totalCount, page, pageSize);
    }

    /// <summary>Lựa chọn cho bộ lọc: chỉ lấy người/loại hành động xuất hiện trong sổ người gọi được đọc.</summary>
    public async Task<AuditLogFilterOptionsDto> GetFilterOptionsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        IReadOnlyList<AuditLogFileEntry> rows;
        if (caller.IsAdmin)
        {
            rows = await store.ReadAsync(AuditLogCategory.Account, ownerId: null, cancellationToken);
        }
        else if (caller.IsChuTro)
        {
            rows = await store.ReadAsync(AuditLogCategory.Business, caller.UserId, cancellationToken);
        }
        else
        {
            throw new AuditLogAuthorizationException(
                "Chỉ Admin hoặc Chủ trọ được xem nhật ký thao tác.",
                AuditLogErrorCodes.Forbidden);
        }

        var actorIds = rows
            .Select(entry => entry.ActorUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var names = await ActorNamesAsync(actorIds, cancellationToken);

        var actors = actorIds
            .Select(id => new AuditFilterActorDto(id, names.GetValueOrDefault(id, ShortId(id))))
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var actions = rows
            .Select(entry => entry.Action)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(action => action, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AuditLogFilterOptionsDto(actors, actions);
    }

    /// <summary>Ghi một dòng nhật ký đã lược dữ liệu nhạy cảm; dùng cho seed và test.</summary>
    public async Task RecordAsync(
        AuditLogCategory category,
        string action,
        string? actorUserId = null,
        int? propertyId = null,
        string? metadata = null,
        DateTimeOffset? occurredAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        var ownerId = category == AuditLogCategory.Business && propertyId is { } id
            ? await db.Properties.AsNoTracking()
                .Where(property => property.Id == id)
                .Select(property => property.OwnerId)
                .FirstOrDefaultAsync(cancellationToken)
            : null;

        await store.AppendAsync(new AuditLogFileEntry(
            Guid.NewGuid(),
            category,
            action,
            actorUserId,
            category == AuditLogCategory.Business ? propertyId : null,
            ownerId,
            metadata,
            occurredAt ?? DateTimeOffset.UtcNow), cancellationToken);
    }

    private async Task<HashSet<int>> OwnedPropertyIdsAsync(PropertyCaller caller, CancellationToken cancellationToken) =>
        (await db.Properties
            .AsNoTracking()
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    private async Task<IReadOnlyList<AuditLogEntryDto>> ToDtosAsync(
        IReadOnlyList<AuditLogFileEntry> entries,
        CancellationToken cancellationToken)
    {
        if (entries.Count == 0)
        {
            return [];
        }

        var actorIds = entries
            .Select(entry => entry.ActorUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var propertyIds = entries
            .Where(entry => entry.PropertyId is not null)
            .Select(entry => entry.PropertyId!.Value)
            .Distinct()
            .ToList();

        var actorNames = await ActorNamesAsync(actorIds, cancellationToken);
        var propertyNames = propertyIds.Count == 0
            ? new Dictionary<int, string>()
            : await db.Properties.AsNoTracking()
                .Where(property => propertyIds.Contains(property.Id))
                .Select(property => new { property.Id, property.Name })
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

        return entries.Select(entry => new AuditLogEntryDto(
            entry.Id,
            entry.Category == AuditLogCategory.Account ? "Account" : "Business",
            entry.Action,
            entry.ActorUserId,
            entry.ActorUserId is { } actorId
                ? actorNames.GetValueOrDefault(actorId, ShortId(actorId))
                : null,
            entry.PropertyId,
            entry.PropertyId is { } propertyId ? propertyNames.GetValueOrDefault(propertyId) : null,
            entry.Metadata,
            entry.OccurredAt)).ToList();
    }

    private async Task<Dictionary<string, string>> ActorNamesAsync(
        IReadOnlyCollection<string> actorIds,
        CancellationToken cancellationToken)
    {
        if (actorIds.Count == 0)
        {
            return [];
        }

        var users = await db.Users.AsNoTracking()
            .Where(user => actorIds.Contains(user.Id))
            .Select(user => new { user.Id, user.FullName, user.Email })
            .ToListAsync(cancellationToken);

        return users.ToDictionary(
            user => user.Id,
            user => string.IsNullOrWhiteSpace(user.FullName)
                ? (string.IsNullOrWhiteSpace(user.Email) ? ShortId(user.Id) : user.Email!)
                : user.FullName!);
    }

    private static string ShortId(string userId) =>
        userId.Length <= 8 ? userId : userId[..8];
}
