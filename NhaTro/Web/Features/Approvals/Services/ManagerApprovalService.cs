using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Common.Queries;
using Web.Data;
using Web.Features.Accounts.Owners;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Rules;
using Web.Features.Settings;
using Web.Infrastructure.Time;

namespace Web.Features.Approvals.Services;

/// <summary>
/// VHT-96 — hàng đợi duyệt thao tác Quản lý lưu trên SQL. Không cache: Chủ trọ phải thấy
/// yêu cầu ngay sau khi Quản lý gửi.
/// </summary>
public sealed class ManagerApprovalService(
    ApplicationDbContext db,
    TimeProvider clock,
    IEnumerable<IApprovalApplier> appliers,
    ILogger<ManagerApprovalService> logger) : IManagerApprovalService
{
    private readonly Dictionary<string, IApprovalApplier> _appliers =
        appliers.ToDictionary(applier => applier.OperationCode, StringComparer.OrdinalIgnoreCase);

    public async Task<ApprovalResult> CreateAsync(
        CreateApprovalRequest request, ApprovalCaller caller, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!caller.CanRequestApproval)
        {
            return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        // Fail closed: mã không có trong danh mục công tắc thì không nhận.
        if (!ManagerToggleCatalog.IsKnown(request.OperationCode))
        {
            logger.LogError("Mã công tắc không tồn tại: {Code}", request.OperationCode);
            return Failure(ApprovalErrorCodes.UnknownOperationCode, ApprovalErrorMessages.UnknownOperationCode);
        }

        if (ApprovalRequestRules.ValidateCreate(request.TargetKey, request.PayloadJson) is { } invalid)
        {
            return Failure(invalid, MessageOf(invalid));
        }

        // Chống gửi chéo: quyền tạo yêu cầu không được vượt phạm vi tòa được phân công.
        // PropertyId null là yêu cầu không gắn bản ghi (SW-16 xuất báo cáo) — hộp thư vẫn lọc theo phân công.
        if (request.PropertyId is { } propertyId
            && !await db.PropertyManagers.AnyAsync(
                assignment => assignment.PropertyId == propertyId
                    && assignment.ManagerUserId == caller.UserId
                    && assignment.RevokedAt == null,
                ct))
        {
            return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        // PLAN-001 #7: không tạo yêu cầu cho tòa của chủ trọ đang bị khoá.
        if (request.PropertyId is { } governedPropertyId)
        {
            var blockedOwners = OwnerLockQuery.BlockedOwnerIds(db);
            if (await db.Properties.AnyAsync(
                    property => property.Id == governedPropertyId
                        && blockedOwners.Contains(property.OwnerId),
                    ct))
            {
                return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
            }
        }

        var entity = new ManagerApprovalRequest
        {
            Id = Guid.NewGuid(),
            OperationCode = request.OperationCode,
            TargetType = request.TargetType,
            TargetKey = request.TargetKey.Trim(),
            PropertyId = request.PropertyId,
            PayloadJson = request.PayloadJson,
            ExpectedToken = request.ExpectedToken,
            Status = ApprovalRequestStatus.Pending,
            RequestedByUserId = caller.UserId,
            RequestedAt = clock.UtcNow(),
        };

        db.ManagerApprovalRequests.Add(entity);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Index lọc duy nhất: đã có yêu cầu Chờ duyệt cho cùng đối tượng.
            return Failure(
                ApprovalErrorCodes.DuplicatePendingRequest, ApprovalErrorMessages.DuplicatePendingRequest);
        }

        return ApprovalResult.Success(ToDto(entity));
    }

    public async Task<ApprovalListResult> ListMineAsync(
        ApprovalCaller caller, CancellationToken ct = default)
    {
        var query = MineQuery(caller);
        if (query is null)
        {
            return Forbidden();
        }

        var rows = await query
            .OrderByDescending(request => request.RequestedAt)
            .ToListAsync(ct);
        return ApprovalListResult.Success(rows.Select(ToDto).ToList(), rows.Count);
    }

    public async Task<ApprovalListResult> ListMineAsync(
        ApprovalCaller caller,
        IReadOnlyCollection<string> operationCodes,
        CancellationToken ct = default)
    {
        var query = MineQuery(caller);
        if (query is null)
        {
            return Forbidden();
        }

        if (operationCodes.Count > 0)
        {
            query = query.Where(request => operationCodes.Contains(request.OperationCode));
        }

        var rows = await query
            .OrderByDescending(request => request.RequestedAt)
            .ToListAsync(ct);
        return ApprovalListResult.Success(rows.Select(ToDto).ToList(), rows.Count);
    }

    public async Task<ApprovalListResult> ListMinePageAsync(
        ApprovalCaller caller, int page, int pageSize, CancellationToken ct = default)
    {
        var query = MineQuery(caller);
        if (query is null)
        {
            return Forbidden();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(request => request.RequestedAt)
            .ThenByDescending(request => request.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return ApprovalListResult.Success(rows.Select(ToDto).ToList(), total);
    }

    public async Task<ApprovalListResult> ListInboxAsync(
        ApprovalCaller caller, CancellationToken ct = default)
    {
        var query = InboxQuery(caller);
        if (query is null)
        {
            return Forbidden();
        }

        var rows = await query
            .OrderBy(request => request.RequestedAt)
            .ThenBy(request => request.Id)
            .ToListAsync(ct);
        return ApprovalListResult.Success(rows.Select(ToDto).ToList(), rows.Count);
    }

    public async Task<ApprovalListResult> ListInboxAsync(
        ApprovalCaller caller, IReadOnlyCollection<string> operationCodes, CancellationToken ct = default)
    {
        var query = InboxQuery(caller);
        if (query is null)
        {
            return Forbidden();
        }

        if (operationCodes.Count > 0)
        {
            query = query.Where(request => operationCodes.Contains(request.OperationCode));
        }

        var rows = await query
            .OrderBy(request => request.RequestedAt)
            .ThenBy(request => request.Id)
            .ToListAsync(ct);
        return ApprovalListResult.Success(rows.Select(ToDto).ToList(), rows.Count);
    }

    public async Task<ApprovalListResult> ListInboxPageAsync(
        ApprovalCaller caller, int page, int pageSize, CancellationToken ct = default)
    {
        var query = InboxQuery(caller);
        if (query is null)
        {
            return Forbidden();
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(request => request.RequestedAt)
            .ThenBy(request => request.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return ApprovalListResult.Success(rows.Select(ToDto).ToList(), total);
    }

    public async Task<ApprovalListResult> ListInboxPageAsync(
        ApprovalCaller caller, int page, int pageSize,
        IReadOnlyCollection<string>? operationCodes, bool newestFirst, CancellationToken ct = default,
        string? keyword = null)
    {
        var query = InboxQuery(caller);
        if (query is null)
        {
            return Forbidden();
        }

        if (operationCodes is { Count: > 0 })
        {
            query = query.Where(request => operationCodes.Contains(request.OperationCode));
        }

        // Từ khóa tìm ở SQL trên mã thao tác + nội dung yêu cầu (chứa mã phòng/hợp đồng/tiêu đề).
        if (SearchTerm.Normalize(keyword) is { Length: > 0 } term)
        {
            query = query.Where(request =>
                request.OperationCode.Contains(term) || request.PayloadJson.Contains(term));
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(ct);
        var ordered = newestFirst
            ? query.OrderByDescending(request => request.RequestedAt).ThenByDescending(request => request.Id)
            : query.OrderBy(request => request.RequestedAt).ThenBy(request => request.Id);
        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return ApprovalListResult.Success(rows.Select(ToDto).ToList(), total);
    }

    public async Task<ApprovalInboxStats> GetInboxStatsAsync(ApprovalCaller caller, CancellationToken ct = default)
    {
        var query = InboxQuery(caller);
        if (query is null)
        {
            return new ApprovalInboxStats(0, 0, null);
        }

        var today = DateOnly.FromDateTime(VietnamTime.ToVietnam(clock.GetUtcNow().UtcDateTime));
        var (startUtc, endUtcExclusive) = VietnamDateRange.Day(today);
        var stats = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                PendingCount = group.Count(),
                TodayCount = group.Count(request =>
                    request.RequestedAt >= startUtc && request.RequestedAt < endUtcExclusive),
                OldestRequestedAt = (DateTime?)group.Min(request => request.RequestedAt),
            })
            .SingleOrDefaultAsync(ct);

        return stats is null
            ? new ApprovalInboxStats(0, 0, null)
            : new ApprovalInboxStats(stats.PendingCount, stats.TodayCount, stats.OldestRequestedAt);
    }

    public async Task<int> CountPendingAsync(ApprovalCaller caller, CancellationToken ct = default)
    {
        if (caller.IsChuTro)
        {
            return (InboxQuery(caller) is { } inbox) ? await inbox.CountAsync(ct) : 0;
        }

        if (caller.IsQuanLy && MineQuery(caller) is { } mine)
        {
            return await mine.CountAsync(request => request.Status == ApprovalRequestStatus.Pending, ct);
        }

        return 0;
    }

    /// <summary>null khi người gọi không phải Quản lý.</summary>
    private IQueryable<ManagerApprovalRequest>? MineQuery(ApprovalCaller caller) =>
        caller.IsQuanLy
            ? db.ManagerApprovalRequests
                .AsNoTracking()
                .Where(request => request.RequestedByUserId == caller.UserId)
            : null;

    /// <summary>Hộp thư Chờ duyệt thuộc tòa Chủ trọ sở hữu; null khi người gọi không phải Chủ trọ.</summary>
    private IQueryable<ManagerApprovalRequest>? InboxQuery(ApprovalCaller caller)
    {
        if (!caller.IsChuTro)
        {
            return null;
        }

        var ownedPropertyIds = db.Properties
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id);

        return db.ManagerApprovalRequests
            .AsNoTracking()
            .Where(request => request.Status == ApprovalRequestStatus.Pending)
            .Where(request =>
                (request.PropertyId != null && ownedPropertyIds.Contains(request.PropertyId.Value))
                // Yêu cầu chưa gắn tòa (tạo hồ sơ mới): xét phân công hiệu lực của người gửi.
                || (request.PropertyId == null
                    && db.PropertyManagers.Any(assignment =>
                        assignment.ManagerUserId == request.RequestedByUserId
                        && assignment.RevokedAt == null
                        && ownedPropertyIds.Contains(assignment.PropertyId))));
    }

    private static ApprovalListResult Forbidden() =>
        ApprovalListResult.Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);

    public async Task<ApprovalResult> CancelAsync(
        Guid id, ApprovalCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsQuanLy)
        {
            return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var request = await LockAsync(id, ct);
        if (request is null)
        {
            return Failure(ApprovalErrorCodes.NotFound, ApprovalErrorMessages.NotFound);
        }

        if (request.RequestedByUserId != caller.UserId)
        {
            return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        if (!ApprovalRequestRules.CanDecide(request.Status))
        {
            return Failure(ApprovalErrorCodes.AlreadyDecided, ApprovalErrorMessages.AlreadyDecided);
        }

        request.Status = ApprovalRequestStatus.Cancelled;
        request.DecidedByUserId = caller.UserId;
        request.DecidedAt = clock.UtcNow();

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ApprovalErrorCodes.Conflict, ApprovalErrorMessages.Conflict);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Failure(ApprovalErrorCodes.Conflict, ApprovalErrorMessages.Conflict);
        }

        await transaction.CommitAsync(ct);
        return ApprovalResult.Success(ToDto(request));
    }

    public async Task<ApprovalResult> ApproveAsync(
        Guid id, ApprovalCaller caller, CancellationToken ct = default)
    {
        if (!caller.CanApprove)
        {
            // D4: Quản lý không được duyệt, kể cả yêu cầu của chính mình.
            return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var request = await LockAsync(id, ct);
        if (request is null)
        {
            return Failure(ApprovalErrorCodes.NotFound, ApprovalErrorMessages.NotFound);
        }

        if (!await IsInOwnerScopeAsync(request, caller, ct))
        {
            return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        if (!ApprovalRequestRules.CanDecide(request.Status))
        {
            return Failure(ApprovalErrorCodes.AlreadyDecided, ApprovalErrorMessages.AlreadyDecided);
        }

        if (!_appliers.TryGetValue(request.OperationCode, out var applier))
        {
            logger.LogError("Chưa đăng ký applier cho mã công tắc {Code}", request.OperationCode);
            return Failure(ApprovalErrorCodes.ApplierNotRegistered, ApprovalErrorMessages.ApplierNotRegistered);
        }

        request.Status = ApprovalRequestStatus.Approved;
        request.DecidedByUserId = caller.UserId;
        request.DecidedAt = clock.UtcNow();
        request.DecisionReason = null;

        try
        {
            await applier.ApplyAsync(request, db, ct);

            // Áp dụng thao tác + ghi quyết định trong cùng một transaction và một lần SaveChanges.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Đối tượng (hoặc chính yêu cầu) đã bị luồng khác sửa ⇒ không áp dụng.
            db.ChangeTracker.Clear();
            return Failure(ApprovalErrorCodes.Conflict, ApprovalErrorMessages.Conflict);
        }
        catch (DbUpdateException ex) when (applier is IApprovalSaveExceptionMapper mapper && mapper.MapSaveException(ex) is { } mapped)
        {
            db.ChangeTracker.Clear();
            return Failure(mapped.ErrorCode, mapped.Message);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return Failure(ApprovalErrorCodes.Conflict, ApprovalErrorMessages.Conflict);
        }
        catch (ApprovalApplyException ex)
        {
            db.ChangeTracker.Clear();
            return Failure(ex.ErrorCode, ex.Message);
        }

        await transaction.CommitAsync(ct);
        return ApprovalResult.Success(ToDto(request));
    }

    public async Task<ApprovalResult> RejectAsync(
        Guid id, RejectApprovalRequest request, ApprovalCaller caller, CancellationToken ct = default)
    {
        if (!caller.CanApprove)
        {
            return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        if (ApprovalRequestRules.ValidateRejectReason(request?.Reason, out var reason) is { } invalid)
        {
            return Failure(invalid, MessageOf(invalid));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var entity = await LockAsync(id, ct);
        if (entity is null)
        {
            return Failure(ApprovalErrorCodes.NotFound, ApprovalErrorMessages.NotFound);
        }

        if (!await IsInOwnerScopeAsync(entity, caller, ct))
        {
            return Failure(ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        if (!ApprovalRequestRules.CanDecide(entity.Status))
        {
            return Failure(ApprovalErrorCodes.AlreadyDecided, ApprovalErrorMessages.AlreadyDecided);
        }

        entity.Status = ApprovalRequestStatus.Rejected;
        entity.DecidedByUserId = caller.UserId;
        entity.DecidedAt = clock.UtcNow();
        entity.DecisionReason = reason;

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(ApprovalErrorCodes.Conflict, ApprovalErrorMessages.Conflict);
        }

        await transaction.CommitAsync(ct);
        return ApprovalResult.Success(ToDto(entity));
    }

    /// <summary>
    /// Khóa dòng yêu cầu bằng UPDLOCK/HOLDLOCK (mẫu ContractService) để hai luồng
    /// duyệt/hủy cùng lúc chỉ một luồng đọc được trạng thái Chờ duyệt.
    /// </summary>
    private Task<ManagerApprovalRequest?> LockAsync(Guid id, CancellationToken ct) =>
        db.ManagerApprovalRequests
            .FromSqlInterpolated(
                $"SELECT * FROM [ManagerApprovalRequests] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}")
            .SingleOrDefaultAsync(ct);

    /// <summary>Chủ trọ chỉ quyết định yêu cầu thuộc tòa mình sở hữu.</summary>
    private async Task<bool> IsInOwnerScopeAsync(
        ManagerApprovalRequest request, ApprovalCaller caller, CancellationToken ct)
    {
        if (!caller.IsChuTro)
        {
            return false;
        }

        if (request.PropertyId is { } propertyId)
        {
            return await db.Properties.AnyAsync(
                property => property.Id == propertyId && property.OwnerId == caller.UserId, ct);
        }

        return await db.PropertyManagers.AnyAsync(
            assignment => assignment.ManagerUserId == request.RequestedByUserId
                && assignment.RevokedAt == null
                && db.Properties.Any(
                    property => property.Id == assignment.PropertyId
                        && property.OwnerId == caller.UserId),
            ct);
    }

    private static ApprovalResult Failure(string errorCode, string errorMessage) =>
        ApprovalResult.Failure(errorCode, errorMessage);

    private static string MessageOf(string errorCode) => errorCode switch
    {
        ApprovalErrorCodes.RejectReasonRequired => ApprovalErrorMessages.RejectReasonRequired,
        ApprovalErrorCodes.RejectReasonTooLong => ApprovalErrorMessages.RejectReasonTooLong,
        _ => ApprovalErrorMessages.InvalidRequest,
    };

    private static ApprovalRequestDto ToDto(ManagerApprovalRequest request) =>
        new(
            request.Id,
            request.OperationCode,
            request.TargetType,
            request.TargetKey,
            request.PropertyId,
            request.PayloadJson,
            request.Status,
            request.RequestedByUserId,
            request.RequestedAt,
            request.DecidedByUserId,
            request.DecidedAt,
            request.DecisionReason);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
