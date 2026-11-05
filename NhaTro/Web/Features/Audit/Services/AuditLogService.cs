using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Audit.DTOs;
using Web.Features.Audit.Entities;
using Web.Features.Properties;

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
/// D16 — hai sổ tách biệt: Admin chỉ đọc sổ hệ thống/tài khoản, Chủ trọ chỉ đọc sổ nghiệp vụ
/// trong các tòa nhà mình sở hữu. Quản lý và Khách thuê không có quyền đọc nhật ký.
/// </summary>
public sealed class AuditLogService(ApplicationDbContext db)
{
    public async Task<IReadOnlyList<AuditLogEntryDto>> QueryAsync(
        PropertyCaller caller,
        AuditLogQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        query ??= new AuditLogQuery();
        var take = Math.Clamp(query.Take, 1, 500);

        if (caller.IsAdmin)
        {
            return await db.Set<AuditLogEntry>()
                .AsNoTracking()
                .Where(entry => entry.Category == AuditLogCategory.Account)
                .OrderByDescending(entry => entry.OccurredAt)
                .Take(take)
                .Select(ToDto)
                .ToListAsync(cancellationToken);
        }

        if (!caller.IsChuTro)
        {
            throw new AuditLogAuthorizationException(
                "Chỉ Admin hoặc Chủ trọ được xem nhật ký thao tác.",
                AuditLogErrorCodes.Forbidden);
        }

        var ownedPropertyIds = db.Properties
            .AsNoTracking()
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id);

        if (query.PropertyId is { } requestedPropertyId
            && !await ownedPropertyIds.AnyAsync(id => id == requestedPropertyId, cancellationToken))
        {
            throw new AuditLogAuthorizationException(
                "Không được xem nhật ký ngoài phạm vi tòa nhà của mình.",
                AuditLogErrorCodes.OutsideScope);
        }

        return await db.Set<AuditLogEntry>()
            .AsNoTracking()
            .Where(entry => entry.Category == AuditLogCategory.Business)
            .Where(entry => entry.PropertyId != null && ownedPropertyIds.Contains(entry.PropertyId.Value))
            .Where(entry => query.PropertyId == null || entry.PropertyId == query.PropertyId)
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(take)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    /// <summary>Ghi một dòng nhật ký đã lược dữ liệu nhạy cảm; dùng chung cho nghiệp vụ và test.</summary>
    public async Task RecordAsync(
        AuditLogCategory category,
        string action,
        string? actorUserId = null,
        int? propertyId = null,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);

        db.Set<AuditLogEntry>().Add(new AuditLogEntry
        {
            Category = category,
            Action = action,
            ActorUserId = actorUserId,
            PropertyId = category == AuditLogCategory.Business ? propertyId : null,
            Metadata = metadata,
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static System.Linq.Expressions.Expression<Func<AuditLogEntry, AuditLogEntryDto>> ToDto =>
        entry => new AuditLogEntryDto(
            entry.Id,
            entry.Category == AuditLogCategory.Account ? "Account" : "Business",
            entry.Action,
            entry.ActorUserId,
            entry.PropertyId,
            entry.Metadata,
            entry.OccurredAt);
}
