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
using Web.Features.Rooms;

namespace Web.Features.Maintenance.Services;

public sealed class IncidentCreateApprovalApplier(TimeProvider clock) : IApprovalApplier
{
    public string OperationCode => "SW-12";

    public async Task ApplyAsync(ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Room
            || !int.TryParse(request.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out var roomId)
            || roomId <= 0
            || request.PropertyId is null)
        {
            throw Invalid("Yêu cầu tạo phiếu sự cố không hợp lệ.");
        }

        IncidentCreatePayload? payload;
        try { payload = JsonSerializer.Deserialize<IncidentCreatePayload>(request.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException ex) { throw new ApprovalApplyException(IncidentErrorCodes.InvalidRequest, "Dữ liệu phiếu sự cố không hợp lệ.", ex); }

        var input = payload is null ? null : new IncidentCreateRequest
        {
            RoomId = roomId,
            Title = payload.Title,
            Description = payload.Description,
            Category = payload.Category,
            Urgency = payload.Urgency,
            ExpectedPhotoCount = payload.ExpectedPhotoCount,
        };
        if (IncidentRules.ValidateCreate(input) is not null) throw Invalid("Dữ liệu phiếu sự cố không hợp lệ.");

        var room = await db.Rooms.SingleOrDefaultAsync(item => item.Id == roomId
            && item.PropertyId == request.PropertyId && item.IsActive && !item.IsDeleted, ct);
        if (room is null) throw Invalid("Phòng không còn hợp lệ.");

        var now = clock.GetUtcNow().UtcDateTime;
        var incident = new MaintenanceIncident
        {
            RoomId = room.Id,
            ReportedByUserId = request.RequestedByUserId,
            Title = payload!.Title.Trim(),
            Description = payload.Description.Trim(),
            Category = payload.Category,
            Urgency = payload.Urgency,
            Status = IncidentStatus.New,
            CreatedAt = now,
        };
        incident.History.Add(new MaintenanceIncidentHistory
        {
            Action = IncidentHistoryAction.Created,
            ToStatus = IncidentStatus.New,
            ActorUserId = request.RequestedByUserId,
            CreatedAt = now,
        });
        incident.History.Add(new MaintenanceIncidentHistory
        {
            Action = IncidentHistoryAction.Approved,
            ToStatus = IncidentStatus.New,
            ActorUserId = request.DecidedByUserId ?? string.Empty,
            CreatedAt = now,
        });
        db.MaintenanceIncidents.Add(incident);
        // Request đã bị queue khóa; ảnh chỉ gắn vào phiếu trong cùng transaction duyệt.
        var photos = await db.IncidentPhotoAttachments.Include(p => p.File)
            .Where(p => p.ApprovalRequestId == request.Id).ToListAsync(ct);
        if (photos.Count(p => !p.File.IsDeleted) != payload.ExpectedPhotoCount
            || photos.Any(p => p.Kind != IncidentPhotoKind.Report || p.IncidentId != null))
            throw new ApprovalApplyException("ApprovalPhotosIncomplete", "Cần tải đủ ảnh của yêu cầu trước khi duyệt.");
        foreach (var photo in photos) photo.Incident = incident;
    }

    private static ApprovalApplyException Invalid(string message) =>
        new(IncidentErrorCodes.InvalidRequest, message);
}
