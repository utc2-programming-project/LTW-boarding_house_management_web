using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Tenants;

namespace Web.Features.Contracts.Services;

/// <summary>
/// Service SQL của yêu cầu gia hạn (VHT-125/2D): kiểm quyền và phạm vi bên trong service,
/// không tin hợp đồng hay hồ sơ gửi lên từ client. Chỉ ghi yêu cầu chờ xử lý — đổi
/// <c>Contract.EndDate</c> là việc của phân hệ Hợp đồng (P4).
/// </summary>
public sealed class ContractRenewalService(ApplicationDbContext db, TimeProvider clock) : IContractRenewalService
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

    public async Task<ContractRenewalResult> GetPendingAsync(
        TenantCaller caller,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        var (failure, _) = await ResolveActorAsync(caller, contractId, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var pending = await db.ContractRenewalRequests.AsNoTracking()
            .Where(request => request.ContractId == contractId && request.Status == RenewalRequestStatus.Pending)
            .OrderByDescending(request => request.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return ContractRenewalResult.Success(pending is null ? null : ToDto(pending));
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
    };
}
