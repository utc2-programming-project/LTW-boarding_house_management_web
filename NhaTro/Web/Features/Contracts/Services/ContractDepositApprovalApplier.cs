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

/// <summary>Applies the SW-08 deposit proposal inside the approval transaction.</summary>
public sealed class ContractDepositApprovalApplier(TimeProvider clock) : IApprovalApplier
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public string OperationCode => "SW-08";

    public async Task ApplyAsync(ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Contract || request.PropertyId is null
            || !Guid.TryParse(request.TargetKey, out var contractId)) throw Invalid(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus);
        ContractApprovalPayload? payload;
        try { payload = JsonSerializer.Deserialize<ContractApprovalPayload>(request.PayloadJson, JsonOptions); }
        catch (JsonException ex) { throw new ApprovalApplyException(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus, ex); }
        if (payload?.Action != ContractApprovalActions.UpdateDeposit)
            throw Invalid(ContractErrorCodes.InvalidStatus, ContractErrorMessages.InvalidStatus);
        if (payload.DepositAmount is null || payload.DepositAmount < 0m || payload.DepositAmount > 9_999_999_999_999_999.99m)
            throw Invalid(ContractErrorCodes.InvalidDepositAmount, ContractErrorMessages.InvalidDepositAmount);

        var locked = await db.Contracts.FromSqlInterpolated($"SELECT * FROM [Contracts] WITH (UPDLOCK, HOLDLOCK) WHERE [ContractId] = {contractId}")
            .AsNoTracking().SingleOrDefaultAsync(ct);
        var contract = await db.Contracts.SingleOrDefaultAsync(x => x.ContractId == contractId, ct);
        if (locked is null || contract is null) throw Invalid(ContractErrorCodes.ContractNotFound, ContractErrorMessages.ContractNotFound);
        var room = await db.Rooms.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == contract.RoomId, ct);
        if (room is null || room.PropertyId != request.PropertyId || room.IsDeleted || !room.IsActive) throw Invalid(ContractErrorCodes.RoomUnavailable, ContractErrorMessages.RoomUnavailable);
        var property = await db.Properties.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.Id == room.PropertyId, ct);
        if (property is null || property.IsDeleted) throw Invalid(ContractErrorCodes.PropertyUnavailable, ContractErrorMessages.PropertyUnavailable);
        try
        {
            ContractLifecycleOperations.EnsureDepositEditable(contract, clock.TodayVietnam(),
                await ContractLifecycleOperations.HasApprovedSuccessorAsync(db, contract.ContractId, contract.RoomId, contract.StartDate, ct));
        }
        catch (ContractOperationException ex) { throw new ApprovalApplyException(ex.ErrorCode, ex.Message, ex); }
        if (payload.ExpectedDepositAmount is not null && payload.ExpectedDepositAmount != contract.DepositAmount)
            throw Invalid(ContractErrorCodes.DepositChangedAfterRequest, ContractErrorMessages.DepositChangedAfterRequest);

        var oldDeposit = contract.DepositAmount;
        contract.DepositAmount = Web.Common.Utils.MoneyFormatter.RoundVnd(payload.DepositAmount.Value);
        // Đổi mức cọc thì phải thu lại theo mức mới; xoá xác nhận đã thu cũ.
        contract.DepositPaidAmount = null;
        contract.DepositPaidAt = null;
        contract.DepositPaidByUserId = null;
        ContractLifecycleOperations.AddAudit(db, ContractLifecycleOperations.ContractAuditActions.DepositUpdated,
            request.DecidedByUserId ?? string.Empty, request.PropertyId.Value,
            JsonSerializer.Serialize(new { contractId, oldDeposit, newDeposit = contract.DepositAmount }), clock.GetUtcNow());
    }

    private static ApprovalApplyException Invalid(string code, string message) => new(code, message);
}
