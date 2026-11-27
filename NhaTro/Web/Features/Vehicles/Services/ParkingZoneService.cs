using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

public interface IParkingZoneService
{
    Task<IReadOnlyList<ParkingZoneDto>> ListZonesAsync(int propertyId, PropertyCaller caller, CancellationToken ct = default);
    Task<ParkingZoneDto> SaveZoneAsync(ParkingZoneSaveRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task DeleteZoneAsync(int zoneId, PropertyCaller caller, CancellationToken ct = default);
    Task<IReadOnlyList<ParkingSlotDto>> GenerateSlotsAsync(int zoneId, string prefix, int count, PropertyCaller caller, CancellationToken ct = default);
    Task SetSlotStatusAsync(int slotId, ParkingSlotStatus status, PropertyCaller caller, CancellationToken ct = default);
}

public sealed class ParkingZoneService(
    ApplicationDbContext db,
    AuditLogService audit,
    TimeProvider clock) : IParkingZoneService
{
    public async Task<IReadOnlyList<ParkingZoneDto>> ListZonesAsync(
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
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var threshold = await db.VehiclePropertySettings.AsNoTracking()
            .Where(s => s.PropertyId == propertyId)
            .Select(s => (int?)s.ZoneNearFullThresholdPercent)
            .FirstOrDefaultAsync(ct) ?? VehicleRules.DefaultZoneNearFullThresholdPercent;

        var zones = await db.ParkingZones.AsNoTracking()
            .Where(z => z.PropertyId == propertyId)
            .OrderBy(z => z.Name)
            .ToListAsync(ct);

        if (zones.Count == 0)
        {
            return [];
        }

        var zoneIds = zones.Select(z => z.Id).ToArray();

        var activeVehicles = await (from v in db.Vehicles.AsNoTracking()
                                    join r in db.Rooms.IgnoreQueryFilters().AsNoTracking() on v.RoomId equals r.Id
                                    where v.PropertyId == propertyId && v.Status == VehicleStatus.Active
                                    select new
                                    {
                                        v.Id,
                                        v.ParkingZoneId,
                                        v.ParkingSlotId,
                                        v.PlateNumber,
                                        v.VehicleType,
                                        RoomCode = r.Code,
                                    })
            .ToListAsync(ct);

        var slots = await db.ParkingSlots.AsNoTracking()
            .Where(s => zoneIds.Contains(s.ParkingZoneId))
            .OrderBy(s => s.Code)
            .ToListAsync(ct);

        var vehicleById = activeVehicles.ToDictionary(v => v.Id);
        var vehicleBySlotId = activeVehicles
            .Where(v => v.ParkingSlotId.HasValue)
            .GroupBy(v => v.ParkingSlotId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<ParkingZoneDto>(zones.Count);
        foreach (var zone in zones)
        {
            var occupiedCount = activeVehicles.Count(v => v.ParkingZoneId == zone.Id);
            var usagePercent = zone.Capacity > 0
                ? (int)Math.Round(occupiedCount * 100.0 / zone.Capacity)
                : 0;
            var isFull = occupiedCount >= zone.Capacity;
            var isNearFull = !isFull && usagePercent >= threshold;

            var zoneSlots = slots
                .Where(s => s.ParkingZoneId == zone.Id)
                .Select(slot =>
                {
                    var current = slot.CurrentVehicleId.HasValue && vehicleById.TryGetValue(slot.CurrentVehicleId.Value, out var byId)
                        ? byId
                        : (vehicleBySlotId.TryGetValue(slot.Id, out var bySlot) ? bySlot : null);

                    var status = current is not null
                        ? ParkingSlotStatus.Occupied
                        : (slot.Status == ParkingSlotStatus.Occupied ? ParkingSlotStatus.Available : slot.Status);

                    return new ParkingSlotDto(
                        slot.Id,
                        slot.ParkingZoneId,
                        slot.Code,
                        status,
                        current?.Id,
                        current?.PlateNumber,
                        current?.RoomCode,
                        current?.VehicleType);
                })
                .ToList();

            result.Add(new ParkingZoneDto(
                zone.Id,
                zone.PropertyId,
                propertyName,
                zone.Name,
                zone.IsIndoor,
                zone.Capacity,
                occupiedCount,
                usagePercent,
                isNearFull,
                isFull,
                zone.AllowedVehicleTypes,
                zone.HasSlots,
                zone.IsFree,
                zone.OverrideMonthlyFee,
                zone.IsActive,
                zone.SlotPrefix,
                zoneSlots));
        }

        return result;
    }

    public async Task<ParkingZoneDto> SaveZoneAsync(
        ParkingZoneSaveRequest request,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (!await VehicleScope.IsOwnerAsync(db, request.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidSettingValue);
        }

        if (request.Capacity is < 1 or > 5000)
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidZoneCapacity);
        }

        if (request.AllowedVehicleTypes == AllowedVehicleTypeFlags.None)
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidSettingValue);
        }

        if (request.OverrideMonthlyFee is < 0m)
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidFeeAmount);
        }

        var duplicateName = await db.ParkingZones.AsNoTracking()
            .AnyAsync(z => z.PropertyId == request.PropertyId
                && z.Name == name
                && (!request.Id.HasValue || z.Id != request.Id.Value), ct);
        if (duplicateName)
        {
            throw new InvalidOperationException(VehicleErrorCodes.DuplicateZoneName);
        }

        var now = clock.GetUtcNow();
        ParkingZone zone;
        if (request.Id is { } existingId && existingId > 0)
        {
            zone = await db.ParkingZones
                .FirstOrDefaultAsync(z => z.Id == existingId && z.PropertyId == request.PropertyId, ct)
                ?? throw new InvalidOperationException(VehicleErrorCodes.ZoneNotFound);

            var currentOccupied = await db.Vehicles.AsNoTracking()
                .CountAsync(v => v.ParkingZoneId == zone.Id && v.Status == VehicleStatus.Active, ct);

            if (request.Capacity < currentOccupied)
            {
                throw new InvalidOperationException(VehicleErrorCodes.InvalidZoneCapacity);
            }

            if (!request.IsActive && currentOccupied > 0)
            {
                throw new InvalidOperationException(VehicleErrorCodes.ZoneHasActiveVehicles);
            }

            // Đổi loại xe được phép: không để xe đang đỗ rơi vào loại không còn được phép.
            if (request.AllowedVehicleTypes != zone.AllowedVehicleTypes)
            {
                var activeTypes = await db.Vehicles.AsNoTracking()
                    .Where(v => v.ParkingZoneId == zone.Id && v.Status == VehicleStatus.Active)
                    .Select(v => v.VehicleType)
                    .Distinct()
                    .ToListAsync(ct);

                if (activeTypes.Any(t => !VehicleRules.IsVehicleTypeAllowed(t, request.AllowedVehicleTypes)))
                {
                    throw new InvalidOperationException(VehicleErrorCodes.ZoneTypeExcludesActiveVehicles);
                }
            }
        }
        else
        {
            zone = new ParkingZone
            {
                PropertyId = request.PropertyId,
                CreatedAt = now,
            };
            db.ParkingZones.Add(zone);
        }

        zone.Name = name;
        zone.IsIndoor = request.IsIndoor;
        zone.Capacity = request.Capacity;
        zone.AllowedVehicleTypes = request.AllowedVehicleTypes;
        zone.HasSlots = request.HasSlots;
        // Giữ tiền tố đã dùng khi sửa khu (kể cả khi tạm thời không còn ô nào) thay vì luôn reset về "A".
        var requestedPrefix = string.IsNullOrWhiteSpace(request.SlotPrefix)
            ? null
            : request.SlotPrefix.Trim().ToUpperInvariant();
        zone.SlotPrefix = zone.HasSlots ? (requestedPrefix ?? zone.SlotPrefix ?? "A") : null;
        zone.IsFree = request.IsFree;
        zone.OverrideMonthlyFee = request.IsFree
            ? null
            : (request.OverrideMonthlyFee.HasValue ? decimal.Round(request.OverrideMonthlyFee.Value, 2) : null);
        zone.IsActive = request.IsActive;

        // Lưu khu và sinh ô đỗ trong cùng một transaction để không lệch cấu hình.
        await using var transaction = await BeginWriteTransactionAsync(ct);
        await db.SaveChangesAsync(ct);

        if (zone.HasSlots)
        {
            var slotCount = request.GenerateSlotCount is > 0
                ? Math.Min(request.GenerateSlotCount.Value, zone.Capacity)
                : zone.Capacity;
            var prefix = zone.SlotPrefix ?? "A";
            await ReconcileSlotsAsync(zone.Id, hasSlots: true, prefix, slotCount, ct);
        }
        else
        {
            // Tắt ô đỗ cố định: dọn sạch các ô còn lại (chặn nếu vẫn có ô đang dùng).
            await ReconcileSlotsAsync(zone.Id, hasSlots: false, prefix: string.Empty, desiredCount: 0, ct);
        }

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        await audit.RecordAsync(
            AuditLogCategory.Business,
            request.Id.HasValue ? "Vehicle.ZoneUpdated" : "Vehicle.ZoneCreated",
            caller.UserId,
            zone.PropertyId,
            $"Zone={zone.Name};Indoor={zone.IsIndoor};Capacity={zone.Capacity};Allowed={(int)zone.AllowedVehicleTypes};HasSlots={zone.HasSlots}",
            now,
            ct);

        var all = await ListZonesAsync(zone.PropertyId, caller, ct);
        return all.First(z => z.Id == zone.Id);
    }

    public async Task DeleteZoneAsync(
        int zoneId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var zone = await db.ParkingZones.FirstOrDefaultAsync(z => z.Id == zoneId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.ZoneNotFound);

        if (!await VehicleScope.IsOwnerAsync(db, zone.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var hasActive = await db.Vehicles.AsNoTracking()
            .AnyAsync(v => v.ParkingZoneId == zone.Id && v.Status == VehicleStatus.Active, ct);
        if (hasActive)
        {
            throw new InvalidOperationException(VehicleErrorCodes.ZoneHasActiveVehicles);
        }

        // Gỡ liên kết của các xe Inactive trong lịch sử trước khi xóa khu
        var historicalVehicles = await db.Vehicles
            .Where(v => v.ParkingZoneId == zone.Id)
            .ToListAsync(ct);
        foreach (var vehicle in historicalVehicles)
        {
            vehicle.ParkingZoneId = null;
            vehicle.ParkingSlotId = null;
        }

        db.ParkingZones.Remove(zone);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.ZoneDeleted",
            caller.UserId,
            zone.PropertyId,
            $"Zone={zone.Name}",
            clock.GetUtcNow(),
            ct);
    }

    public async Task<IReadOnlyList<ParkingSlotDto>> GenerateSlotsAsync(
        int zoneId,
        string prefix,
        int count,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var zone = await db.ParkingZones.FirstOrDefaultAsync(z => z.Id == zoneId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.ZoneNotFound);

        if (!await VehicleScope.IsOwnerAsync(db, zone.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var cleanPrefix = string.IsNullOrWhiteSpace(prefix) ? "A" : prefix.Trim().ToUpperInvariant();
        var targetCount = Math.Clamp(count, 1, zone.Capacity);

        await using var transaction = await BeginWriteTransactionAsync(ct);
        zone.HasSlots = true;
        zone.SlotPrefix = cleanPrefix;
        await db.SaveChangesAsync(ct);

        await ReconcileSlotsAsync(zone.Id, hasSlots: true, cleanPrefix, targetCount, ct);

        if (transaction is not null)
        {
            await transaction.CommitAsync(ct);
        }

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.SlotsGenerated",
            caller.UserId,
            zone.PropertyId,
            $"Zone={zone.Name};Prefix={cleanPrefix};Count={targetCount}",
            clock.GetUtcNow(),
            ct);

        var updatedZones = await ListZonesAsync(zone.PropertyId, caller, ct);
        return updatedZones.First(z => z.Id == zone.Id).Slots;
    }

    public async Task SetSlotStatusAsync(
        int slotId,
        ParkingSlotStatus status,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var slot = await db.ParkingSlots.FirstOrDefaultAsync(s => s.Id == slotId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.SlotNotFound);

        var zone = await db.ParkingZones.AsNoTracking().FirstOrDefaultAsync(z => z.Id == slot.ParkingZoneId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.ZoneNotFound);

        // Cấu hình khu/ô đỗ là đặc quyền của Chủ trọ (Quản lý chỉ vận hành xe), theo SPEC §8.
        if (!await VehicleScope.IsOwnerAsync(db, zone.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var hasActiveVehicle = await db.Vehicles.AsNoTracking()
            .AnyAsync(v => v.ParkingSlotId == slot.Id && v.Status == VehicleStatus.Active, ct);
        if (hasActiveVehicle || slot.CurrentVehicleId.HasValue)
        {
            throw new InvalidOperationException(VehicleErrorCodes.SlotCurrentlyOccupied);
        }

        if (status is not (ParkingSlotStatus.Available or ParkingSlotStatus.Maintenance))
        {
            throw new InvalidOperationException(VehicleErrorCodes.InvalidStatus);
        }

        slot.Status = status;
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.SlotStatusChanged",
            caller.UserId,
            zone.PropertyId,
            $"Zone={zone.Name};Slot={slot.Code};Status={status}",
            clock.GetUtcNow(),
            ct);
    }

    private async Task<IDbContextTransaction?> BeginWriteTransactionAsync(CancellationToken ct)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await db.Database.BeginTransactionAsync(ct);
    }

    /// <summary>
    /// Đồng bộ danh sách ô đỗ theo cấu hình: thêm ô còn thiếu và cắt ô dư,
    /// nhưng chặn nếu ô cần cắt đang được xe dùng (tránh mất dữ liệu).
    /// </summary>
    private async Task ReconcileSlotsAsync(
        int zoneId,
        bool hasSlots,
        string prefix,
        int desiredCount,
        CancellationToken ct)
    {
        var slots = await db.ParkingSlots
            .Where(s => s.ParkingZoneId == zoneId)
            .OrderBy(s => s.Code)
            .ToListAsync(ct);

        if (!hasSlots)
        {
            if (slots.Count == 0)
            {
                return;
            }

            var occupiedIds = (await db.Vehicles.AsNoTracking()
                    .Where(v => v.Status == VehicleStatus.Active && v.ParkingSlotId != null)
                    .Select(v => v.ParkingSlotId!.Value)
                    .ToListAsync(ct))
                .ToHashSet();

            if (slots.Any(s => s.CurrentVehicleId.HasValue || occupiedIds.Contains(s.Id)))
            {
                throw new InvalidOperationException(VehicleErrorCodes.ZoneHasOccupiedSlots);
            }

            db.ParkingSlots.RemoveRange(slots);
            await db.SaveChangesAsync(ct);
            return;
        }

        if (desiredCount < 1)
        {
            desiredCount = 1;
        }

        if (slots.Count > desiredCount)
        {
            var toRemove = slots.Skip(desiredCount).ToList();
            var removeIds = toRemove.Select(s => s.Id).ToList();
            var occupiedIds = (await db.Vehicles.AsNoTracking()
                    .Where(v => v.Status == VehicleStatus.Active
                        && v.ParkingSlotId != null
                        && removeIds.Contains(v.ParkingSlotId.Value))
                    .Select(v => v.ParkingSlotId!.Value)
                    .ToListAsync(ct))
                .ToHashSet();

            if (toRemove.Any(s => s.CurrentVehicleId.HasValue || occupiedIds.Contains(s.Id)))
            {
                throw new InvalidOperationException(VehicleErrorCodes.ZoneHasOccupiedSlots);
            }

            db.ParkingSlots.RemoveRange(toRemove);
            await db.SaveChangesAsync(ct);
        }

        await EnsureSlotsGeneratedAsync(zoneId, prefix, desiredCount, ct);
    }

    private async Task EnsureSlotsGeneratedAsync(int zoneId, string prefix, int count, CancellationToken ct)
    {
        var existingCodes = (await db.ParkingSlots.AsNoTracking()
                .Where(s => s.ParkingZoneId == zoneId)
                .Select(s => s.Code)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = false;
        for (var i = 1; i <= count; i++)
        {
            var code = $"{prefix}{i:D2}";
            if (existingCodes.Contains(code))
            {
                continue;
            }

            db.ParkingSlots.Add(new ParkingSlot
            {
                ParkingZoneId = zoneId,
                Code = code,
                Status = ParkingSlotStatus.Available,
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(ct);
        }
    }
}
