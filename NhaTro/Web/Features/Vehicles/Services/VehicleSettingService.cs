using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Properties;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Vehicles.Services;

public interface IVehicleSettingService
{
    Task<IReadOnlyList<VehiclePropertyOptionDto>> PropertiesAsync(PropertyCaller caller, CancellationToken ct = default);
    Task<VehiclePropertySettingDto> GetOrCreateAsync(int propertyId, PropertyCaller caller, CancellationToken ct = default);
    Task<VehiclePropertySettingDto> SaveAsync(VehiclePropertySettingSaveRequest request, PropertyCaller caller, CancellationToken ct = default);
}

public sealed class VehicleSettingService(
    ApplicationDbContext db,
    AuditLogService audit,
    TimeProvider clock) : IVehicleSettingService
{
    public async Task<IReadOnlyList<VehiclePropertyOptionDto>> PropertiesAsync(
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        IQueryable<Web.Features.Properties.Entities.Property> query;
        if (caller.IsKhachThue)
        {
            var propertyIds = await VehicleScope.GetTenantPropertyIdsAsync(db, caller, clock.TodayVietnam(), ct);
            query = db.Properties.AsNoTracking().Where(p => propertyIds.Contains(p.Id));
        }
        else
        {
            query = VehicleScope.ApplyProperties(db, caller);
        }

        var rows = await (from property in query
                          join setting in db.VehiclePropertySettings.AsNoTracking()
                              on property.Id equals setting.PropertyId into settingGroup
                          from setting in settingGroup.DefaultIfEmpty()
                          orderby property.Id
                          select new VehiclePropertyOptionDto(
                              property.Id,
                              property.Name,
                              setting != null && setting.EnableParkingZones))
            .ToListAsync(ct);

        return rows;
    }

    public async Task<VehiclePropertySettingDto> GetOrCreateAsync(
        int propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var canAccess = caller.IsKhachThue
            ? (await VehicleScope.GetTenantPropertyIdsAsync(db, caller, clock.TodayVietnam(), ct)).Contains(propertyId)
            : await VehicleScope.CanAccessPropertyAsync(db, propertyId, caller, ct);

        if (!canAccess)
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var propertyName = await db.Properties.AsNoTracking()
            .Where(p => p.Id == propertyId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.PropertyNotFound);

        var setting = await EnsureEntityAsync(db, propertyId, clock.GetUtcNow(), ct);
        return ToDto(setting, propertyName);
    }

    public async Task<VehiclePropertySettingDto> SaveAsync(
        VehiclePropertySettingSaveRequest request,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (!await VehicleScope.IsOwnerAsync(db, request.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        if (request.ElectricMotorbikeMonthlyFee < 0m
            || request.ManualMotorbikeMonthlyFee < 0m
            || request.ScooterMonthlyFee < 0m)
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidFeeAmount);
        }

        if (request.MaxVehiclesPerRoom is < 0 or > 50
            || request.ZoneNearFullThresholdPercent is < 50 or > 100)
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidSettingValue);
        }

        // Không hạ trần xe/phòng xuống dưới số xe đang gửi thực tế của phòng đông nhất.
        if (request.MaxVehiclesPerRoom > 0)
        {
            var busiestRoomActive = await db.Vehicles.AsNoTracking()
                .Where(v => v.PropertyId == request.PropertyId && v.Status == VehicleStatus.Active)
                .GroupBy(v => v.RoomId)
                .Select(g => g.Count())
                .OrderByDescending(c => c)
                .FirstOrDefaultAsync(ct);

            if (busiestRoomActive > request.MaxVehiclesPerRoom)
            {
                throw new InvalidOperationException(VehicleErrorCodes.MaxVehiclesBelowActiveCount);
            }
        }

        var propertyName = await db.Properties.AsNoTracking()
            .Where(p => p.Id == request.PropertyId)
            .Select(p => p.Name)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.PropertyNotFound);

        var now = clock.GetUtcNow();
        var setting = await db.VehiclePropertySettings
            .FirstOrDefaultAsync(s => s.PropertyId == request.PropertyId, ct);

        if (setting is null)
        {
            setting = new VehiclePropertySetting
            {
                PropertyId = request.PropertyId,
            };
            db.VehiclePropertySettings.Add(setting);
        }

        // Tắt phân khu & ô đỗ khi vẫn còn xe đang gán khu/ô: cảnh báo thay vì để dữ liệu treo.
        if (setting.EnableParkingZones && !request.EnableParkingZones)
        {
            var zonesInUse = await db.Vehicles.AsNoTracking()
                .AnyAsync(v => v.PropertyId == request.PropertyId
                    && v.Status == VehicleStatus.Active
                    && (v.ParkingZoneId != null || v.ParkingSlotId != null), ct);
            if (zonesInUse)
            {
                throw new InvalidOperationException(VehicleErrorCodes.ParkingZonesInUse);
            }
        }

        setting.ElectricMotorbikeMonthlyFee = decimal.Round(request.ElectricMotorbikeMonthlyFee, 2);
        setting.ManualMotorbikeMonthlyFee = decimal.Round(request.ManualMotorbikeMonthlyFee, 2);
        setting.ScooterMonthlyFee = decimal.Round(request.ScooterMonthlyFee, 2);
        setting.MaxVehiclesPerRoom = request.MaxVehiclesPerRoom;
        setting.EnableParkingZones = request.EnableParkingZones;
        setting.ZoneNearFullThresholdPercent = request.ZoneNearFullThresholdPercent;
        setting.AllowTenantGuestNotice = request.AllowTenantGuestNotice;
        setting.AllowBlockingAlert = request.AllowBlockingAlert;
        setting.AllowManagerManageVehicles = request.AllowManagerManageVehicles;
        setting.UpdatedByUserId = caller.UserId;
        setting.UpdatedAt = now;

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.SettingsUpdated",
            caller.UserId,
            request.PropertyId,
            $"Electric={setting.ElectricMotorbikeMonthlyFee:0};Manual={setting.ManualMotorbikeMonthlyFee:0};Scooter={setting.ScooterMonthlyFee:0};MaxPerRoom={setting.MaxVehiclesPerRoom};Zones={setting.EnableParkingZones}",
            now,
            ct);

        return ToDto(setting, propertyName);
    }

    internal static async Task<VehiclePropertySetting> EnsureEntityAsync(
        ApplicationDbContext db,
        int propertyId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var setting = await db.VehiclePropertySettings
            .FirstOrDefaultAsync(s => s.PropertyId == propertyId, ct);

        if (setting is not null)
        {
            return setting;
        }

        setting = new VehiclePropertySetting
        {
            PropertyId = propertyId,
            ElectricMotorbikeMonthlyFee = VehicleRules.DefaultElectricMotorbikeMonthlyFee,
            ManualMotorbikeMonthlyFee = VehicleRules.DefaultManualMotorbikeMonthlyFee,
            ScooterMonthlyFee = VehicleRules.DefaultScooterMonthlyFee,
            MaxVehiclesPerRoom = VehicleRules.DefaultMaxVehiclesPerRoom,
            EnableParkingZones = false,
            ZoneNearFullThresholdPercent = VehicleRules.DefaultZoneNearFullThresholdPercent,
            AllowTenantGuestNotice = true,
            AllowBlockingAlert = true,
            AllowManagerManageVehicles = true,
            UpdatedAt = now,
        };

        db.VehiclePropertySettings.Add(setting);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.Entry(setting).State = EntityState.Detached;
            setting = await db.VehiclePropertySettings
                .FirstAsync(s => s.PropertyId == propertyId, ct);
        }

        return setting;
    }

    private static VehiclePropertySettingDto ToDto(VehiclePropertySetting s, string propertyName) =>
        new(
            s.Id,
            s.PropertyId,
            propertyName,
            s.ElectricMotorbikeMonthlyFee,
            s.ManualMotorbikeMonthlyFee,
            s.ScooterMonthlyFee,
            s.MaxVehiclesPerRoom,
            s.EnableParkingZones,
            s.ZoneNearFullThresholdPercent,
            s.AllowTenantGuestNotice,
            s.AllowBlockingAlert,
            s.AllowManagerManageVehicles,
            s.UpdatedAt);
}
