using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Data;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Rules;
using Web.Features.Properties;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Features.Storage;
using Web.Features.Storage.DTOs;
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Mapping;
using Web.Features.Storage.Rules;
using Web.Features.Storage.Services;
using Web.Infrastructure.Storage;

namespace Web.Features.Maintenance.Services;

// Dùng chung cho Blazor và /api/files; mọi quyết định nhóm ảnh/duyệt nằm ở server.
public sealed class IncidentPhotoService(
    IDbContextFactory<ApplicationDbContext> factory, IFileStorage storage,
    IIncidentFileScope scope, IManagerTogglePolicy toggles, IManagerApprovalService approvals,
    IOptions<StorageOptions> options, TimeProvider clock, IFileAccessAuditWriter audit,
    ILogger<IncidentPhotoService> logger)
{
    public async Task<UploadResult> UploadAsync(UploadFileRequest input, StorageCaller caller, CancellationToken ct = default)
    {
        var result = await UploadCoreAsync(input, caller, ct);
        await audit.WriteAsync(new(caller.UserId, caller.Role, FileAccessAction.Upload, result.File?.Id,
            FileCategory.IncidentPhoto, input.RelatedEntityType, input.RelatedEntityId, result.Succeeded, result.ErrorCode), ct);
        return result;
    }

    private async Task<UploadResult> UploadCoreAsync(UploadFileRequest input, StorageCaller caller, CancellationToken ct)
    {
        Guid? approvalId = input.ApprovalRequestId;
        UploadResult Fail(string code) => new() { Succeeded = false, ErrorCode = code, ErrorMessage = code, ApprovalRequestId = approvalId };
        if (caller.IsAdmin || string.IsNullOrWhiteSpace(caller.UserId)
            || !(caller.IsChuTro || caller.IsQuanLy || caller.IsKhachThue)) return Fail(StorageErrorCodes.Forbidden);
        if (!Enum.IsDefined(input.IncidentPhotoKind) || input.ExpectedPhotoCount is < 1 or > 3 || input.FileCategory != FileCategory.IncidentPhoto
            || input.File is null || input.Description?.Length > 500) return Fail(StorageErrorCodes.InvalidRequest);
        var extension = FileTypeRules.GetExtension(input.File.FileName);
        if (!IncidentPhotoRules.AllowedExtension(extension)) return Fail(StorageErrorCodes.InvalidFileType);
        var maxBytes = Math.Min(IncidentPhotoRules.MaxBytes, StorageLimits.MaxBytes(options.Value, FileCategory.IncidentPhoto));
        if (input.File.Length <= 0 || input.File.Length > maxBytes) return Fail(StorageErrorCodes.FileSizeExceeded);

        // Đọc hữu hạn kể cả stream khai báo Length sai; không nhận dữ liệu nhị phân trong payload duyệt.
        byte[] bytes;
        try
        {
            await using var source = input.File.OpenReadStream();
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await source.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > maxBytes) return Fail(StorageErrorCodes.FileSizeExceeded);
                await buffer.WriteAsync(chunk.AsMemory(0, read), ct);
            }
            bytes = buffer.ToArray();
        }
        catch (IOException) { return Fail(StorageErrorCodes.UploadFailed); }
        var mime = FileContentSniffer.DetectContentType(bytes);
        if (bytes.Length == 0 || !FileContentSniffer.IsConsistentWithExtension(extension, mime)) return Fail(StorageErrorCodes.InvalidFileType);

        int? incidentId = null;
        await using var db = await factory.CreateDbContextAsync(ct);
        if (input.RelatedEntityType == RelatedEntityType.IncidentApproval)
        {
            if (input.IncidentPhotoKind != IncidentPhotoKind.Report || !Guid.TryParse(input.RelatedEntityId, out var requestKey)
                || (approvalId is not null && approvalId != requestKey)) return Fail(StorageErrorCodes.InvalidRequest);
            approvalId = requestKey;
            if (!await CanUseApprovalAsync(db, requestKey, caller, true, ct)) return Fail(StorageErrorCodes.Forbidden);
        }
        else if (input.RelatedEntityType == RelatedEntityType.MaintenanceIncident
            && int.TryParse(input.RelatedEntityId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0)
        {
            incidentId = id;
            var access = await scope.ResolveAsync(id, caller, ct);
            if (access == IncidentFileAccess.None) return Fail(StorageErrorCodes.Forbidden);
            // Quản lý chỉ bổ sung ảnh báo của phiếu mình vừa tạo; ảnh xử lý phải qua SW-14.
            if (input.IncidentPhotoKind == IncidentPhotoKind.Report)
            {
                if (approvalId is not null || !await db.MaintenanceIncidents.AnyAsync(i => i.Id == id
                    && (!caller.IsQuanLy || i.ReportedByUserId == caller.UserId) && i.Status == IncidentStatus.New, ct)) return Fail(StorageErrorCodes.Forbidden);
            }
            else
            {
                if (access != IncidentFileAccess.Manage) return Fail(StorageErrorCodes.Forbidden);
                var decision = caller.IsChuTro ? RoomChangeDecision.Allowed : await toggles.EvaluateAsync("SW-14", caller.Role, ct);
                if (decision is not (RoomChangeDecision.Allowed or RoomChangeDecision.RequiresApproval)) return Fail(StorageErrorCodes.Forbidden);
                if (decision == RoomChangeDecision.RequiresApproval && approvalId is null)
                {
                    var created = await approvals.CreateAsync(new CreateApprovalRequest
                    {
                        OperationCode = "SW-14", TargetType = ApprovalTargetType.Incident,
                        TargetKey = id.ToString(CultureInfo.InvariantCulture),
                        PropertyId = await (from i in db.MaintenanceIncidents join r in db.Rooms on i.RoomId equals r.Id
                            where i.Id == id select r.PropertyId).SingleAsync(ct),
                        PayloadJson = JsonSerializer.Serialize(new IncidentResultPhotoPayload(input.ExpectedPhotoCount), new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    }, new ApprovalCaller(caller.UserId, caller.Role), ct);
                    if (!created.Succeeded) return Fail(created.ErrorCode ?? IncidentErrorCodes.ApprovalFailed);
                    approvalId = created.Request!.Id;
                }
                else if (decision == RoomChangeDecision.Allowed && approvalId is not null) return Fail(StorageErrorCodes.InvalidRequest);
            }
        }
        else return Fail(StorageErrorCodes.InvalidRequest);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (approvalId is Guid pendingId)
        {
            // Cùng khóa với ManagerApprovalService: upload không lọt qua approve/reject/cancel.
            var pending = await db.ManagerApprovalRequests.FromSqlInterpolated(
                $"SELECT * FROM [ManagerApprovalRequests] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {pendingId}").SingleOrDefaultAsync(ct);
            if (pending is null || pending.Status != ApprovalRequestStatus.Pending || pending.RequestedByUserId != caller.UserId
                || !await CanUseApprovalAsync(db, pendingId, caller, true, ct)
                || (incidentId is null && pending.OperationCode != "SW-12")
                || (incidentId is not null && (pending.OperationCode != "SW-14" || pending.TargetKey != incidentId.Value.ToString(CultureInfo.InvariantCulture))))
                return Fail(StorageErrorCodes.Forbidden);
            int expected;
            try
            {
                var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
                expected = pending.OperationCode == "SW-12"
                    ? JsonSerializer.Deserialize<IncidentCreatePayload>(pending.PayloadJson, jsonOptions)?.ExpectedPhotoCount ?? 0
                    : JsonSerializer.Deserialize<IncidentResultPhotoPayload>(pending.PayloadJson, jsonOptions)?.ExpectedPhotoCount ?? 0;
            }
            catch (JsonException) { return Fail(StorageErrorCodes.InvalidRequest); }
            var attached = await db.IncidentPhotoAttachments.CountAsync(p => p.ApprovalRequestId == pendingId && !p.File.IsDeleted, ct);
            if (expected is < 1 or > 3 || attached >= expected) return Fail(StorageErrorCodes.FileCountExceeded);
        }
        if (incidentId is int incident)
        {
            await LockIncidentAsync(db, incident, ct);
            if (await scope.ResolveAsync(incident, caller, ct) == IncidentFileAccess.None) return Fail(StorageErrorCodes.Forbidden);
        }

        var photos = db.IncidentPhotoAttachments.Where(p => incidentId != null
            ? p.IncidentId == incidentId : p.ApprovalRequestId == approvalId);
        var count = await photos.CountAsync(p => p.Kind == input.IncidentPhotoKind && !p.File.IsDeleted
            && (p.ApprovalRequest == null || p.ApprovalRequest.Status == ApprovalRequestStatus.Pending
                || p.ApprovalRequest.Status == ApprovalRequestStatus.Approved), ct);
        if (incidentId is int legacyIncident && input.IncidentPhotoKind == IncidentPhotoKind.Report)
        {
            var key = legacyIncident.ToString(CultureInfo.InvariantCulture);
            count += await db.StoredFiles.CountAsync(f => f.FileCategory == FileCategory.IncidentPhoto
                && f.RelatedEntityType == RelatedEntityType.MaintenanceIncident && f.RelatedEntityId == key
                && !db.IncidentPhotoAttachments.Any(p => p.FileId == f.Id), ct);
        }
        if (count >= IncidentPhotoRules.MaxCount) return Fail(StorageErrorCodes.FileCountExceeded);

        StoredFileWriteResult? written = null;
        try
        {
            var now = clock.GetUtcNow();
            written = await storage.SaveAsync(bytes, extension, now, ct);
            var file = new StoredFile
            {
                FileName = Path.GetFileName(input.File.FileName.Replace('\\', '/')),
                StoredFileName = written.StoredFileName, RelativePath = written.RelativePath,
                ContentType = mime!, FileSizeBytes = bytes.Length, FileCategory = FileCategory.IncidentPhoto,
                UploadedByUserId = caller.UserId, UploadedAt = now,
                RelatedEntityType = incidentId is null ? RelatedEntityType.IncidentApproval : RelatedEntityType.MaintenanceIncident,
                RelatedEntityId = incidentId?.ToString(CultureInfo.InvariantCulture) ?? approvalId!.Value.ToString("D"),
                Description = input.Description?.Trim()
            };
            if (file.FileName.Length > 260) file.FileName = file.Id.ToString("N") + extension;
            db.IncidentPhotoAttachments.Add(new() { File = file, FileId = file.Id, Kind = input.IncidentPhotoKind,
                IncidentId = incidentId, ApprovalRequestId = approvalId });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return UploadResult.Success(StoredFileMapping.ToMetadata(file), approvalId);
        }
        catch (Exception ex)
        {
            if (written is not null)
            {
                try { storage.Delete(written.RelativePath); }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { logger.LogError(cleanup, "Không dọn được ảnh mới sau lỗi lưu metadata P7."); }
            }
            logger.LogError(ex, "Không lưu được ảnh sự cố.");
            if (ex is OperationCanceledException) throw;
            return Fail(StorageErrorCodes.UploadFailed);
        }
    }

    public static async Task LockIncidentAsync(ApplicationDbContext db, int id, CancellationToken ct)
    {
        // Khóa hàng phiếu giữ tới hết transaction, tuần tự hóa quota của hai nhóm ảnh.
        _ = await db.MaintenanceIncidents.FromSqlInterpolated(
            $"SELECT * FROM [MaintenanceIncidents] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {id}").SingleOrDefaultAsync(ct);
    }

    public async Task<bool> CanUseApprovalAsync(ApplicationDbContext db, Guid id, StorageCaller caller, bool writing, CancellationToken ct)
    {
        if (caller.IsAdmin || !(caller.IsChuTro || caller.IsQuanLy)) return false;
        var request = await db.ManagerApprovalRequests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, ct);
        if (request is null || request.OperationCode is not ("SW-12" or "SW-14")
            || request.Status != ApprovalRequestStatus.Pending || request.PropertyId is null) return false;
        if (writing && (caller.UserId != request.RequestedByUserId || !caller.IsQuanLy)) return false;
        if (!caller.IsChuTro && caller.UserId != request.RequestedByUserId) return false;
        return await PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(),
            new PropertyCaller(caller.UserId, caller.Role)).AnyAsync(p => p.Id == request.PropertyId, ct);
    }

    public async Task<bool> CanReadAsync(ApplicationDbContext db, StoredFile file, StorageCaller caller, CancellationToken ct)
    {
        if (caller.IsAdmin) return false;
        var photo = await db.IncidentPhotoAttachments.AsNoTracking().Include(p => p.ApprovalRequest).SingleOrDefaultAsync(p => p.FileId == file.Id, ct);
        if (photo is null) return file.RelatedEntityType == RelatedEntityType.MaintenanceIncident
            && int.TryParse(file.RelatedEntityId, out var oldId) && await scope.ResolveAsync(oldId, caller, ct) != IncidentFileAccess.None;
        if (!IncidentPhotoRules.IsEffective(photo.ApprovalRequest?.Status)) return false;
        if (!IncidentPhotoRules.IsPublished(photo.ApprovalRequest?.Status))
            return await CanUseApprovalAsync(db, photo.ApprovalRequestId!.Value, caller, false, ct);
        return photo.IncidentId is int id && await scope.ResolveAsync(id, caller, ct) != IncidentFileAccess.None;
    }

    public async Task<IReadOnlyList<(StoredFile File, IncidentPhotoKind Kind, bool Pending)>> ListAsync(
        int? incidentId, Guid? approvalId, StorageCaller caller, CancellationToken ct = default)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        if (incidentId is int id && await scope.ResolveAsync(id, caller, ct) == IncidentFileAccess.None) return [];
        if (incidentId is null && (approvalId is not Guid requestId || !await CanUseApprovalAsync(db, requestId, caller, false, ct))) return [];
        var photos = await db.IncidentPhotoAttachments.AsNoTracking().Include(p => p.File).Include(p => p.ApprovalRequest)
            .Where(p => incidentId != null ? p.IncidentId == incidentId : p.ApprovalRequestId == approvalId).ToListAsync(ct);
        var result = new List<(StoredFile, IncidentPhotoKind, bool)>();
        foreach (var photo in photos)
            if (!photo.File.IsDeleted && await CanReadAsync(db, photo.File, caller, ct))
                result.Add((photo.File, photo.Kind, photo.ApprovalRequest?.Status == ApprovalRequestStatus.Pending));
        if (incidentId is int legacyId)
        {
            var key = legacyId.ToString(CultureInfo.InvariantCulture);
            var legacy = await db.StoredFiles.AsNoTracking().Where(f => f.FileCategory == FileCategory.IncidentPhoto
                && f.RelatedEntityType == RelatedEntityType.MaintenanceIncident && f.RelatedEntityId == key
                && !db.IncidentPhotoAttachments.Any(p => p.FileId == f.Id)).ToListAsync(ct);
            result.AddRange(legacy.Select(f => (f, IncidentPhotoKind.Report, false)));
        }
        return result;
    }

}
