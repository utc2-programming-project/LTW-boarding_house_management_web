using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Models;
using Web.Features.Properties;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Enums;

namespace Web.Features.Maintenance.Services;

// Adapter UI giữ caller đáng tin cậy; không chứa dữ liệu mẫu hoặc state nghiệp vụ theo phiên.
public sealed class IncidentPageService(AuthenticationStateProvider authentication, IIncidentService incidents,
    IncidentPhotoService photos, IManagerTogglePolicy toggles, IManagerApprovalService approvals)
{
    public async Task<PropertyCaller> CallerAsync()
    {
        var user = (await authentication.GetAuthenticationStateAsync()).User;
        if (!IncidentAccess.IsAllowed(user)) throw new IncidentPageException("AccessDenied");
        var storage = StorageCaller.FromPrincipal(user);
        return new(storage.UserId, storage.Role);
    }
    public async Task<IReadOnlyList<IncidentRoomDto>> RoomsAsync() => await incidents.RoomsAsync(await CallerAsync());
    public async Task<string> RequestRoomLabelAsync(ApprovalRequestDto request)
    {
        if (!int.TryParse(request.TargetKey, out var id)) return "";
        if (request.OperationCode == "SW-12")
        {
            var room = (await RoomsAsync()).FirstOrDefault(r => r.Id == id);
            return room is null ? "" : $"{room.PropertyName} / {room.Name}";
        }
        var result = await incidents.GetAsync(id, await CallerAsync());
        return result.Incident is { } i ? $"{i.PropertyName} / {i.RoomCode}" : "";
    }
    public async Task<IncidentMode> ModeAsync(IncidentAction action)
    {
        var caller = await CallerAsync();
        if (caller.IsChuTro || (caller.IsKhachThue && action == IncidentAction.Create)) return IncidentMode.Allow;
        if (!caller.IsQuanLy) return IncidentMode.Deny;
        return (await toggles.EvaluateAsync(action switch { IncidentAction.Create => "SW-12", IncidentAction.Update => "SW-13", _ => "SW-14" }, caller.Role)) switch
        {
            RoomChangeDecision.Allowed => IncidentMode.Allow,
            RoomChangeDecision.RequiresApproval => IncidentMode.Approval,
            _ => IncidentMode.Deny
        };
    }
    public async Task<IReadOnlyList<ApprovalRequestDto>> RequestsAsync()
    {
        var caller = await CallerAsync();
        if (caller.IsKhachThue) return [];
        var result = caller.IsChuTro ? await approvals.ListInboxAsync(new(caller.UserId, caller.Role))
            : await approvals.ListMineAsync(new(caller.UserId, caller.Role));
        if (!result.Succeeded) throw new IncidentPageException(result.ErrorCode ?? "LoadFailed");
        return result.Items.Where(r => r.OperationCode is "SW-12" or "SW-13" or "SW-14").ToArray();
    }
    public async Task<IReadOnlyList<IncidentItem>> ListAsync()
    {
        var caller = await CallerAsync();
        return (await incidents.ListAsync(caller)).Select(i => new IncidentItem(i.Id, i.RoomId, i.Title, "", i.Category, i.Urgency,
            i.Status, i.CreatedAt, "", [], [], [], RoomLabel: $"{i.PropertyName} / {i.RoomCode}")).ToArray();
    }
    public async Task<IncidentItem> DetailAsync(int id)
    {
        var caller = await CallerAsync();
        var result = await incidents.GetAsync(id, caller);
        if (!result.Succeeded || result.Incident is null) throw new IncidentPageException(result.ErrorCode ?? "LoadFailed");
        var i = result.Incident;
        var images = await PhotosAsync(id, null);
        var requests = (await RequestsAsync()).Where(r => r.TargetType == ApprovalTargetType.Incident
            && r.TargetKey == id.ToString(CultureInfo.InvariantCulture) && r.Status == ApprovalRequestStatus.Pending);
        var pending = requests.Select(r =>
        {
            var payload = r.OperationCode == "SW-13" ? JsonSerializer.Deserialize<IncidentUpdatePayload>(r.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) : null;
            var expected = r.OperationCode == "SW-14" ? JsonSerializer.Deserialize<IncidentResultPhotoPayload>(r.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))?.ExpectedPhotoCount ?? 0 : 0;
            return new IncidentPending(r.OperationCode == "SW-13" ? IncidentAction.Update : IncidentAction.ResultPhoto,
                payload?.Status, payload?.TechnicalNotes ?? "", images.Where(p => p.Pending).ToArray(), r.RequestedAt,
                payload?.AssigneeName ?? "", payload?.RepairCost, expected) { Id = r.Id };
        }).ToArray();
        return new(i.Id, i.RoomId, i.Title, i.Description, i.Category, i.Urgency, i.Status, i.CreatedAt, i.TechnicalNotes ?? "",
            images.Where(p => !p.Pending).ToArray(), i.History.Select(h => new IncidentHistory(h.CreatedAt,
                "ActorSystem", $"History{h.Action}")).ToArray(), pending, i.AssigneeName ?? "", i.RepairCost, $"{i.PropertyName} / {i.RoomCode}");
    }

    public async Task<bool> CreateAsync(IncidentForm form)
    {
        var caller = await CallerAsync();
        if (!form.Saved)
        {
            var result = await incidents.CreateAsync(new IncidentCreateRequest { RoomId = form.RoomId, Title = form.Title,
                Description = form.Description, Category = form.Category, Urgency = form.Urgency, ExpectedPhotoCount = form.Photos.Count }, caller);
            if (!result.Succeeded) throw new IncidentPageException(result.ErrorCode ?? "SaveFailed");
            // Lưu ID trước upload: retry trên form này tuyệt đối không gọi Create lần hai.
            form.SavedIncidentId = result.Incident?.Id;
            form.SavedApprovalId = result.ApprovalRequest?.Id;
        }
        var remaining = form.Photos.ToList();
        form.PhotoErrors.Clear();
        foreach (var selected in form.Photos.ToArray())
        {
            var result = await UploadAsync(form.SavedIncidentId, form.SavedApprovalId, IncidentPhotoKind.Report, selected);
            if (result.Succeeded) remaining.Remove(selected);
            else form.PhotoErrors[selected.Name] = result.ErrorCode ?? "UploadFailed";
            form.Photos = remaining.ToArray();
        }
        form.Photos = remaining;
        if (remaining.Count > 0) throw new IncidentPageException(form.SavedApprovalId is not null ? "RequestSavedPhotosFailed" : "IncidentSavedPhotosFailed");
        return form.SavedApprovalId is not null;
    }
    public async Task<bool> UpdateAsync(int id, IncidentStatus next, string notes, string assignee, decimal? cost)
    {
        var result = await incidents.UpdateAsync(id, new() { Status = next, TechnicalNotes = notes, AssigneeName = assignee, RepairCost = cost }, await CallerAsync());
        if (!result.Succeeded) throw new IncidentPageException(result.ErrorCode ?? "SaveFailed");
        return result.ApprovalRequest is not null;
    }
    public async Task<UploadResult> UploadAsync(int? id, Guid? requestId, IncidentPhotoKind kind, IncidentSelectedPhoto selected, int expectedPhotoCount = 1)
    {
        var caller = await CallerAsync();
        using var stream = new MemoryStream(selected.Content, false);
        return await photos.UploadAsync(new UploadFileRequest
        {
            File = new FormFile(stream, 0, stream.Length, "file", selected.Name), FileCategory = FileCategory.IncidentPhoto,
            RelatedEntityType = id is null ? RelatedEntityType.IncidentApproval : RelatedEntityType.MaintenanceIncident,
            RelatedEntityId = id?.ToString(CultureInfo.InvariantCulture) ?? requestId!.Value.ToString("D"),
            IncidentPhotoKind = kind, ApprovalRequestId = requestId, ExpectedPhotoCount = expectedPhotoCount
        }, new(caller.UserId, caller.Role));
    }
    public async Task<IReadOnlyList<IncidentStoredPhoto>> PhotosAsync(int? id, Guid? requestId)
    {
        var caller = await CallerAsync();
        return (await photos.ListAsync(id, requestId, new(caller.UserId, caller.Role)))
            .Select(p => new IncidentStoredPhoto(p.File.Id, p.File.FileName, p.Kind == IncidentPhotoKind.Result, p.Pending)).ToArray();
    }
    public async Task DecideAsync(Guid id, bool? approve, string reason = "")
    {
        var caller = await CallerAsync();
        var actor = new ApprovalCaller(caller.UserId, caller.Role);
        var result = approve is null ? await approvals.CancelAsync(id, actor) : approve.Value
            ? await approvals.ApproveAsync(id, actor) : await approvals.RejectAsync(id, new() { Reason = reason }, actor);
        if (!result.Succeeded) throw new IncidentPageException(result.ErrorCode ?? "SaveFailed");
    }
}
