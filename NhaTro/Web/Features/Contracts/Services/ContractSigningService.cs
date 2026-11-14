using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Web.Common.Utils;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Accounts.Otp;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants;
using Web.Infrastructure.Email;
using Web.Infrastructure.Time;

namespace Web.Features.Contracts.Services;

/// <summary>
/// Điều phối luồng ký hợp đồng: gửi khách → khách đồng ý (OTP) hoặc yêu cầu sửa → chủ duyệt.
/// Mọi bước đều ghi audit + thông báo trong ứng dụng.
/// </summary>
public sealed class ContractSigningService(
    ApplicationDbContext db,
    TimeProvider timeProvider,
    IOtpService otp,
    IOutboundEmailSender emailSender,
    INotificationService notifications,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    IContractService contracts,
    ILogger<ContractSigningService> logger) : IContractSigningService
{
    private const string EmailPurpose = "ContractSigning";

    public async Task<TenantContractReviewDto> GetForTenantAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (!caller.IsKhachThue)
        {
            throw Forbidden();
        }

        var contract = await contracts.GetAsync(contractId, caller, cancellationToken);
        if (contract.Status == ContractStatus.Draft)
        {
            throw new ContractNotFoundException(
                ContractErrorMessages.ContractNotFound,
                ContractErrorCodes.ContractNotFound);
        }

        var tenant = await GetTenantAsync(caller.UserId, cancellationToken);
        await EnsureMemberAsync(contractId, tenant.Id, cancellationToken);

        var version = await GetLatestVersionAsync(contractId, cancellationToken);
        var acceptance = version is null
            ? null
            : await db.ContractAcceptances.AsNoTracking()
                .Where(item => item.ContractId == contractId && item.ContractVersionId == version.Id)
                .OrderByDescending(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);

        var changeRequests = await LoadChangeRequestsAsync(contractId, cancellationToken);
        var isRepresentative = await db.ContractMembers.AsNoTracking()
            .AnyAsync(member => member.ContractId == contractId
                && member.TenantId == tenant.Id
                && member.IsRepresentative, cancellationToken);
        var canRespond = isRepresentative
            && contract.Status == ContractStatus.AwaitingTenant
            && acceptance is null
            && version is not null;

        return new TenantContractReviewDto(
            contract,
            version?.VersionNo ?? 0,
            version?.ContentHash ?? string.Empty,
            version?.SentAt,
            canRespond,
            canRespond,
            acceptance is not null,
            acceptance?.AcceptedAt,
            changeRequests);
    }

    public async Task IssueAcceptanceOtpAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (!caller.IsKhachThue)
        {
            throw Forbidden();
        }

        var tenant = await GetTenantAsync(caller.UserId, cancellationToken);
        await EnsureMemberAsync(contractId, tenant.Id, cancellationToken);
        await EnsureRepresentativeAsync(contractId, tenant.Id, cancellationToken);

        var contract = await LoadTenantContractAsync(contractId, caller, tenant.Id, cancellationToken);
        EnsureStatus(contract, ContractStatus.AwaitingTenant);

        if (string.IsNullOrWhiteSpace(tenant.Email) || string.IsNullOrWhiteSpace(tenant.UserId))
        {
            throw Forbidden();
        }

        var result = await otp.IssueAsync(tenant.UserId, tenant.Email, OtpPurpose.ContractAcceptance, cancellationToken);
        if (result.Status is not OtpIssueStatus.Success)
        {
            throw new ContractOperationException(
                ContractErrorMessages.OtpTooManyRequests,
                ContractErrorCodes.OtpTooManyRequests);
        }
    }

    public async Task<ContractActionResultDto> SendToTenantAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(caller);
        var contract = await LoadManagedContractAsync(contractId, caller, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockContractAsync(contractId, cancellationToken);
            await db.Entry(contract).ReloadAsync(cancellationToken);
            EnsureStatus(contract, ContractStatus.Draft, ContractStatus.ChangeRequested);
            await EnsureDraftValidAsync(contract, cancellationToken);

            var now = timeProvider.GetUtcNow();
            var pendingRequests = await db.ContractChangeRequests
                .Where(request => request.ContractId == contractId && request.Status == ContractChangeRequestStatus.Pending)
                .ToListAsync(cancellationToken);
            foreach (var request in pendingRequests)
            {
                request.Status = ContractChangeRequestStatus.Resolved;
                request.ResolvedAt = now;
                request.ResolvedByUserId = caller.UserId;
                request.ResolutionNote = "Đã gửi bản chỉnh sửa cho khách.";
            }

            var version = await ContractSigningWorkflow.CreateVersionAsync(
                db, contract, caller.UserId, now, sent: true, cancellationToken);
            contract.Status = ContractStatus.AwaitingTenant;
            contract.RejectReason = null;

            await AddAuditAsync(contract, "ContractSentToTenant", caller.UserId, now, new { version = version.VersionNo }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await NotifyTenantsContractSentAsync(contract, cancellationToken);
        return Applied(contractId);
    }

    public async Task<ContractActionResultDto> UpdateDraftAsync(
        Guid contractId,
        UpdateContractDraftRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsureCanManage(caller);
        var contract = await LoadManagedContractAsync(contractId, caller, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockContractAsync(contractId, cancellationToken);
            await db.Entry(contract).ReloadAsync(cancellationToken);
            EnsureStatus(contract, ContractStatus.Draft, ContractStatus.ChangeRequested);

            var tenantIds = await ValidateDraftRequestAsync(request, caller, cancellationToken);
            var representativeId = request.RepresentativeTenantId ?? tenantIds[0];
            if (!tenantIds.Contains(representativeId))
            {
                throw new ContractValidationException(
                    ContractErrorMessages.RepresentativeInvalid,
                    ContractErrorCodes.RepresentativeInvalid,
                    nameof(request.RepresentativeTenantId));
            }

            var relations = NormalizeRelationships(request.MemberRelations, tenantIds);
            var terms = NormalizeTerms(request.Terms);
            var monthlyRent = request.MonthlyRent is { } rent
                ? MoneyFormatter.RoundVnd(rent)
                : contract.SnapshotPrice;
            var deposit = request.DepositAmount is { } value
                ? MoneyFormatter.RoundVnd(value)
                : contract.DepositAmount;

            var depositChanged = contract.DepositAmount != deposit;
            contract.StartDate = request.StartDate;
            contract.EndDate = request.EndDate;
            contract.SnapshotPrice = monthlyRent;
            contract.DepositAmount = deposit;
            if (depositChanged)
            {
                // Đổi mức cọc thì xác nhận thu cọc cũ không còn hiệu lực.
                contract.DepositPaidAmount = null;
                contract.DepositPaidAt = null;
                contract.DepositPaidByUserId = null;
            }
            contract.PaymentCycleMonths = request.PaymentCycleMonths;
            contract.MeterReadingDay = request.MeterReadingDay;
            contract.Terms = terms;

            await db.ContractMembers
                .Where(member => member.ContractId == contractId)
                .ExecuteDeleteAsync(cancellationToken);
            db.ContractMembers.AddRange(tenantIds.Select(tenantId => new ContractMember
            {
                ContractId = contractId,
                TenantId = tenantId,
                IsRepresentative = tenantId == representativeId,
                Relationship = relations.GetValueOrDefault(tenantId),
            }));

            var now = timeProvider.GetUtcNow();
            await AddAuditAsync(contract, "ContractDraftUpdated", caller.UserId, now, new { members = tenantIds.Count }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Applied(contractId);
    }

    public async Task<ContractActionResultDto> RequestChangesAsync(
        Guid contractId,
        string message,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (!caller.IsKhachThue)
        {
            throw Forbidden();
        }

        var tenant = await GetTenantAsync(caller.UserId, cancellationToken);
        await EnsureMemberAsync(contractId, tenant.Id, cancellationToken);
        await EnsureRepresentativeAsync(contractId, tenant.Id, cancellationToken);
        var trimmed = (message ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ContractValidationException(
                ContractErrorMessages.ChangeRequestMessageRequired,
                ContractErrorCodes.ChangeRequestMessageRequired,
                nameof(message));
        }

        if (trimmed.Length > 2000)
        {
            throw new ContractValidationException(
                ContractErrorMessages.ChangeRequestMessageTooLong,
                ContractErrorCodes.ChangeRequestMessageTooLong,
                nameof(message));
        }

        var contract = await LoadTenantContractAsync(contractId, caller, tenant.Id, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockContractAsync(contractId, cancellationToken);
            await db.Entry(contract).ReloadAsync(cancellationToken);
            EnsureStatus(contract, ContractStatus.AwaitingTenant);

            var version = await GetLatestVersionAsync(contractId, cancellationToken)
                ?? throw new ContractOperationException(
                    ContractErrorMessages.ContractVersionMissing,
                    ContractErrorCodes.ContractVersionMissing);

            var now = timeProvider.GetUtcNow();
            db.ContractChangeRequests.Add(new ContractChangeRequest
            {
                ContractId = contractId,
                ContractVersionId = version.Id,
                TenantId = tenant.Id,
                Message = trimmed,
                Status = ContractChangeRequestStatus.Pending,
                CreatedAt = now,
            });
            contract.Status = ContractStatus.ChangeRequested;

            await AddAuditAsync(contract, "ContractChangeRequested", caller.UserId, now, new { version = version.VersionNo }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await NotifyManagersChangeRequestedAsync(contract, tenant.FullName, cancellationToken);
        return Applied(contractId);
    }

    public async Task<ContractActionResultDto> AcceptAsync(
        Guid contractId,
        string otpCode,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (!caller.IsKhachThue)
        {
            throw Forbidden();
        }

        if (string.IsNullOrWhiteSpace(otpCode))
        {
            throw new ContractValidationException(
                ContractErrorMessages.OtpRequired,
                ContractErrorCodes.OtpRequired,
                nameof(otpCode));
        }

        var tenant = await GetTenantAsync(caller.UserId, cancellationToken);
        await EnsureMemberAsync(contractId, tenant.Id, cancellationToken);
        await EnsureRepresentativeAsync(contractId, tenant.Id, cancellationToken);
        var contract = await LoadTenantContractAsync(contractId, caller, tenant.Id, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockContractAsync(contractId, cancellationToken);
            await db.Entry(contract).ReloadAsync(cancellationToken);
            EnsureStatus(contract, ContractStatus.AwaitingTenant);

            var version = await GetLatestVersionAsync(contractId, cancellationToken)
                ?? throw new ContractOperationException(
                    ContractErrorMessages.ContractVersionMissing,
                    ContractErrorCodes.ContractVersionMissing);

            if (ContractSigningWorkflow.ComputeHash(version.SnapshotJson) != version.ContentHash)
            {
                throw new ContractOperationException(
                    ContractErrorMessages.ContractVersionMissing,
                    ContractErrorCodes.ContractVersionMissing);
            }

            if (await db.ContractAcceptances.AnyAsync(
                    item => item.ContractId == contractId && item.ContractVersionId == version.Id,
                    cancellationToken))
            {
                throw new ContractOperationException(
                    ContractErrorMessages.ContractAlreadyAccepted,
                    ContractErrorCodes.ContractAlreadyAccepted);
            }

            if (string.IsNullOrWhiteSpace(tenant.Email) || string.IsNullOrWhiteSpace(tenant.UserId))
            {
                throw Forbidden();
            }

            var verify = await otp.VerifyAsync(
                tenant.UserId, tenant.Email, OtpPurpose.ContractAcceptance, otpCode.Trim(), cancellationToken);
            switch (verify.Status)
            {
                case OtpVerifyStatus.Success:
                    break;
                case OtpVerifyStatus.Expired:
                    throw new ContractValidationException(
                        ContractErrorMessages.OtpExpired, ContractErrorCodes.OtpExpired, nameof(otpCode));
                case OtpVerifyStatus.Locked:
                    throw new ContractOperationException(
                        ContractErrorMessages.OtpTooManyRequests, ContractErrorCodes.OtpTooManyRequests);
                default:
                    throw new ContractValidationException(
                        ContractErrorMessages.OtpInvalid, ContractErrorCodes.OtpInvalid, nameof(otpCode));
            }

            var http = httpContextAccessor.HttpContext;
            var ip = http?.Connection.RemoteIpAddress?.ToString();
            var userAgent = http?.Request.Headers.UserAgent.ToString();
            if (userAgent is { Length: > 256 })
            {
                userAgent = userAgent[..256];
            }

            var now = timeProvider.GetUtcNow();
            var challengeId = await db.OtpChallenges.AsNoTracking()
                .Where(item => item.Email == tenant.Email
                    && item.Purpose == OtpPurpose.ContractAcceptance
                    && item.ConsumedAt != null)
                .OrderByDescending(item => item.Id)
                .Select(item => (int?)item.Id)
                .FirstOrDefaultAsync(cancellationToken);

            db.ContractAcceptances.Add(new ContractAcceptance
            {
                ContractId = contractId,
                ContractVersionId = version.Id,
                TenantId = tenant.Id,
                UserId = caller.UserId,
                AcceptedAt = now,
                ContentHash = version.ContentHash,
                OtpChallengeId = challengeId,
                IpAddress = ip,
                UserAgent = userAgent,
                SignatureMethod = "Otp",
            });

            var ownerCreated = string.Equals(contract.CreatedByRole, Roles.ChuTro, StringComparison.Ordinal);
            // Chỉ chủ trọ tạo mới được duyệt ngay và bắt đầu hạn cọc. HĐ do QL tạo còn
            // chờ chủ trọ duyệt nên chưa được chạy đồng hồ 48h.
            contract.DepositDueAt = ownerCreated && contract.DepositPaidAt is null
                ? now.Add(ContractPaymentRules.DepositPaymentWindow)
                : null;
            if (ownerCreated)
            {
                await EnsureRoomAvailableForApprovalAsync(contract, cancellationToken);
            }

            contract.Status = ownerCreated ? ContractStatus.Approved : ContractStatus.Pending;
            contract.RejectReason = null;

            if (ownerCreated)
            {
                await RentRoomIfActiveAsync(contract, cancellationToken);
            }

            await AddAuditAsync(contract, "ContractTenantAccepted", caller.UserId, now, new
            {
                version = version.VersionNo,
                hash = version.ContentHash,
            }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await NotifyManagersTenantAcceptedAsync(contract, cancellationToken);
        return Applied(contractId);
    }

    public async Task<ContractActionResultDto> WithdrawAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(caller);
        var contract = await LoadManagedContractAsync(contractId, caller, cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await LockContractAsync(contractId, cancellationToken);
            await db.Entry(contract).ReloadAsync(cancellationToken);
            EnsureStatus(contract, ContractStatus.AwaitingTenant, ContractStatus.ChangeRequested);

            var now = timeProvider.GetUtcNow();
            var pending = await db.ContractChangeRequests
                .Where(request => request.ContractId == contractId && request.Status == ContractChangeRequestStatus.Pending)
                .ToListAsync(cancellationToken);
            foreach (var request in pending)
            {
                request.Status = ContractChangeRequestStatus.Dismissed;
                request.ResolvedAt = now;
                request.ResolvedByUserId = caller.UserId;
            }

            contract.Status = ContractStatus.Draft;
            await AddAuditAsync(contract, "ContractWithdrawn", caller.UserId, now, new { }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Applied(contractId);
    }

    public async Task<IReadOnlyList<ContractChangeRequestDto>> ListChangeRequestsAsync(
        Guid contractId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        if (caller.IsKhachThue)
        {
            var tenant = await GetTenantAsync(caller.UserId, cancellationToken);
            await EnsureMemberAsync(contractId, tenant.Id, cancellationToken);
        }
        else if (caller.IsChuTro || caller.IsQuanLy)
        {
            await LoadManagedContractAsync(contractId, caller, cancellationToken);
        }
        else
        {
            throw Forbidden();
        }

        return await LoadChangeRequestsAsync(contractId, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingRoomAssignmentDto>> ListPendingAssignmentsAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        EnsureCanManage(caller);
        var properties = PropertyScope.Apply(
            db.Properties.AsNoTracking(),
            db.PropertyManagers.AsNoTracking(),
            caller);

        return await (
            from assignment in db.PendingRoomAssignments.AsNoTracking()
            join property in properties on assignment.PropertyId equals property.Id
            join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on assignment.RoomId equals room.Id
            join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on assignment.TenantId equals tenant.Id
            where assignment.Status == Web.Features.Tenants.Entities.PendingRoomAssignmentStatus.Pending
            orderby assignment.CreatedAt descending
            select new PendingRoomAssignmentDto(
                assignment.Id,
                tenant.Id,
                tenant.FullName,
                room.Id,
                room.Code,
                property.Id,
                property.Name,
                assignment.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<ContractChangeRequestDto>> LoadChangeRequestsAsync(
        Guid contractId,
        CancellationToken cancellationToken)
    {
        return await (
            from request in db.ContractChangeRequests.AsNoTracking()
            join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on request.TenantId equals tenant.Id
            join version in db.ContractVersions.AsNoTracking() on request.ContractVersionId equals version.Id
            where request.ContractId == contractId
            orderby request.CreatedAt descending
            select new ContractChangeRequestDto(
                request.Id,
                version.VersionNo,
                tenant.Id,
                tenant.FullName,
                request.Message,
                request.Status,
                request.CreatedAt,
                request.ResolvedAt,
                request.ResolutionNote))
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureDraftValidAsync(Contract contract, CancellationToken cancellationToken)
    {
        var tenantIds = await db.ContractMembers.AsNoTracking()
            .Where(member => member.ContractId == contract.ContractId)
            .Select(member => member.TenantId)
            .ToListAsync(cancellationToken);
        if (tenantIds.Count == 0)
        {
            throw new ContractValidationException(
                ContractErrorMessages.TenantRequired,
                ContractErrorCodes.TenantRequired,
                nameof(tenantIds));
        }

        var hasRepresentative = await db.ContractMembers.AsNoTracking()
            .AnyAsync(member => member.ContractId == contract.ContractId && member.IsRepresentative, cancellationToken);
        if (!hasRepresentative)
        {
            throw new ContractValidationException(
                ContractErrorMessages.RepresentativeInvalid,
                ContractErrorCodes.RepresentativeInvalid,
                nameof(hasRepresentative));
        }

        var roomStatus = await db.Rooms.AsNoTracking()
            .Where(room => room.Id == contract.RoomId)
            .Select(room => new { room.IsActive, room.IsDeleted, room.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (roomStatus is null || roomStatus.IsDeleted || !roomStatus.IsActive)
        {
            throw new ContractOperationException(
                ContractErrorMessages.RoomUnavailable,
                ContractErrorCodes.RoomUnavailable);
        }

        if (roomStatus.Status == RoomStatus.Maintenance)
        {
            throw new ContractOperationException(
                ContractErrorMessages.RoomUnderMaintenance,
                ContractErrorCodes.RoomUnderMaintenance);
        }

        var overlaps = await db.Contracts.AnyAsync(other =>
            other.ContractId != contract.ContractId
            && other.RoomId == contract.RoomId
            && (other.Status == ContractStatus.Pending
                || other.Status == ContractStatus.Approved
                || other.Status == ContractStatus.AwaitingTenant
                || other.Status == ContractStatus.ChangeRequested
                || other.Status == ContractStatus.TenantAccepted)
            && other.StartDate <= contract.EndDate
            && contract.StartDate <= other.EndDate,
            cancellationToken);
        if (overlaps)
        {
            throw new ContractOperationException(
                ContractErrorMessages.PendingContractExists,
                ContractErrorCodes.PendingContractExists);
        }
    }

    private async Task<IReadOnlyList<int>> ValidateDraftRequestAsync(
        UpdateContractDraftRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken)
    {
        if (request.StartDate >= request.EndDate)
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidDateRange,
                ContractErrorCodes.InvalidDateRange,
                nameof(request.StartDate));
        }

        if (request.EndDate.Date < request.StartDate.Date.AddMonths(1))
        {
            throw new ContractValidationException(
                ContractErrorMessages.EndDateTooShort,
                ContractErrorCodes.EndDateTooShort,
                nameof(request.EndDate));
        }

        if (DateOnly.FromDateTime(request.StartDate) > timeProvider.TodayVietnam())
        {
            throw new ContractValidationException(
                ContractErrorMessages.FutureStartNotAllowed,
                ContractErrorCodes.FutureStartNotAllowed,
                nameof(request.StartDate));
        }

        var tenantIds = request.TenantIds.Distinct().ToList();
        if (tenantIds.Count == 0)
        {
            throw new ContractValidationException(
                ContractErrorMessages.TenantRequired,
                ContractErrorCodes.TenantRequired,
                nameof(request.TenantIds));
        }

        var existing = db.Tenants.IgnoreQueryFilters()
            .Where(tenant => tenantIds.Contains(tenant.Id) && tenant.IsActive && !tenant.IsDeleted)
            .Select(tenant => tenant.Id);
        if (await existing.CountAsync(cancellationToken) != tenantIds.Count)
        {
            throw new ContractValidationException(
                ContractErrorMessages.TenantUnavailable,
                ContractErrorCodes.TenantUnavailable,
                nameof(request.TenantIds));
        }

        if (request.MonthlyRent is < 0m)
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidMonthlyRent,
                ContractErrorCodes.InvalidMonthlyRent,
                nameof(request.MonthlyRent));
        }

        if (request.DepositAmount is < 0m)
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidDepositAmount,
                ContractErrorCodes.InvalidDepositAmount,
                nameof(request.DepositAmount));
        }

        if (request.PaymentCycleMonths is not (1 or 2 or 3 or 6 or 12))
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidPaymentCycle,
                ContractErrorCodes.InvalidPaymentCycle,
                nameof(request.PaymentCycleMonths));
        }

        if (request.MeterReadingDay is { } day && day is < 1 or > 28)
        {
            throw new ContractValidationException(
                ContractErrorMessages.InvalidMeterReadingDay,
                ContractErrorCodes.InvalidMeterReadingDay,
                nameof(request.MeterReadingDay));
        }

        return tenantIds;
    }

    private static Dictionary<int, string> NormalizeRelationships(
        IReadOnlyDictionary<int, string>? relations,
        IReadOnlyList<int> tenantIds)
    {
        var result = new Dictionary<int, string>();
        if (relations is null)
        {
            return result;
        }

        foreach (var tenantId in tenantIds)
        {
            if (!relations.TryGetValue(tenantId, out var relationship) || string.IsNullOrWhiteSpace(relationship))
            {
                continue;
            }

            var trimmed = relationship.Trim();
            if (trimmed.Length > 50)
            {
                throw new ContractValidationException(
                    ContractErrorMessages.RelationshipTooLong,
                    ContractErrorCodes.RelationshipTooLong,
                    "MemberRelations");
            }

            result[tenantId] = trimmed;
        }

        return result;
    }

    private static string? NormalizeTerms(string? terms)
    {
        var normalized = string.IsNullOrWhiteSpace(terms) ? null : terms.Trim();
        if (normalized is { Length: > 4000 })
        {
            throw new ContractValidationException(
                ContractErrorMessages.TermsTooLong,
                ContractErrorCodes.TermsTooLong,
                nameof(terms));
        }

        return normalized;
    }

    private async Task EnsureRoomAvailableForApprovalAsync(Contract contract, CancellationToken cancellationToken)
    {
        var room = await db.Rooms.AsNoTracking()
            .Where(item => item.Id == contract.RoomId)
            .Select(item => new { item.IsDeleted, item.IsActive, item.Status })
            .SingleAsync(cancellationToken);

        if (room.IsDeleted || !room.IsActive)
        {
            throw new ContractOperationException(
                ContractErrorMessages.RoomUnavailable,
                ContractErrorCodes.RoomUnavailable);
        }

        if (room.Status == RoomStatus.Maintenance)
        {
            throw new ContractOperationException(
                ContractErrorMessages.RoomUnderMaintenance,
                ContractErrorCodes.RoomUnderMaintenance);
        }

        if (room.Status != RoomStatus.Available)
        {
            throw new ContractOperationException(
                ContractErrorMessages.RoomNotVacant,
                ContractErrorCodes.RoomNotVacant);
        }

        var overlaps = await db.Contracts.AnyAsync(other =>
            other.ContractId != contract.ContractId
            && other.RoomId == contract.RoomId
            && other.Status == ContractStatus.Approved
            && other.StartDate <= contract.EndDate
            && contract.StartDate <= other.EndDate,
            cancellationToken);
        if (overlaps)
        {
            throw new ContractOperationException(
                ContractErrorMessages.ContractOverlap,
                ContractErrorCodes.ContractOverlap);
        }
    }

    private async Task RentRoomIfActiveAsync(Contract contract, CancellationToken cancellationToken)
    {
        if (DateOnly.FromDateTime(contract.StartDate) > timeProvider.TodayVietnam())
        {
            return;
        }

        var room = await db.Rooms
            .FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {contract.RoomId}")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
        if (room is null)
        {
            return;
        }

        room.Status = RoomStatus.Rented;
        room.UpdatedAt = timeProvider.UtcNow();
    }

    private async Task<TenantInfo> GetTenantAsync(string userId, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .Select(item => new TenantInfo(item.Id, item.UserId, item.Email, item.FullName))
            .FirstOrDefaultAsync(cancellationToken);
        if (tenant is null)
        {
            throw Forbidden();
        }

        return tenant;
    }

    private async Task EnsureMemberAsync(Guid contractId, int tenantId, CancellationToken cancellationToken)
    {
        if (!await db.ContractMembers.AsNoTracking()
                .AnyAsync(member => member.ContractId == contractId && member.TenantId == tenantId, cancellationToken))
        {
            throw Forbidden();
        }
    }

    private async Task EnsureRepresentativeAsync(Guid contractId, int tenantId, CancellationToken cancellationToken)
    {
        if (!await db.ContractMembers.AsNoTracking()
                .AnyAsync(member => member.ContractId == contractId
                    && member.TenantId == tenantId
                    && member.IsRepresentative, cancellationToken))
        {
            throw Forbidden();
        }
    }

    private async Task<Contract> LoadManagedContractAsync(
        Guid contractId, PropertyCaller caller, CancellationToken cancellationToken)
    {
        var contract = await ScopedContracts(caller)
            .SingleOrDefaultAsync(item => item.ContractId == contractId, cancellationToken);
        return contract ?? throw new ContractNotFoundException(
            ContractErrorMessages.ContractNotFound, ContractErrorCodes.ContractNotFound);
    }

    private async Task<Contract> LoadTenantContractAsync(
        Guid contractId, PropertyCaller caller, int tenantId, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts
            .Where(item => item.ContractId == contractId
                && db.ContractMembers.Any(member => member.ContractId == contractId && member.TenantId == tenantId))
            .SingleOrDefaultAsync(cancellationToken);
        return contract ?? throw new ContractNotFoundException(
            ContractErrorMessages.ContractNotFound, ContractErrorCodes.ContractNotFound);
    }

    private IQueryable<Contract> ScopedContracts(PropertyCaller caller)
    {
        if (caller.IsChuTro)
        {
            return from contract in db.Contracts
                   join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   join property in db.Properties.IgnoreQueryFilters() on room.PropertyId equals property.Id
                   where property.OwnerId == caller.UserId
                   select contract;
        }

        if (caller.IsQuanLy)
        {
            return from contract in db.Contracts
                   join room in db.Rooms.IgnoreQueryFilters() on contract.RoomId equals room.Id
                   where db.PropertyManagers.Any(assignment =>
                       assignment.PropertyId == room.PropertyId
                       && assignment.ManagerUserId == caller.UserId
                       && assignment.RevokedAt == null)
                   select contract;
        }

        return db.Contracts.Where(_ => false);
    }

    private async Task<ContractVersion?> GetLatestVersionAsync(Guid contractId, CancellationToken cancellationToken) =>
        await db.ContractVersions.AsNoTracking()
            .Where(version => version.ContractId == contractId)
            .OrderByDescending(version => version.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task LockContractAsync(Guid contractId, CancellationToken cancellationToken)
    {
        await db.Contracts
            .FromSqlInterpolated($"SELECT * FROM [Contracts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContractId] = {contractId}")
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task NotifyTenantsContractSentAsync(Contract contract, CancellationToken cancellationToken)
    {
        var recipients = await GetTenantRecipientsAsync(contract.ContractId, cancellationToken);
        var link = $"/my/contracts/{contract.ContractId}";
        foreach (var recipient in recipients.Where(item => !string.IsNullOrWhiteSpace(item.UserId)))
        {
            await SafeNotifyAsync(
                recipient.UserId!, "Hợp đồng chờ xác nhận",
                $"Hợp đồng {contract.ContractNumber} đã được gửi để bạn xem và xác nhận.",
                link);
        }

        var representative = recipients.FirstOrDefault(item => item.IsRepresentative) ?? recipients.FirstOrDefault();
        if (representative is not null && !string.IsNullOrWhiteSpace(representative.Email))
        {
            var content = EmailTemplates.ContractAwaitingTenant(
                representative.FullName,
                representative.Email,
                contract.ContractNumber,
                $"{BaseUrl}/my/contracts/{contract.ContractId}");
            await SafeEmailAsync(representative.Email, content);
        }
    }

    private async Task NotifyManagersChangeRequestedAsync(
        Contract contract, string tenantName, CancellationToken cancellationToken)
    {
        var (ownerId, managerIds) = await GetPropertyRecipientsAsync(contract.ContractId, cancellationToken);
        var link = $"/contracts/{contract.ContractId}";
        foreach (var userId in new[] { ownerId }.Concat(managerIds).Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            await SafeNotifyAsync(
                userId!, "Khách yêu cầu chỉnh sửa hợp đồng",
                $"{tenantName} đã gửi yêu cầu chỉnh sửa hợp đồng {contract.ContractNumber}.",
                link);
        }
    }

    private async Task NotifyManagersTenantAcceptedAsync(Contract contract, CancellationToken cancellationToken)
    {
        var (ownerId, managerIds) = await GetPropertyRecipientsAsync(contract.ContractId, cancellationToken);
        var link = $"/contracts/{contract.ContractId}";
        foreach (var userId in new[] { ownerId }.Concat(managerIds).Where(id => !string.IsNullOrWhiteSpace(id)))
        {
            await SafeNotifyAsync(
                userId!, "Khách đã đồng ý hợp đồng",
                $"Hợp đồng {contract.ContractNumber} đã được khách xác nhận.",
                link);
        }
    }

    private async Task<IReadOnlyList<TenantRecipient>> GetTenantRecipientsAsync(
        Guid contractId, CancellationToken cancellationToken)
    {
        return await (
            from member in db.ContractMembers.AsNoTracking()
            join tenant in db.Tenants.IgnoreQueryFilters().AsNoTracking() on member.TenantId equals tenant.Id
            where member.ContractId == contractId && !tenant.IsDeleted
            orderby member.IsRepresentative descending, member.TenantId
            select new TenantRecipient(
                tenant.Id, tenant.UserId, tenant.Email, tenant.FullName, member.IsRepresentative))
            .ToListAsync(cancellationToken);
    }

    private async Task<(string? OwnerId, IReadOnlyList<string> ManagerIds)> GetPropertyRecipientsAsync(
        Guid contractId, CancellationToken cancellationToken)
    {
        var property = await (
            from contract in db.Contracts.IgnoreQueryFilters().AsNoTracking()
            join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
            join item in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals item.Id
            where contract.ContractId == contractId
            select new { item.Id, item.OwnerId })
            .SingleAsync(cancellationToken);

        var managerIds = await db.PropertyManagers.AsNoTracking()
            .Where(assignment => assignment.PropertyId == property.Id && assignment.RevokedAt == null)
            .Select(assignment => assignment.ManagerUserId)
            .ToListAsync(cancellationToken);

        return (property.OwnerId, managerIds);
    }

    private async Task SafeNotifyAsync(string userId, string title, string body, string link)
    {
        try
        {
            await notifications.NotifyAsync(userId, title, body, link);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được thông báo hợp đồng cho {UserId}.", userId);
        }
    }

    private async Task SafeEmailAsync(string email, EmailTemplates.EmailContent content)
    {
        try
        {
            await emailSender.SendAsync(email, content.Subject, content.Html);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email hợp đồng tới {Email}.", email);
        }
    }

    private async Task AddAuditAsync(
        Contract contract,
        string action,
        string actorUserId,
        DateTimeOffset now,
        object metadata,
        CancellationToken cancellationToken)
    {
        var propertyId = await db.Contracts.IgnoreQueryFilters()
            .Where(item => item.ContractId == contract.ContractId)
            .Join(db.Rooms.IgnoreQueryFilters(), item => item.RoomId, room => room.Id, (_, room) => room.PropertyId)
            .FirstOrDefaultAsync(cancellationToken);
        ContractLifecycleOperations.AddAudit(
            db,
            action,
            actorUserId,
            propertyId,
            JsonSerializer.Serialize(metadata),
            now);
    }

    private static void EnsureStatus(Contract contract, params ContractStatus[] allowed)
    {
        if (Array.IndexOf(allowed, contract.Status) < 0)
        {
            throw new ContractOperationException(
                ContractErrorMessages.InvalidSigningStatus,
                ContractErrorCodes.InvalidSigningStatus);
        }
    }

    private static void EnsureCanManage(PropertyCaller caller)
    {
        if (!caller.IsChuTro && !caller.IsQuanLy)
        {
            throw Forbidden();
        }
    }

    private static ContractAuthorizationException Forbidden() =>
        new(ContractErrorMessages.Forbidden, ContractErrorCodes.Forbidden);

    private static ContractActionResultDto Applied(Guid contractId) =>
        new(ContractActionOutcome.Applied, contractId, null);

    private string BaseUrl
    {
        get
        {
            var request = httpContextAccessor.HttpContext?.Request;
            if (request is not null && request.Host.HasValue)
            {
                return $"{request.Scheme}://{request.Host}";
            }

            var configured = configuration["Invitations:PublicBaseUrl"];
            return string.IsNullOrWhiteSpace(configured) ? "http://localhost" : configured.TrimEnd('/');
        }
    }

    private sealed record TenantInfo(int Id, string? UserId, string? Email, string FullName);

    private sealed record TenantRecipient(
        int TenantId, string? UserId, string? Email, string FullName, bool IsRepresentative);
}
