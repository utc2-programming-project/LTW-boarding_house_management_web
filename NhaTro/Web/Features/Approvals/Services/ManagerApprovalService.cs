using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Data;
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
        if (!caller.IsQuanLy)
        {
            return ApprovalListResult.Failure(
                ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        var rows = await db.ManagerApprovalRequests
            .AsNoTracking()
            .Where(request => request.RequestedByUserId == caller.UserId)
            .OrderByDescending(request => request.RequestedAt)
            .ToListAsync(ct);

        return ApprovalListResult.Success(rows.Select(ToDto).ToList());
    }

    public async Task<ApprovalListResult> ListInboxAsync(
        ApprovalCaller caller, CancellationToken ct = default)
    {
        if (!caller.IsChuTro)
        {
            return ApprovalListResult.Failure(
                ApprovalErrorCodes.Forbidden, ApprovalErrorMessages.Forbidden);
        }

        var ownedPropertyIds = db.Properties
            .Where(property => property.OwnerId == caller.UserId)
            .Select(property => property.Id);

        var rows = await db.ManagerApprovalRequests
            .AsNoTracking()
            .Where(request => request.Status == ApprovalRequestStatus.Pending)
            .Where(request =>
                (request.PropertyId != null && ownedPropertyIds.Contains(request.PropertyId.Value))
                // Yêu cầu chưa gắn tòa (tạo hồ sơ mới): xét phân công hiệu lực của người gửi.
                || (request.PropertyId == null
                    && db.PropertyManagers.Any(assignment =>
                        assignment.ManagerUserId == request.RequestedByUserId
                        && assignment.RevokedAt == null
                        && ownedPropertyIds.Contains(assignment.PropertyId))))
            .OrderBy(request => request.RequestedAt)
            .ToListAsync(ct);

        return ApprovalListResult.Success(rows.Select(ToDto).ToList());
    }

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
