using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.AccessControl.DTOs;
using Web.Features.AccessControl.Entities;
using Web.Features.AccessControl.Enums;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Properties;

namespace Web.Features.AccessControl.Services;

public interface IGatePolicyService
{
    Task<IReadOnlyList<GatePropertyOption>> PropertiesAsync(PropertyCaller caller, CancellationToken ct = default);
    Task<GatePolicyDto> GetAsync(int propertyId, PropertyCaller caller, CancellationToken ct = default);
    Task<GatePolicyDto> SaveAsync(GatePolicySaveRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task<IReadOnlyList<GateDeviceDto>> DevicesAsync(int propertyId, PropertyCaller caller, CancellationToken ct = default);
    Task<GateDeviceDto> SaveDeviceAsync(GateDeviceSaveRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task DeleteDeviceAsync(int deviceId, PropertyCaller caller, CancellationToken ct = default);
}

public sealed class GatePolicyService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    TimeProvider clock,
    AuditLogService audit) : IGatePolicyService
{
    public async Task<IReadOnlyList<GatePropertyOption>> PropertiesAsync(PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        return await GateScope.Apply(db, caller)
            .OrderBy(property => property.Name)
            .Select(property => new GatePropertyOption(property.Id, property.Name))
            .ToListAsync(ct);
    }

    public async Task<GatePolicyDto> GetAsync(int propertyId, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await GateScope.CanAccessAsync(db, propertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(GateErrorCodes.Forbidden);
        }

        var policy = await db.GatePolicies.AsNoTracking().FirstOrDefaultAsync(item => item.PropertyId == propertyId, ct);
        return policy is null ? GatePolicyDto.Defaults(propertyId) : ToDto(policy);
    }

    public async Task<GatePolicyDto> SaveAsync(GatePolicySaveRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await GateScope.IsOwnerAsync(db, request.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(GateErrorCodes.Forbidden);
        }

        Validate(request);

        var policy = await db.GatePolicies.FirstOrDefaultAsync(item => item.PropertyId == request.PropertyId, ct);
        if (policy is null)
        {
            policy = new GatePolicy { PropertyId = request.PropertyId };
            db.GatePolicies.Add(policy);
        }

        policy.AuthMode = request.AuthMode;
        policy.AntiPassbackEnabled = request.AntiPassbackEnabled;
        policy.AllowManualOpen = request.AllowManualOpen;
        policy.AllowManagerApprove = request.AllowManagerApprove;
        policy.AutoApproveDayVisit = request.AutoApproveDayVisit;
        // Qua đêm luôn phải chờ duyệt — không cho bật tự duyệt.
        policy.AutoApproveOvernight = false;
        policy.AllowTenantIssuePass = request.AllowTenantIssuePass;
        policy.AllowGuestSelfFill = request.AllowGuestSelfFill;
        policy.GuestSelfMaxPassesPerDay = request.GuestSelfMaxPassesPerDay;
        policy.TenantMayIssueOvernight = request.TenantMayIssueOvernight;
        policy.TenantMaxPassesPerDay = request.TenantMaxPassesPerDay;
        policy.TenantMaxTtlMinutes = request.TenantMaxTtlMinutes;
        policy.TenantMaxUses = request.TenantMaxUses;
        policy.PassTtlMinutes = request.PassTtlMinutes;
        policy.DayVisitStartHour = request.DayVisitStartHour;
        policy.DayVisitEndHour = request.DayVisitEndHour;
        policy.RetentionDays = request.RetentionDays;
        policy.OfflineGraceMinutes = request.OfflineGraceMinutes;
        policy.RequiredGuestFields = request.RequiredGuestFields.Trim();
        policy.PreferredStayChannel = request.PreferredStayChannel;
        policy.UpdatedAt = clock.GetUtcNow();
        policy.UpdatedByUserId = caller.UserId;

        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(
            AuditLogCategory.Business,
            "GatePolicyUpdated",
            caller.UserId,
            request.PropertyId,
            metadata: $"authMode:{policy.AuthMode};tenantIssue:{policy.AllowTenantIssuePass}",
            cancellationToken: ct);
        return ToDto(policy);
    }

    public async Task<IReadOnlyList<GateDeviceDto>> DevicesAsync(int propertyId, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await GateScope.CanAccessAsync(db, propertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(GateErrorCodes.Forbidden);
        }

        return await db.GateDevices.AsNoTracking()
            .Where(device => device.PropertyId == propertyId)
            .OrderBy(device => device.Name)
            .Select(device => new GateDeviceDto(device.Id, device.Name, device.Kind, device.Level, device.IsOnline, device.LastSeenAt))
            .ToListAsync(ct);
    }

    public async Task<GateDeviceDto> SaveDeviceAsync(GateDeviceSaveRequest request, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        if (!await GateScope.IsOwnerAsync(db, request.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(GateErrorCodes.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 150)
        {
            throw new ArgumentException(GateErrorCodes.InvalidRequest);
        }

        var name = request.Name.Trim();
        if (await db.GateDevices.AnyAsync(device => device.PropertyId == request.PropertyId && device.Name == name, ct))
        {
            throw new InvalidOperationException(GateErrorCodes.InvalidRequest);
        }

        var device = new GateDevice
        {
            PropertyId = request.PropertyId,
            Name = name,
            Kind = request.Kind,
            Level = request.Level,
            IsOnline = false,
            CreatedAt = clock.GetUtcNow(),
        };
        db.GateDevices.Add(device);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "GateDeviceCreated",
            caller.UserId,
            request.PropertyId,
            metadata: $"device:{device.Id};level:{device.Level}",
            cancellationToken: ct);

        return new GateDeviceDto(device.Id, device.Name, device.Kind, device.Level, device.IsOnline, device.LastSeenAt);
    }

    public async Task DeleteDeviceAsync(int deviceId, PropertyCaller caller, CancellationToken ct = default)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct);
        var device = await db.GateDevices.FirstOrDefaultAsync(item => item.Id == deviceId, ct)
            ?? throw new KeyNotFoundException(GateErrorCodes.InvalidRequest);
        if (!await GateScope.IsOwnerAsync(db, device.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(GateErrorCodes.Forbidden);
        }

        db.GateDevices.Remove(device);
        await db.SaveChangesAsync(ct);
        await audit.RecordAsync(
            AuditLogCategory.Business,
            "GateDeviceDeleted",
            caller.UserId,
            device.PropertyId,
            metadata: $"device:{device.Id}",
            cancellationToken: ct);
    }

    private static void Validate(GatePolicySaveRequest request)
    {
        if (request.TenantMaxPassesPerDay is < 0 or > 50 ||
            request.GuestSelfMaxPassesPerDay is < 0 or > 50 ||
            request.TenantMaxUses is < 1 or > 10 ||
            request.TenantMaxTtlMinutes is < 5 or > 24 * 60 ||
            request.PassTtlMinutes is < 5 or > 24 * 60 ||
            request.DayVisitStartHour is < 0 or > 23 ||
            request.DayVisitEndHour is < 1 or > 24 ||
            request.DayVisitStartHour >= request.DayVisitEndHour ||
            request.RetentionDays is < 1 or > 3650 ||
            request.OfflineGraceMinutes is < 0 or > 1440)
        {
            throw new ArgumentException(GateErrorCodes.InvalidRequest);
        }

        if (request.AuthMode is not (GateAuthMode.SingleFactor or GateAuthMode.QrPlusBiometric or GateAuthMode.FaceOnly))
        {
            throw new ArgumentException(GateErrorCodes.InvalidRequest);
        }
    }

    internal static GatePolicyDto ToDto(GatePolicy policy) => new(
        policy.PropertyId, policy.AuthMode, policy.AntiPassbackEnabled, policy.AllowManualOpen,
        policy.AllowManagerApprove, policy.AutoApproveDayVisit, policy.AutoApproveOvernight,
        policy.AllowTenantIssuePass, policy.AllowGuestSelfFill, policy.GuestSelfMaxPassesPerDay,
        policy.TenantMayIssueOvernight, policy.TenantMaxPassesPerDay,
        policy.TenantMaxTtlMinutes, policy.TenantMaxUses, policy.PassTtlMinutes, policy.DayVisitStartHour,
        policy.DayVisitEndHour, policy.RetentionDays, policy.OfflineGraceMinutes, policy.RequiredGuestFields,
        policy.PreferredStayChannel, policy.UpdatedAt);
}
