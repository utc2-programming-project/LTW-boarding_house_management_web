using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Contracts.DTOs;
using Web.Features.Accounts;

namespace Web.Features.Contracts.Services;

public sealed record ContractApprovalSummary(string ContractNumber, string RoomCode, string PropertyName, string Action, DateTime? NewEndDate, decimal? CurrentDeposit, decimal? ProposedDeposit);

/// <summary>Read-only, owner-scoped summaries for P4 entries in the approval inbox.</summary>
public sealed class ContractApprovalSummaryService(ApplicationDbContext db)
{
    public async Task<IReadOnlyDictionary<Guid, ContractApprovalSummary>> GetAsync(IEnumerable<ApprovalRequestDto> requests, string userId, string role, CancellationToken ct = default)
    {
        var candidates = requests.Where(x => x.TargetType == ApprovalTargetType.Contract && (x.OperationCode == "SW-07" || x.OperationCode == "SW-08") && Guid.TryParse(x.TargetKey, out _)).ToList();
        var ids = candidates.Select(x => Guid.Parse(x.TargetKey)).ToList();
        var contracts = await (from contract in db.Contracts.AsNoTracking()
                               join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on contract.RoomId equals room.Id
                               join property in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
                               where ids.Contains(contract.ContractId)
                                   && (role == Roles.ChuTro ? property.OwnerId == userId
                                       : role == Roles.QuanLy && db.PropertyManagers.Any(m => m.PropertyId == property.Id && m.ManagerUserId == userId && m.RevokedAt == null))
                               select new { contract.ContractId, contract.ContractNumber, room.Code, PropertyName = property.Name }).ToDictionaryAsync(x => x.ContractId, ct);
        var result = new Dictionary<Guid, ContractApprovalSummary>();
        foreach (var request in candidates)
        {
            var id = Guid.Parse(request.TargetKey);
            if (!contracts.TryGetValue(id, out var contract)) continue;
            ContractApprovalPayload? payload;
            try { payload = JsonSerializer.Deserialize<ContractApprovalPayload>(request.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
            catch (JsonException) { continue; }
            if (payload is null) continue;
            result[request.Id] = new ContractApprovalSummary(contract.ContractNumber, contract.Code, contract.PropertyName, payload.Action, payload.EndDate, payload.ExpectedDepositAmount, payload.DepositAmount);
        }
        return result;
    }
}
