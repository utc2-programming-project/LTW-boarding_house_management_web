using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Rules;
using Web.Features.Storage;

namespace Web.Features.Maintenance.Services;

public sealed class IncidentResultPhotoApprovalApplier(IIncidentFileScope scope) : IApprovalApplier
{
    public string OperationCode => "SW-14";

    public async Task ApplyAsync(ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
    {
        if (request.TargetType != ApprovalTargetType.Incident || request.PropertyId is null
            || !int.TryParse(request.TargetKey, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw Invalid();
        await IncidentPhotoService.LockIncidentAsync(db, id, ct);
        if (await scope.ResolveAsync(id, new StorageCaller(request.RequestedByUserId, Roles.QuanLy), ct) != IncidentFileAccess.Manage
            || !await (from i in db.MaintenanceIncidents join r in db.Rooms on i.RoomId equals r.Id
                where i.Id == id && r.PropertyId == request.PropertyId select i.Id).AnyAsync(ct)) throw Invalid();
        var photos = await db.IncidentPhotoAttachments.Include(p => p.File)
            .Where(p => p.ApprovalRequestId == request.Id).ToListAsync(ct);
        IncidentResultPhotoPayload? payload;
        try { payload = JsonSerializer.Deserialize<IncidentResultPhotoPayload>(request.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)); }
        catch (JsonException) { throw Invalid(); }
        if (payload is null || payload.ExpectedPhotoCount is < 1 or > 3) throw Invalid();
        if (photos.Count != payload.ExpectedPhotoCount)
            throw new ApprovalApplyException("ApprovalPhotosIncomplete", "Cần tải đủ ảnh của yêu cầu trước khi duyệt.");
        if (photos.Any(p => p.IncidentId != id || p.Kind != IncidentPhotoKind.Result || p.File.IsDeleted)) throw Invalid();
        var count = await db.IncidentPhotoAttachments.CountAsync(p => p.IncidentId == id && p.Kind == IncidentPhotoKind.Result
            && !p.File.IsDeleted && (p.ApprovalRequestId == request.Id || p.ApprovalRequest == null
                || p.ApprovalRequest.Status == ApprovalRequestStatus.Pending || p.ApprovalRequest.Status == ApprovalRequestStatus.Approved), ct);
        if (count > IncidentPhotoRules.MaxCount) throw Invalid();
        // Queue ghi Approved cùng transaction; từ thời điểm commit ảnh mới được công bố.
    }

    private static ApprovalApplyException Invalid() => new(IncidentErrorCodes.InvalidRequest, "Ảnh kết quả không còn hợp lệ.");
}
