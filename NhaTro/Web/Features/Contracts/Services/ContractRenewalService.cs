using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Tenants;

namespace Web.Features.Contracts.Services;

/// <summary>
/// Service SQL của yêu cầu gia hạn (VHT-125/2D và 2E): kiểm quyền và phạm vi bên trong service,
/// không tin hợp đồng hay hồ sơ gửi lên từ client. Chấp thuận yêu cầu giao việc đổi hợp đồng cho
/// phân hệ Hợp đồng (P4) để công tắc SW-07 của Quản lý áp dụng như mọi thao tác gia hạn khác.
/// </summary>
public sealed class ContractRenewalService(
    ApplicationDbContext db,
    TimeProvider clock,
    IContractService contracts) : IContractRenewalService
{
    public async Task<ContractRenewalResult> SubmitAsync(
        TenantCaller caller,
        Guid contractId,
        int requestedMonths,
        string? note,
        CancellationToken cancellationToken = default)
    {
        var (failure, tenantId) = await ResolveActorAsync(caller, contractId, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!ContractRenewalRules.IsMonthsInRange(requestedMonths))
        {
            return Failure(ContractRenewalErrorCodes.InvalidMonths, ContractRenewalErrorMessages.InvalidMonths);
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmedNote?.Length > ContractRenewalRules.MaxNoteLength)
        {
            return Failure(ContractRenewalErrorCodes.NoteTooLong, ContractRenewalErrorMessages.NoteTooLong);
        }

        if (await HasPendingAsync(contractId, cancellationToken))
        {
            return Failure(ContractRenewalErrorCodes.DuplicatePending, ContractRenewalErrorMessages.DuplicatePending);
        }

        var request = new ContractRenewalRequest
        {
            ContractId = contractId,
            RequestedByTenantId = tenantId,
            RequestedMonths = requestedMonths,
            Note = trimmedNote,
            Status = RenewalRequestStatus.Pending,
            RequestedAt = clock.GetUtcNow().UtcDateTime,
        };

        db.ContractRenewalRequests.Add(request);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicatePending(ex))
        {
            // Index UX_ContractRenewalRequests_Pending chặn hai khách cùng phòng gửi song song.
            return Failure(ContractRenewalErrorCodes.DuplicatePending, ContractRenewalErrorMessages.DuplicatePending);
        }

        return ContractRenewalResult.Success(ToDto(request));
    }

    public async Task<RenewalRequestListResult> ListPendingAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsManager)
        {
            return RenewalRequestListResult.Failure(
                ContractRenewalErrorCodes.Forbidden,
                ContractErrorMessages.Forbidden);
        }

        var pending = db.ContractRenewalRequests
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(request => request.Status == RenewalRequestStatus.Pending);

        var activeContracts = db.Contracts.AsNoTracking();
        var rooms = db.Rooms.IgnoreQueryFilters().AsNoTracking();
        var properties = ScopedProperties(caller);
        var tenants = db.Tenants.IgnoreQueryFilters().AsNoTracking();

        var rows = await (
            from request in pending
            join contract in activeContracts on request.ContractId equals contract.ContractId
            join room in rooms on contract.RoomId equals room.Id
            join property in properties on room.PropertyId equals property.Id
            join requester in tenants on request.RequestedByTenantId equals requester.Id
            orderby request.RequestedAt
            select new RenewalRequestItemDto
            {
                Id = request.Id,
                ContractId = contract.ContractId,
                ContractNumber = contract.ContractNumber,
                RoomCode = room.Code,
                PropertyName = property.Name,
                RequesterName = requester.FullName,
                RequestedMonths = request.RequestedMonths,
                Note = request.Note,
                RequestedAt = request.RequestedAt,
                CurrentEndDate = contract.EndDate,
            })
            .ToListAsync(cancellationToken);

        return RenewalRequestListResult.Success(rows);
    }

    public async Task<RenewalHistoryResult> ListForContractAsync(
        TenantCaller caller,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await ResolveActorAsync(caller, contractId, cancellationToken);
        if (failure is not null)
        {
            return RenewalHistoryResult.Failure(failure.ErrorCode!, failure.ErrorMessage!);
        }

        var items = await db.ContractRenewalRequests
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(request => request.ContractId == contractId)
            .OrderByDescending(request => request.RequestedAt)
            .Select(request => new ContractRenewalRequestDto
            {
                Id = request.Id,
                ContractId = request.ContractId,
                RequestedMonths = request.RequestedMonths,
                Note = request.Note,
                Status = request.Status,
                RequestedAt = request.RequestedAt,
                DecidedAt = request.DecidedAt,
                RejectReason = request.RejectReason,
            })
            .ToListAsync(cancellationToken);

        return RenewalHistoryResult.Success(items);
    }

    public async Task<ContractRenewalResult> DecideAsync(
        Guid requestId,
        RenewalDecisionRequest decision,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(caller);

        if (!caller.IsManager)
        {
            return Failure(ContractRenewalErrorCodes.Forbidden, ContractErrorMessages.Forbidden);
        }

        var request = await db.ContractRenewalRequests
            .FirstOrDefaultAsync(item => item.Id == requestId, cancellationToken);

        if (request is null)
        {
            return Failure(ContractRenewalErrorCodes.RequestNotFound, ContractRenewalErrorMessages.RequestNotFound);
        }

        if (request.Status != RenewalRequestStatus.Pending)
        {
            return Failure(ContractRenewalErrorCodes.AlreadyDecided, ContractRenewalErrorMessages.AlreadyDecided);
        }

        if (!await IsContractInScopeAsync(request.ContractId, caller, cancellationToken))
        {
            return Failure(ContractRenewalErrorCodes.Forbidden, ContractErrorMessages.Forbidden);
        }

        return decision.IsApproved
            ? await AcceptAsync(request, caller, cancellationToken)
            : await RejectAsync(request, decision.Reason, caller, cancellationToken);
    }

    /// <summary>
    /// Gia hạn qua P4 trước, chốt trạng thái yêu cầu sau: khi SW-07 bắt Chủ trọ duyệt thì P4 trả
    /// PendingApproval và yêu cầu của khách phải ở lại trạng thái Chờ xử lý.
    /// </summary>
    private async Task<ContractRenewalResult> AcceptAsync(
        ContractRenewalRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var currentEndDate = await db.Contracts.AsNoTracking()
            .Where(contract => contract.ContractId == request.ContractId)
            .Select(contract => (DateTime?)contract.EndDate)
            .FirstOrDefaultAsync(cancellationToken);

        if (currentEndDate is null)
        {
            return Failure(
                ContractRenewalErrorCodes.ContractNotFound,
                ContractRenewalErrorMessages.ContractNotFound);
        }

        var proposedEndDate = currentEndDate.Value.AddMonths(request.RequestedMonths);

        ContractActionResultDto outcome;
        try
        {
            outcome = await contracts.RenewContractAsync(
                request.ContractId,
                new RenewContractRequestDto { EndDate = proposedEndDate },
                caller,
                cancellationToken);
        }
        catch (Exception ex) when (ex is IContractServiceError contractError)
        {
            return Failure(MapContractErrorCode(contractError.ErrorCode), ex.Message);
        }

        if (outcome.Outcome == ContractActionOutcome.PendingApproval)
        {
            return ContractRenewalResult.PendingOwnerApproval(ToDto(request));
        }

        MarkDecided(request, RenewalRequestStatus.Approved, caller.UserId, rejectReason: null);

        return await SaveDecisionAsync(request, cancellationToken);
    }

    private async Task<ContractRenewalResult> RejectAsync(
        ContractRenewalRequest request,
        string? reason,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var trimmed = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        if (trimmed is null)
        {
            return Failure(
                ContractRenewalErrorCodes.RejectReasonRequired,
                ContractRenewalErrorMessages.RejectReasonRequired);
        }

        if (trimmed.Length > ContractRenewalRules.MaxNoteLength)
        {
            return Failure(ContractRenewalErrorCodes.ReasonTooLong, ContractRenewalErrorMessages.ReasonTooLong);
        }

        MarkDecided(request, RenewalRequestStatus.Rejected, caller.UserId, trimmed);

        return await SaveDecisionAsync(request, cancellationToken);
    }

    private async Task<ContractRenewalResult> SaveDecisionAsync(
        ContractRenewalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion: một luồng khác vừa chốt yêu cầu này.
            db.ChangeTracker.Clear();
            return Failure(ContractRenewalErrorCodes.AlreadyDecided, ContractRenewalErrorMessages.AlreadyDecided);
        }

        return ContractRenewalResult.Success(ToDto(request));
    }

    private void MarkDecided(
        ContractRenewalRequest request,
        RenewalRequestStatus status,
        string decidedByUserId,
        string? rejectReason)
    {
        request.Status = status;
        request.RejectReason = rejectReason;
        request.DecidedAt = clock.GetUtcNow().UtcDateTime;
        request.DecidedByUserId = decidedByUserId;
    }

    private IQueryable<Property> ScopedProperties(PropertyCaller caller) =>
        PropertyScope.Apply(
            db.Properties.IgnoreQueryFilters().AsNoTracking(),
            db.PropertyManagers.AsNoTracking(),
            caller);

    /// <summary>Yêu cầu chỉ thuộc phạm vi quyết định nếu hợp đồng của nó nằm trong nhà trọ của caller.</summary>
    private async Task<bool> IsContractInScopeAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        var propertyId = await (
            from contract in db.Contracts.AsNoTracking()
            join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
            where contract.ContractId == contractId
            select (int?)room.PropertyId)
            .FirstOrDefaultAsync(cancellationToken);

        return propertyId is not null
            && await ScopedProperties(caller).AnyAsync(property => property.Id == propertyId, cancellationToken);
    }

    /// <summary>
    /// Kiểm caller là khách thuê có hồ sơ còn hiệu lực, là thành viên của hợp đồng và hợp đồng
    /// đã duyệt. Trả <c>failure</c> khác <c>null</c> khi không được thao tác.
    /// </summary>
    private async Task<(ContractRenewalResult? failure, int tenantId)> ResolveActorAsync(
        TenantCaller caller,
        Guid contractId,
        CancellationToken cancellationToken)
    {
        if (!caller.IsTenant || string.IsNullOrWhiteSpace(caller.UserId))
        {
            return (Failure(ContractRenewalErrorCodes.Forbidden, ContractRenewalErrorMessages.Forbidden), 0);
        }

        var tenantId = await db.Tenants.AsNoTracking()
            .Where(tenant => tenant.UserId == caller.UserId && !tenant.IsDeleted)
            .Select(tenant => tenant.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (tenantId == 0)
        {
            return (Failure(ContractRenewalErrorCodes.Forbidden, ContractRenewalErrorMessages.Forbidden), 0);
        }

        var isMember = await db.ContractMembers.AsNoTracking()
            .AnyAsync(member => member.ContractId == contractId && member.TenantId == tenantId, cancellationToken);

        if (!isMember)
        {
            return (Failure(ContractRenewalErrorCodes.Forbidden, ContractRenewalErrorMessages.Forbidden), tenantId);
        }

        var status = await db.Contracts.AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Select(contract => (ContractStatus?)contract.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status is null)
        {
            return (Failure(ContractRenewalErrorCodes.ContractNotFound, ContractRenewalErrorMessages.ContractNotFound), tenantId);
        }

        if (status != ContractStatus.Approved)
        {
            return (Failure(ContractRenewalErrorCodes.InvalidStatus, ContractRenewalErrorMessages.InvalidStatus), tenantId);
        }

        return (null, tenantId);
    }

    private Task<bool> HasPendingAsync(Guid contractId, CancellationToken cancellationToken) =>
        db.ContractRenewalRequests.AnyAsync(
            request => request.ContractId == contractId && request.Status == RenewalRequestStatus.Pending,
            cancellationToken);

    // 2601 = vi phạm unique index, 2627 = vi phạm primary key.
    private static bool IsDuplicatePending(DbUpdateException exception) =>
        exception.InnerException is SqlException sql
        && (sql.Number == 2601 || sql.Number == 2627);

    // Mã lỗi của P4 ánh xạ về mã lỗi luồng gia hạn để UI chỉ cần một bảng tra.
    private static string MapContractErrorCode(string contractErrorCode) => contractErrorCode switch
    {
        ContractErrorCodes.ApprovalDenied => ContractRenewalErrorCodes.ApprovalDenied,
        ContractErrorCodes.Forbidden => ContractRenewalErrorCodes.Forbidden,
        ContractErrorCodes.ContractNotFound => ContractRenewalErrorCodes.ContractNotFound,
        ContractErrorCodes.InvalidStatus => ContractRenewalErrorCodes.InvalidStatus,
        ContractErrorCodes.ContractNotActive => ContractRenewalErrorCodes.InvalidStatus,
        ContractErrorCodes.ContractAlreadyRenewed => ContractRenewalErrorCodes.AlreadyRenewed,
        ContractErrorCodes.ContractOverlap => ContractRenewalErrorCodes.DateOverlap,
        ContractErrorCodes.InvalidRenewalEndDate => ContractRenewalErrorCodes.EndDateInvalid,
        _ => ContractRenewalErrorCodes.RenewApplyFailed,
    };

    private static ContractRenewalResult Failure(string errorCode, string errorMessage) =>
        ContractRenewalResult.Failure(errorCode, errorMessage);

    private static ContractRenewalRequestDto ToDto(ContractRenewalRequest request) => new()
    {
        Id = request.Id,
        ContractId = request.ContractId,
        RequestedMonths = request.RequestedMonths,
        Note = request.Note,
        Status = request.Status,
        RequestedAt = request.RequestedAt,
        DecidedAt = request.DecidedAt,
        RejectReason = request.RejectReason,
    };
}
