using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Contracts.Services;

/// <summary>Áp dụng yêu cầu SW-07 (gia hạn / thanh lý hợp đồng) khi Chủ trọ duyệt.</summary>
public sealed class ContractApprovalApplier(TimeProvider clock) : IApprovalApplier, IApprovalSaveExceptionMapper
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public string OperationCode => "SW-07";

    public async Task ApplyAsync(
        ManagerApprovalRequest request,
        ApplicationDbContext db,
        CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Contract
            || !Guid.TryParse(request.TargetKey, out var contractId)
            || request.PropertyId is null)
        {
            throw Invalid(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus);
        }

        ContractApprovalPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<ContractApprovalPayload>(request.PayloadJson, PayloadJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new ApprovalApplyException(
                ContractErrorCodes.InvalidStatus,
                ContractErrorMessages.InvalidStatus,
                ex);
        }

        if (payload is null)
        {
            throw Invalid(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus);
        }

        var roomId = await db.Contracts.AsNoTracking().Where(x => x.ContractId == contractId)
            .Select(x => (int?)x.RoomId).SingleOrDefaultAsync(ct);
        if (roomId is null)
        {
            throw Invalid(ContractErrorCodes.ContractNotFound, ContractErrorMessages.ContractNotFound);
        }
        var lockedRoom = await db.Rooms
            .FromSqlInterpolated($"SELECT * FROM [Rooms] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {roomId.Value}")
            .IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(ct);
        var lockedContract = await db.Contracts
            .FromSqlInterpolated($"SELECT * FROM [Contracts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContractId] = {contractId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var contract = await db.Contracts.SingleOrDefaultAsync(item => item.ContractId == contractId, ct);
        if (lockedContract is null || contract is null)
            throw Invalid(ContractErrorCodes.ContractNotFound, ContractErrorMessages.ContractNotFound);
        var room = await db.Rooms
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == contract.RoomId, ct);
        if (lockedRoom is null || room is null)
        {
            throw Invalid(ContractErrorCodes.RoomNotFound, ContractErrorMessages.RoomNotFound);
        }

        if (room.PropertyId != request.PropertyId.Value)
        {
            throw Invalid(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus);
        }

        var property = await db.Properties.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == room.PropertyId, ct);
        if (property is null || property.IsDeleted)
            throw Invalid(ContractErrorCodes.PropertyUnavailable, ContractErrorMessages.PropertyUnavailable);
        if (room.IsDeleted || !room.IsActive)
            throw Invalid(ContractErrorCodes.RoomUnavailable, ContractErrorMessages.RoomUnavailable);

        var today = clock.TodayVietnam();
        if (!ContractLifecycleOperations.IsActionable(contract.Status, contract.StartDate, contract.EndDate, today))
        {
            throw Invalid(ContractErrorCodes.ContractNotActive, ContractErrorMessages.ContractNotActive);
        }

        switch (payload.Action)
        {
            case ContractApprovalActions.Renew:
                if (payload.EndDate is null)
                {
                    throw Invalid(ContractErrorCodes.InvalidRenewalEndDate, ContractErrorMessages.InvalidRenewalEndDate);
                }

                try
                {
                    ContractLifecycleOperations.EnsureRenewalEndDate(contract.EndDate, payload.EndDate.Value);
                }
                catch (ContractValidationException ex)
                {
                    throw new ApprovalApplyException(ex.ErrorCode, ex.Message, ex);
                }

                if (await ContractLifecycleOperations.HasApprovedSuccessorAsync(
                        db, contract.ContractId, contract.RoomId, contract.StartDate, ct))
                {
                    throw Invalid(ContractErrorCodes.ContractAlreadyRenewed, ContractErrorMessages.ContractAlreadyRenewed);
                }

                if (await ContractLifecycleOperations.HasOverlappingApprovedAsync(
                        db,
                        contract.RoomId,
                        contract.EndDate.AddDays(1),
                        payload.EndDate.Value,
                        ct))
                {
                    throw Invalid(ContractErrorCodes.ContractOverlap, ContractErrorMessages.ContractOverlap);
                }

                var renewal = await ContractLifecycleOperations.CreateRenewalAsync(db, contract, room, payload.EndDate.Value, clock.GetUtcNow(), ct);
                await ContractLifecycleOperations.SettlePendingRenewalRequestsAsync(db, contractId,
                    RenewalRequestStatus.Approved, request.DecidedByUserId ?? string.Empty,
                    request.DecidedAt ?? clock.GetUtcNow().UtcDateTime, null, ct);
                ContractLifecycleOperations.AddAudit(db, ContractLifecycleOperations.ContractAuditActions.Renewed,
                    request.DecidedByUserId ?? string.Empty, request.PropertyId.Value,
                    JsonSerializer.Serialize(new { contractId, newContractId = renewal.ContractId, newEndDate = payload.EndDate.Value }), clock.GetUtcNow());
                break;

            case ContractApprovalActions.Liquidate:
                await ContractLifecycleOperations.LiquidateAsync(db, contract, room, today, ct);
                await ContractLifecycleOperations.SettlePendingRenewalRequestsAsync(db, contractId,
                    RenewalRequestStatus.Rejected, request.DecidedByUserId ?? string.Empty,
                    request.DecidedAt ?? clock.GetUtcNow().UtcDateTime, ContractErrorMessages.RenewalRejectedByLiquidation, ct);
                ContractLifecycleOperations.AddAudit(db, ContractLifecycleOperations.ContractAuditActions.Liquidated,
                    request.DecidedByUserId ?? string.Empty, request.PropertyId.Value,
                    JsonSerializer.Serialize(new { contractId }), clock.GetUtcNow());
                break;

            default:
                throw Invalid(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus);
        }
    }

    private static ApprovalApplyException Invalid(string errorCode, string message) =>
        new(errorCode, message);

    public ApprovalApplyException? MapSaveException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 51030 })
                return Invalid(ContractErrorCodes.ContractOverlap, ContractErrorMessages.ContractOverlap);
        }
        return null;
    }

}
