using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Web.Common.Models;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Audit.Entities;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts.Enums;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Rules;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Rooms;
using Web.Features.Settings.Services;
using Web.Features.Storage.Entities;
using Web.Features.Storage.Enums;
using Web.Infrastructure.Storage;

namespace Web.Features.Maintenance.Services;

public sealed class IncidentService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    IManagerTogglePolicy togglePolicy,
    IManagerApprovalService approvals,
    IFileStorage? fileStorage = null) : IIncidentService
{
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<IncidentRoomDto>> RoomsAsync(PropertyCaller caller, CancellationToken cancellationToken = default)
    {
        if (!CanCreate(caller)) return [];
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = db.Rooms.AsNoTracking().Where(r => r.IsActive && !r.IsDeleted);
        if (caller.IsKhachThue)
        {
            var today = Today();
            var rooms = from member in db.ContractMembers.AsNoTracking()
                        join tenant in db.Tenants.AsNoTracking() on member.TenantId equals tenant.Id
                        join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                        where tenant.UserId == caller.UserId && contract.Status == ContractStatus.Approved
                            && contract.StartDate <= today && contract.EndDate >= today
                        select contract.RoomId;
            candidates = candidates.Where(r => rooms.Contains(r.Id));
        }
        else
        {
            var properties = PropertyScope.Apply(db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), caller);
            candidates = candidates.Where(r => properties.Any(p => p.Id == r.PropertyId));
        }
        return await (from room in candidates join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            orderby property.Name, room.Code select new IncidentRoomDto(room.Id, property.Id, property.Name, room.Code)).ToListAsync(cancellationToken);
    }

    public async Task<IncidentResult> CreateAsync(
        IncidentCreateRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        var invalid = IncidentRules.ValidateCreate(request);
        if (invalid is not null)
        {
            return IncidentResult.Failure(invalid);
        }

        if (!CanCreate(caller))
        {
            return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var today = Today();
        var room = await FindCreatableRoomAsync(db, request.RoomId, caller, today, cancellationToken);
        if (room is null)
        {
            return IncidentResult.Failure(IncidentErrorCodes.RoomNotFound);
        }

        if (caller.IsQuanLy)
        {
            var decision = await togglePolicy.EvaluateAsync("SW-12", caller.Role, cancellationToken);
            if (decision == RoomChangeDecision.Denied)
            {
                return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
            }

            if (decision is not RoomChangeDecision.Allowed and not RoomChangeDecision.RequiresApproval)
            {
                return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
            }

            if (decision == RoomChangeDecision.RequiresApproval)
            {
                var payload = JsonSerializer.Serialize(new IncidentCreatePayload(
                    request.Title.Trim(), request.Description.Trim(), request.Category, request.Urgency, request.ExpectedPhotoCount), PayloadJsonOptions);
                var approval = await approvals.CreateAsync(new CreateApprovalRequest
                {
                    OperationCode = "SW-12",
                    TargetType = ApprovalTargetType.Room,
                    TargetKey = room.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    PropertyId = room.PropertyId,
                    PayloadJson = payload,
                }, new ApprovalCaller(caller.UserId, caller.Role), cancellationToken);

                return approval.Succeeded && approval.Request is not null
                    ? IncidentResult.ApprovalCreated(approval.Request)
                    : IncidentResult.Failure(approval.ErrorCode ?? IncidentErrorCodes.ApprovalFailed);
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var incident = new MaintenanceIncident
        {
            RoomId = room.Id,
            ReportedByUserId = caller.UserId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category,
            Urgency = request.Urgency,
            Status = IncidentStatus.New,
            CreatedAt = now,
        };
        incident.History.Add(new MaintenanceIncidentHistory
        {
            Action = IncidentHistoryAction.Created,
            FromStatus = null,
            ToStatus = IncidentStatus.New,
            ActorUserId = caller.UserId,
            CreatedAt = now,
        });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        db.MaintenanceIncidents.Add(incident);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return IncidentResult.Success(await ToDetailAsync(db, incident.Id, caller, cancellationToken));
    }

    public async Task<IReadOnlyList<IncidentListItemDto>> ListAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!CanView(caller))
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var visible = InScopeIncidents(db, caller, Today());
        return await (
            from incident in visible
            join room in db.Rooms.AsNoTracking() on incident.RoomId equals room.Id
            join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            orderby incident.CreatedAt descending, incident.Id descending
            select new IncidentListItemDto(
                incident.Id,
                room.Id,
                property.Id,
                property.Name,
                room.Code,
                incident.Title,
                incident.Category,
                incident.Urgency,
                incident.Status,
                incident.CreatedAt,
                incident.ReportedByUserId))
            .ToListAsync(cancellationToken);
    }

    public async Task<IncidentResult> GetAsync(
        int incidentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (incidentId <= 0 || !CanView(caller))
        {
            return IncidentResult.Failure(IncidentErrorCodes.NotFound);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await InScopeIncidents(db, caller, Today())
            .AnyAsync(incident => incident.Id == incidentId, cancellationToken);
        return exists
            ? IncidentResult.Success(await ToDetailAsync(db, incidentId, caller, cancellationToken))
            : IncidentResult.Failure(IncidentErrorCodes.NotFound);
    }

    public async Task<IncidentResult> UpdateAsync(
        int incidentId,
        IncidentUpdateRequest request,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!CanUpdate(caller))
        {
            return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
        }

        var invalid = IncidentRules.ValidateUpdate(request);
        if (invalid is not null)
        {
            return IncidentResult.Failure(invalid);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var incident = await InScopeIncidents(db, caller, Today())
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return IncidentResult.Failure(IncidentErrorCodes.NotFound);
        }

        var statusChanged = incident.Status != request.Status;
        if (statusChanged && !IncidentRules.CanTransition(incident.Status, request.Status))
        {
            return IncidentResult.Failure(IncidentErrorCodes.InvalidTransition);
        }

        var room = await db.Rooms.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == incident.RoomId, cancellationToken);
        if (room is null)
        {
            return IncidentResult.Failure(IncidentErrorCodes.NotFound);
        }

        if (caller.IsQuanLy)
        {
            var decision = await togglePolicy.EvaluateAsync("SW-13", caller.Role, cancellationToken);
            if (decision == RoomChangeDecision.Denied)
            {
                return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
            }

            if (decision is not RoomChangeDecision.Allowed and not RoomChangeDecision.RequiresApproval)
            {
                return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
            }

            if (decision == RoomChangeDecision.RequiresApproval)
            {
                var payload = JsonSerializer.Serialize(new IncidentUpdatePayload(
                    request.Status, request.TechnicalNotes?.Trim(), request.AssigneeName?.Trim(), request.RepairCost),
                    PayloadJsonOptions);
                var approval = await approvals.CreateAsync(new CreateApprovalRequest
                {
                    OperationCode = "SW-13",
                    TargetType = ApprovalTargetType.Incident,
                    TargetKey = incident.Id.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    PropertyId = room.PropertyId,
                    PayloadJson = payload,
                    ExpectedToken = incident.RowVersion,
                }, new ApprovalCaller(caller.UserId, caller.Role), cancellationToken);

                return approval.Succeeded && approval.Request is not null
                    ? IncidentResult.ApprovalCreated(approval.Request)
                    : IncidentResult.Failure(approval.ErrorCode ?? IncidentErrorCodes.ApprovalFailed);
            }
        }

        var from = incident.Status;
        var now = clock.GetUtcNow().UtcDateTime;
        incident.Status = request.Status;
        incident.AssigneeName = request.AssigneeName is null
            ? incident.AssigneeName
            : NormalizeOptional(request.AssigneeName);
        incident.TechnicalNotes = request.TechnicalNotes is null
            ? incident.TechnicalNotes
            : NormalizeOptional(request.TechnicalNotes);
        incident.RepairCost = request.RepairCost ?? incident.RepairCost;
        incident.UpdatedAt = now;
        db.Entry(incident).Property(item => item.UpdatedAt).IsModified = true;
        db.MaintenanceIncidentHistories.Add(new MaintenanceIncidentHistory
        {
            IncidentId = incident.Id,
            Action = statusChanged ? IncidentHistoryAction.StatusChanged : IncidentHistoryAction.Updated,
            FromStatus = from,
            ToStatus = request.Status,
            ActorUserId = caller.UserId,
            Note = incident.TechnicalNotes,
            CreatedAt = now,
        });

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IncidentResult.Failure(IncidentErrorCodes.Conflict);
        }

        return IncidentResult.Success(await ToDetailAsync(db, incident.Id, caller, cancellationToken));
    }

    public async Task<IncidentResult> DeleteAsync(
        int incidentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!CanUpdate(caller))
        {
            return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var incident = await InScopeIncidents(db, caller, Today())
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        if (incident is null)
        {
            return IncidentResult.Failure(IncidentErrorCodes.NotFound);
        }

        var hasPendingApproval = await db.IncidentPhotoAttachments
            .AnyAsync(item => item.IncidentId == incidentId && item.ApprovalRequestId != null, cancellationToken);
        if (hasPendingApproval)
        {
            return IncidentResult.Failure(IncidentErrorCodes.HasPendingApproval);
        }

        var detail = await ToDetailAsync(db, incidentId, caller, cancellationToken);
        var now = clock.GetUtcNow().UtcDateTime;
        incident.IsDeleted = true;
        incident.DeletedAt = now;
        incident.DeletedBy = caller.UserId;
        incident.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return IncidentResult.Success(detail);
    }

    public async Task<IncidentResult> RestoreAsync(
        int incidentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro)
        {
            return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.MaintenanceIncidents
            .IgnoreQueryFilters()
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == incidentId && item.IsDeleted, cancellationToken);
        if (incident is null)
        {
            return IncidentResult.Failure(IncidentErrorCodes.NotInTrash);
        }

        var propertyId = await OwnedPropertyIdAsync(db, incident.RoomId, caller.UserId, cancellationToken);
        if (propertyId is null)
        {
            var roomExists = await db.Rooms.IgnoreQueryFilters()
                .AnyAsync(room => room.Id == incident.RoomId, cancellationToken);
            return IncidentResult.Failure(roomExists ? IncidentErrorCodes.Forbidden : IncidentErrorCodes.RoomDeleted);
        }

        var roomAlive = await db.Rooms.AnyAsync(room => room.Id == incident.RoomId, cancellationToken);
        if (!roomAlive)
        {
            return IncidentResult.Failure(IncidentErrorCodes.RoomDeleted);
        }

        incident.IsDeleted = false;
        incident.DeletedAt = null;
        incident.DeletedBy = null;
        incident.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return IncidentResult.Success(await ToDetailAsync(db, incident.Id, caller, cancellationToken));
    }

    public async Task<IReadOnlyList<IncidentTrashItemDto>> ListDeletedAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro)
        {
            return [];
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await (
                from incident in db.MaintenanceIncidents.IgnoreQueryFilters().AsNoTracking()
                join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on incident.RoomId equals room.Id
                join property in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
                where incident.IsDeleted && property.OwnerId == caller.UserId
                orderby incident.DeletedAt descending, incident.Id descending
                select new IncidentTrashItemDto(
                    incident.Id,
                    incident.RoomId,
                    property.Name,
                    room.Code,
                    incident.Title,
                    incident.Category,
                    incident.Status,
                    incident.CreatedAt,
                    incident.DeletedAt,
                    incident.DeletedBy))
            .ToListAsync(cancellationToken);
    }

    public async Task<IncidentResult> PurgeAsync(
        int incidentId,
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro)
        {
            return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var incident = await db.MaintenanceIncidents
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == incidentId && item.IsDeleted, cancellationToken);
        if (incident is null)
        {
            return IncidentResult.Failure(IncidentErrorCodes.NotInTrash);
        }

        var propertyId = await OwnedPropertyIdAsync(db, incident.RoomId, caller.UserId, cancellationToken);
        if (propertyId is null)
        {
            return IncidentResult.Failure(IncidentErrorCodes.Forbidden);
        }

        var hasPendingApproval = await db.IncidentPhotoAttachments
            .AnyAsync(item => item.IncidentId == incidentId && item.ApprovalRequestId != null, cancellationToken);
        if (hasPendingApproval)
        {
            return IncidentResult.Failure(IncidentErrorCodes.HasPendingApproval);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var history = await db.MaintenanceIncidentHistories
                .Where(item => item.IncidentId == incidentId)
                .ToListAsync(cancellationToken);
            var photos = await db.IncidentPhotoAttachments
                .Where(item => item.IncidentId == incidentId)
                .ToListAsync(cancellationToken);
            var fileIds = photos.Select(photo => photo.FileId).ToHashSet();
            var relatedId = incidentId.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var files = await db.StoredFiles
                .IgnoreQueryFilters()
                .Where(file => fileIds.Contains(file.Id)
                    || (file.RelatedEntityType == RelatedEntityType.MaintenanceIncident
                        && file.RelatedEntityId == relatedId))
                .ToListAsync(cancellationToken);

            db.MaintenanceIncidentHistories.RemoveRange(history);
            db.IncidentPhotoAttachments.RemoveRange(photos);
            db.StoredFiles.RemoveRange(files);
            db.MaintenanceIncidents.Remove(incident);
            db.AuditLogEntries.Add(new AuditLogEntry
            {
                Category = AuditLogCategory.Business,
                Action = "incident.purge",
                ActorUserId = caller.UserId,
                PropertyId = propertyId,
                Metadata = $"MaintenanceIncident #{incident.Id}",
                OccurredAt = DateTimeOffset.UtcNow,
            });

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            foreach (var file in files)
            {
                TryDeletePhysical(file.RelativePath);
            }

            return IncidentResult.Success();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return IncidentResult.Failure(IncidentErrorCodes.HasDependencies);
        }
    }

    public async Task<TrashPurgeSummary> PurgeAllAsync(
        PropertyCaller caller,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!caller.IsChuTro)
        {
            return new TrashPurgeSummary(0, 0);
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var ids = await (
                from incident in db.MaintenanceIncidents.IgnoreQueryFilters().AsNoTracking()
                join room in db.Rooms.IgnoreQueryFilters().AsNoTracking() on incident.RoomId equals room.Id
                join property in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
                where incident.IsDeleted && property.OwnerId == caller.UserId
                select incident.Id)
            .ToListAsync(cancellationToken);

        var purged = 0;
        var skipped = 0;
        foreach (var id in ids)
        {
            try
            {
                var result = await PurgeAsync(id, caller, cancellationToken);
                if (result.Succeeded) purged++;
                else skipped++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                skipped++;
            }
        }

        return new TrashPurgeSummary(purged, skipped);
    }

    private static Task<int?> OwnedPropertyIdAsync(
        ApplicationDbContext db,
        int roomId,
        string ownerId,
        CancellationToken cancellationToken) =>
        (from room in db.Rooms.IgnoreQueryFilters().AsNoTracking()
         join property in db.Properties.IgnoreQueryFilters().AsNoTracking() on room.PropertyId equals property.Id
         where room.Id == roomId && property.OwnerId == ownerId
         select (int?)property.Id)
        .FirstOrDefaultAsync(cancellationToken);

    private void TryDeletePhysical(string relativePath)
    {
        if (fileStorage is null)
        {
            return;
        }

        try
        {
            fileStorage.Delete(relativePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tệp đã mất hoặc không xóa được: metadata vẫn bị xóa để không giữ tham chiếu mồ côi.
        }
    }

    private static bool CanCreate(PropertyCaller caller) =>
        !string.IsNullOrWhiteSpace(caller.UserId)
        && (caller.IsKhachThue || caller.IsQuanLy || caller.IsChuTro)
        && !caller.IsAdmin;

    private static bool CanView(PropertyCaller caller) =>
        !string.IsNullOrWhiteSpace(caller.UserId)
        && (caller.IsKhachThue || caller.IsQuanLy || caller.IsChuTro)
        && !caller.IsAdmin;

    private static bool CanUpdate(PropertyCaller caller) =>
        !string.IsNullOrWhiteSpace(caller.UserId)
        && (caller.IsQuanLy || caller.IsChuTro)
        && !caller.IsKhachThue
        && !caller.IsAdmin;

    private static async Task<Room?> FindCreatableRoomAsync(
        ApplicationDbContext db, int roomId, PropertyCaller caller, DateTime today, CancellationToken ct)
    {
        if (caller.IsKhachThue)
        {
            var ownTenantIds = db.Tenants.AsNoTracking()
                .Where(tenant => tenant.UserId == caller.UserId)
                .Select(tenant => tenant.Id);
            var activeTenantIds = ActiveTenantIds(db, today);
            var roomIds = from member in db.ContractMembers.AsNoTracking()
                          join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                          where ownTenantIds.Contains(member.TenantId)
                              && activeTenantIds.Contains(member.TenantId)
                              && contract.Status == ContractStatus.Approved
                              && contract.StartDate <= today && contract.EndDate >= today
                          select contract.RoomId;

            return await db.Rooms.AsNoTracking()
                .Where(room => room.Id == roomId && room.IsActive && !room.IsDeleted && roomIds.Contains(room.Id))
                .SingleOrDefaultAsync(ct);
        }

        if (!caller.IsQuanLy && !caller.IsChuTro)
        {
            return null;
        }

        var visibleProperties = PropertyScope.Apply(
            db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), caller);
        return await (
            from room in db.Rooms.AsNoTracking()
            join property in visibleProperties on room.PropertyId equals property.Id
            select room)
            .Where(room => room.Id == roomId && room.IsActive && !room.IsDeleted)
            .SingleOrDefaultAsync(ct);
    }

    private static IQueryable<int> ActiveTenantIds(ApplicationDbContext db, DateTime today) =>
        from member in db.ContractMembers.AsNoTracking()
        join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
        where contract.Status == ContractStatus.Approved
            && contract.StartDate <= today
            && contract.EndDate >= today
        select member.TenantId;

    private static IQueryable<MaintenanceIncident> InScopeIncidents(
        ApplicationDbContext db, PropertyCaller caller, DateTime today)
    {
        if (caller.IsChuTro || caller.IsQuanLy)
        {
            var properties = PropertyScope.Apply(
                db.Properties.AsNoTracking(), db.PropertyManagers.AsNoTracking(), caller);
            var roomIds = from room in db.Rooms.AsNoTracking()
                          join property in properties on room.PropertyId equals property.Id
                          select room.Id;
            return db.MaintenanceIncidents.AsNoTracking().Where(incident => roomIds.Contains(incident.RoomId));
        }

        if (!caller.IsKhachThue || string.IsNullOrWhiteSpace(caller.UserId))
        {
            return db.MaintenanceIncidents.AsNoTracking().Where(_ => false);
        }

        var ownTenantIds = db.Tenants.AsNoTracking()
            .Where(tenant => tenant.UserId == caller.UserId)
            .Select(tenant => tenant.Id);
        var activeTenantIds = ActiveTenantIds(db, today);
        var rentedRoomIds = from member in db.ContractMembers.AsNoTracking()
                            join contract in db.Contracts.AsNoTracking() on member.ContractId equals contract.ContractId
                            join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                            where ownTenantIds.Contains(member.TenantId)
                                && activeTenantIds.Contains(member.TenantId)
                                && contract.Status == ContractStatus.Approved
                                && contract.StartDate <= today && contract.EndDate >= today
                                && room.IsActive && !room.IsDeleted
                            select room.Id;
        return db.MaintenanceIncidents.AsNoTracking().Where(incident => rentedRoomIds.Contains(incident.RoomId));
    }

    private async Task<IncidentDetailDto> ToDetailAsync(
        ApplicationDbContext db, int incidentId, PropertyCaller caller, CancellationToken ct)
    {
        var row = await (
            from item in db.MaintenanceIncidents.AsNoTracking()
            join room in db.Rooms.AsNoTracking() on item.RoomId equals room.Id
            join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
            where item.Id == incidentId
            select new
            {
                Incident = item,
                RoomId = room.Id,
                PropertyId = property.Id,
                PropertyName = property.Name,
                RoomCode = room.Code,
            })
            .SingleAsync(ct);
        var maskInternalFields = caller.IsKhachThue;
        var history = await db.MaintenanceIncidentHistories.AsNoTracking()
            .Where(item => item.IncidentId == incidentId)
            .OrderBy(item => item.Id)
            .Select(item => new IncidentHistoryDto(
                item.Id, item.Action, item.FromStatus, item.ToStatus,
                maskInternalFields ? null : item.ActorUserId,
                maskInternalFields ? null : item.Note,
                item.CreatedAt))
            .ToListAsync(ct);
        var incident = row.Incident;
        var canReadResolution = !maskInternalFields ||
            (incident.Status == IncidentStatus.Completed && incident.ReportedByUserId == caller.UserId);

        return new IncidentDetailDto(
            incident.Id, row.RoomId, row.PropertyId, row.PropertyName, row.RoomCode, incident.ReportedByUserId,
            incident.Title, incident.Description, incident.Category, incident.Urgency, incident.Status,
            canReadResolution ? incident.AssigneeName : null,
            canReadResolution ? incident.TechnicalNotes : null,
            maskInternalFields ? null : incident.RepairCost,
            incident.CreatedAt, incident.UpdatedAt, incident.RowVersion, history);
    }

    private DateTime Today() => clock.GetUtcNow().UtcDateTime.Date;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
