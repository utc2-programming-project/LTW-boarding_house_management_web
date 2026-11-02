using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Rules;

namespace Web.Features.Maintenance.Services;

public sealed class IncidentStatusApprovalApplier(TimeProvider clock) : IApprovalApplier
{
    public string OperationCode => "SW-13";

    public async Task ApplyAsync(ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Incident
            || !int.TryParse(request.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out var incidentId)
            || incidentId <= 0
            || request.PropertyId is null
            || request.ExpectedToken is not { Length: > 0 })
        {
            throw Invalid("Yêu cầu cập nhật phiếu sự cố không hợp lệ.");
        }

        IncidentUpdatePayload? payload;
        try { payload = JsonSerializer.Deserialize<IncidentUpdatePayload>(request.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException ex) { throw new ApprovalApplyException(IncidentErrorCodes.InvalidRequest, "Dữ liệu cập nhật sự cố không hợp lệ.", ex); }
        var update = payload is null ? null : new IncidentUpdateRequest
        {
            Status = payload.Status,
            AssigneeName = payload.AssigneeName,
            TechnicalNotes = payload.TechnicalNotes,
            RepairCost = payload.RepairCost,
        };
        if (IncidentRules.ValidateUpdate(update) is not null) throw Invalid("Dữ liệu cập nhật sự cố không hợp lệ.");

        var incident = await db.MaintenanceIncidents.SingleOrDefaultAsync(item => item.Id == incidentId, ct);
        if (incident is null) throw new ApprovalApplyException(IncidentErrorCodes.NotFound, "Không tìm thấy phiếu sự cố.");
        var roomPropertyId = await db.Rooms.Where(room => room.Id == incident.RoomId)
            .Select(room => (int?)room.PropertyId).SingleOrDefaultAsync(ct);
        if (roomPropertyId != request.PropertyId) throw Invalid("Phiếu sự cố không thuộc nhà trọ trong yêu cầu.");
        var statusChanged = incident.Status != payload!.Status;
        if (statusChanged && !IncidentRules.CanTransition(incident.Status, payload.Status))
            throw new ApprovalApplyException(IncidentErrorCodes.InvalidTransition, "Bước chuyển trạng thái không hợp lệ.");

        var from = incident.Status;
        var now = clock.GetUtcNow().UtcDateTime;
        incident.Status = payload.Status;
        incident.AssigneeName = payload.AssigneeName is null ? incident.AssigneeName : Normalize(payload.AssigneeName);
        incident.TechnicalNotes = payload.TechnicalNotes is null ? incident.TechnicalNotes : Normalize(payload.TechnicalNotes);
        incident.RepairCost = payload.RepairCost ?? incident.RepairCost;
        incident.UpdatedAt = now;
        db.Entry(incident).Property(item => item.RowVersion).OriginalValue = request.ExpectedToken;
        db.Entry(incident).Property(item => item.UpdatedAt).IsModified = true;
        db.MaintenanceIncidentHistories.Add(new MaintenanceIncidentHistory
        {
            IncidentId = incident.Id,
            Action = statusChanged ? IncidentHistoryAction.StatusChanged : IncidentHistoryAction.Updated,
            FromStatus = from,
            ToStatus = payload.Status,
            ActorUserId = request.RequestedByUserId,
            Note = incident.TechnicalNotes,
            CreatedAt = now,
        });
        db.MaintenanceIncidentHistories.Add(new MaintenanceIncidentHistory
        {
            IncidentId = incident.Id,
            Action = IncidentHistoryAction.Approved,
            FromStatus = from,
            ToStatus = payload.Status,
            ActorUserId = request.DecidedByUserId ?? string.Empty,
            Note = incident.TechnicalNotes,
            CreatedAt = now,
        });
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ApprovalApplyException Invalid(string message) => new(IncidentErrorCodes.InvalidRequest, message);
}
