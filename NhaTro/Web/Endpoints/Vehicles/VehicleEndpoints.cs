using System.Security.Claims;
using Web.Features.Accounts;
using Web.Features.Accounts.Owners;
using Web.Features.Properties;
using Web.Features.Vehicles;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Services;

namespace Web.Endpoints.Vehicles;

/// <summary>
/// HTTP API endpoints cho module Quản lý xe (/api/vehicles/*).
/// Tự động xác thực qua policy "Business" và kiểm tra phạm vi dữ liệu theo PropertyCaller.
/// </summary>
public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vehicles")
            .RequireAuthorization("Business");

        group.MapGet("", ListVehiclesAsync);
        group.MapGet("/lookup", LookupVehiclesAsync);
        group.MapPost("", CreateVehicleAsync);
        group.MapPut("/{id:int}", UpdateVehicleAsync);
        group.MapPost("/{id:int}/deactivate", DeactivateVehicleAsync);
        group.MapPost("/{id:int}/reactivate", ReactivateVehicleAsync);
        group.MapPost("/blocking-alert", SendBlockingAlertAsync);
        group.MapPost("/incidents", ReportIncidentAsync);
        group.MapGet("/guests", ListGuestNoticesAsync);
        group.MapPost("/guests", CreateGuestNoticeAsync);
        group.MapPost("/guests/{id:int}/status", UpdateGuestNoticeStatusAsync);
        group.MapGet("/zones/{propertyId:int}", ListZonesAsync);
        group.MapPost("/zones", SaveZoneAsync);
        group.MapDelete("/zones/{zoneId:int}", DeleteZoneAsync);
        group.MapPost("/zones/{zoneId:int}/slots/generate", GenerateZoneSlotsAsync);
        group.MapPost("/slots/{slotId:int}/status", SetSlotStatusAsync);
        group.MapGet("/settings/{propertyId:int}", GetSettingsAsync);
        group.MapPut("/settings/{propertyId:int}", SaveSettingsAsync);
        group.MapPost("/billing/sync/{propertyId:int}", SyncBillingDraftsAsync);
        group.MapGet("/audit/{propertyId:int}/sessions", ListAuditSessionsAsync);
        group.MapGet("/audit/sessions/{id:int}", GetAuditSessionAsync);
        group.MapPost("/audit/{propertyId:int}/sessions", StartAuditSessionAsync);
        group.MapPost("/audit/sessions/{sessionId:int}/items/{itemId:int}/result", RecordAuditItemAsync);
        group.MapPost("/audit/sessions/{sessionId:int}/quick-add", QuickAddAuditVehicleAsync);
        group.MapPost("/audit/sessions/{sessionId:int}/complete", CompleteAuditSessionAsync);

        return endpoints;
    }

    private static async Task<PropertyCaller?> ResolveCallerAsync(
        HttpContext http,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var role = Roles.ResolvePrimaryRole(http.User);
        if (role is not (Roles.ChuTro or Roles.QuanLy or Roles.KhachThue))
        {
            return null;
        }

        var caller = new PropertyCaller(userId, role);
        if (caller.Role is Roles.QuanLy or Roles.KhachThue)
        {
            var blocked = await ownerAccess.GetBlockedOwnerIdsAsync(ct);
            if (blocked.Count > 0)
            {
                caller = caller with { BlockedOwnerIds = blocked.ToList() };
            }
        }

        return caller;
    }

    private static async Task<IResult> ListVehiclesAsync(
        HttpContext http,
        int? propertyId,
        int? roomId,
        VehicleType? vehicleType,
        VehicleStatus? status,
        int? parkingZoneId,
        string? keyword,
        IVehicleService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        var result = await service.ListAsync(
            new VehicleFilter
            {
                PropertyId = propertyId,
                RoomId = roomId,
                VehicleType = vehicleType,
                Status = status,
                ParkingZoneId = parkingZoneId,
                Keyword = keyword,
            },
            caller,
            ct);

        return Results.Ok(result);
    }

    private static async Task<IResult> LookupVehiclesAsync(
        HttpContext http,
        int? propertyId,
        string? q,
        IVehicleService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        var items = await service.LookupAsync(propertyId, q ?? string.Empty, caller, ct);
        return Results.Ok(items);
    }

    private static async Task<IResult> CreateVehicleAsync(
        HttpContext http,
        VehicleCreateRequest request,
        IVehicleService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        var result = await service.CreateAsync(request, caller, ct);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode);
        }

        return Results.Created($"/api/vehicles/{result.Vehicle?.Id}", result.Vehicle);
    }

    private static async Task<IResult> UpdateVehicleAsync(
        HttpContext http,
        int id,
        VehicleUpdateRequest request,
        IVehicleService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        request.Id = id;
        var result = await service.UpdateAsync(request, caller, ct);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode);
        }

        return Results.Ok(result.Vehicle);
    }

    public sealed record DeactivateVehicleApiRequest(string Reason);

    private static async Task<IResult> DeactivateVehicleAsync(
        HttpContext http,
        int id,
        DeactivateVehicleApiRequest body,
        IVehicleService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        var result = await service.DeactivateAsync(id, body.Reason, caller, ct);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode);
        }

        return Results.Ok(result.Vehicle);
    }

    private static async Task<IResult> SendBlockingAlertAsync(
        HttpContext http,
        BlockingAlertRequest request,
        IBlockingAlertService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        var result = await service.SendBlockingAlertAsync(request, caller, ct);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode);
        }

        return Results.Ok(result);
    }

    private static async Task<IResult> ListGuestNoticesAsync(
        HttpContext http,
        int? propertyId,
        GuestVehicleNoticeStatus? status,
        IGuestVehicleNoticeService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        var list = await service.ListAsync(propertyId, status, caller, ct);
        return Results.Ok(list);
    }

    private static async Task<IResult> CreateGuestNoticeAsync(
        HttpContext http,
        GuestVehicleNoticeCreateRequest request,
        IGuestVehicleNoticeService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var created = await service.CreateAsync(request, caller, ct);
            return Results.Created($"/api/vehicles/guests/{created.Id}", created);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> ListZonesAsync(
        HttpContext http,
        int propertyId,
        IParkingZoneService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var zones = await service.ListZonesAsync(propertyId, caller, ct);
            return Results.Ok(zones);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> GetSettingsAsync(
        HttpContext http,
        int propertyId,
        IVehicleSettingService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var dto = await service.GetOrCreateAsync(propertyId, caller, ct);
            return Results.Ok(dto);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> SaveSettingsAsync(
        HttpContext http,
        int propertyId,
        VehiclePropertySettingSaveRequest request,
        IVehicleSettingService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            request.PropertyId = propertyId;
            var dto = await service.SaveAsync(request, caller, ct);
            return Results.Ok(dto);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> SyncBillingDraftsAsync(
        HttpContext http,
        int propertyId,
        IVehicleBillingService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var syncedCount = await service.SyncPropertyDraftInvoicesAsync(propertyId, caller, ct);
            return Results.Ok(new { propertyId, syncedCount });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }

    private static async Task<IResult> ReactivateVehicleAsync(
        HttpContext http,
        int id,
        int? zoneId,
        int? slotId,
        IVehicleService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        var result = await service.ReactivateAsync(id, zoneId, slotId, caller, ct);
        if (!result.Succeeded)
        {
            return MapError(result.ErrorCode);
        }

        return Results.Ok(result.Vehicle);
    }

    private static async Task<IResult> ReportIncidentAsync(
        HttpContext http,
        VehicleIncidentCreateRequest request,
        IVehicleService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var incidentId = await service.ReportVehicleIncidentAsync(request, caller, ct);
            return Results.Created($"/api/vehicles/incidents/{incidentId}", new { incidentId });
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> UpdateGuestNoticeStatusAsync(
        HttpContext http,
        int id,
        GuestVehicleNoticeStatus status,
        IGuestVehicleNoticeService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            await service.UpdateStatusAsync(id, status, caller, ct);
            return Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> SaveZoneAsync(
        HttpContext http,
        ParkingZoneSaveRequest request,
        IParkingZoneService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var zone = await service.SaveZoneAsync(request, caller, ct);
            return Results.Ok(zone);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> DeleteZoneAsync(
        HttpContext http,
        int zoneId,
        IParkingZoneService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            await service.DeleteZoneAsync(zoneId, caller, ct);
            return Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> GenerateZoneSlotsAsync(
        HttpContext http,
        int zoneId,
        string? prefix,
        int count,
        IParkingZoneService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var slots = await service.GenerateSlotsAsync(zoneId, prefix ?? "A", count, caller, ct);
            return Results.Ok(slots);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> SetSlotStatusAsync(
        HttpContext http,
        int slotId,
        ParkingSlotStatus status,
        IParkingZoneService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            await service.SetSlotStatusAsync(slotId, status, caller, ct);
            return Results.NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> ListAuditSessionsAsync(
        HttpContext http,
        int propertyId,
        IVehicleAuditService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var sessions = await service.ListSessionsAsync(propertyId, caller, ct);
            return Results.Ok(sessions);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> GetAuditSessionAsync(
        HttpContext http,
        int id,
        IVehicleAuditService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var session = await service.GetSessionAsync(id, caller, ct);
            return session is null
                ? MapError(VehicleErrorCodes.AuditSessionNotFound)
                : Results.Ok(session);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> StartAuditSessionAsync(
        HttpContext http,
        int propertyId,
        string? note,
        IVehicleAuditService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var session = await service.StartSessionAsync(propertyId, note, caller, ct);
            return Results.Created($"/api/vehicles/audit/sessions/{session.Id}", session);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> RecordAuditItemAsync(
        HttpContext http,
        int sessionId,
        int itemId,
        VehicleAuditItemResult result,
        string? note,
        IVehicleAuditService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var session = await service.RecordItemResultAsync(itemId, result, note, caller, ct);
            if (session.Id != sessionId)
            {
                return MapError(VehicleErrorCodes.AuditSessionNotFound);
            }

            return Results.Ok(session);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> QuickAddAuditVehicleAsync(
        HttpContext http,
        int sessionId,
        VehicleCreateRequest request,
        IVehicleAuditService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var session = await service.QuickAddVehicleAsync(sessionId, request, caller, ct);
            return Results.Ok(session);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static async Task<IResult> CompleteAuditSessionAsync(
        HttpContext http,
        int sessionId,
        string? note,
        IVehicleAuditService service,
        OwnerAccessGuard ownerAccess,
        CancellationToken ct)
    {
        var caller = await ResolveCallerAsync(http, ownerAccess, ct);
        if (caller is null) return Results.Forbid();

        try
        {
            var session = await service.CompleteSessionAsync(sessionId, note, caller, ct);
            return Results.Ok(session);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return MapError(ex.Message);
        }
    }

    private static IResult MapError(string? code) => code switch
    {
        VehicleErrorCodes.Forbidden => Results.Forbid(),
        VehicleErrorCodes.PropertyNotFound
            or VehicleErrorCodes.RoomNotFound
            or VehicleErrorCodes.VehicleNotFound
            or VehicleErrorCodes.ZoneNotFound
            or VehicleErrorCodes.SlotNotFound
            or VehicleErrorCodes.GuestNoticeNotFound
            or VehicleErrorCodes.AuditSessionNotFound =>
            Results.Problem(statusCode: StatusCodes.Status404NotFound, title: code),
        VehicleErrorCodes.BlockingAlertRateLimited =>
            Results.Problem(statusCode: StatusCodes.Status429TooManyRequests, title: code),
        VehicleErrorCodes.DuplicateActivePlate
            or VehicleErrorCodes.RoomVehicleLimitReached
            or VehicleErrorCodes.ZoneFull
            or VehicleErrorCodes.SlotCurrentlyOccupied
            or VehicleErrorCodes.SlotNotAvailable
            or VehicleErrorCodes.DuplicateZoneName
            or VehicleErrorCodes.DuplicateSlotCode
            or VehicleErrorCodes.ZoneHasActiveVehicles
            or VehicleErrorCodes.GuestNoticeLimitReached
            or VehicleErrorCodes.BlockingAlertAmbiguousPlate
            or VehicleErrorCodes.AuditSessionAlreadyCompleted =>
            Results.Problem(statusCode: StatusCodes.Status409Conflict, title: code),
        _ => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: code ?? VehicleErrorCodes.SaveFailed),
    };
}
