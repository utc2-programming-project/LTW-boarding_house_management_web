using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Contracts.Services;

/// <summary>Áp dụng yêu cầu SW-07 (gia hạn / thanh lý hợp đồng) khi Chủ trọ duyệt.</summary>
public sealed class ContractApprovalApplier(TimeProvider clock) : IApprovalApplier
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

        var contract = await db.Contracts.SingleOrDefaultAsync(item => item.ContractId == contractId, ct);
        if (contract is null)
        {
            throw Invalid(ContractErrorCodes.ContractNotFound, ContractErrorMessages.ContractNotFound);
        }

        var room = await db.Rooms
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == contract.RoomId, ct);
        if (room is null)
        {
            throw Invalid(ContractErrorCodes.RoomNotFound, ContractErrorMessages.RoomNotFound);
        }

        if (room.PropertyId != request.PropertyId.Value)
        {
            throw Invalid(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus);
        }

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

                await ContractLifecycleOperations.CreateRenewalAsync(db, contract, room, payload.EndDate.Value, ct);
                break;

            case ContractApprovalActions.Liquidate:
                await ContractLifecycleOperations.LiquidateAsync(db, contract, room, today, ct);
                break;

            default:
                throw Invalid(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus);
        }
    }

    private static ApprovalApplyException Invalid(string errorCode, string message) =>
        new(errorCode, message);
}
