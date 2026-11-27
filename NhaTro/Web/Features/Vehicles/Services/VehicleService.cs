using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Localization;
using Web.Data;
using Web.Features.Audit.Entities;
using Web.Features.Audit.Services;
using Web.Features.Contracts.Enums;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;
using Web.Infrastructure.Time;

namespace Web.Features.Vehicles.Services;

public interface IVehicleService
{
    Task<IReadOnlyList<VehicleRoomOptionDto>> RoomsWithActiveContractAsync(int? propertyId, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleListResultDto> ListAsync(VehicleFilter filter, PropertyCaller caller, CancellationToken ct = default);
    Task<IReadOnlyList<VehicleLookupItemDto>> LookupAsync(int? propertyId, string query, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleOperationResult> CreateAsync(VehicleCreateRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleOperationResult> UpdateAsync(VehicleUpdateRequest request, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleOperationResult> DeactivateAsync(int vehicleId, string reason, PropertyCaller caller, CancellationToken ct = default);
    Task<VehicleOperationResult> ReactivateAsync(int vehicleId, int? parkingZoneId, int? parkingSlotId, PropertyCaller caller, CancellationToken ct = default);
    Task<int> DeactivateContractVehiclesAsync(Guid contractId, string reason, PropertyCaller caller, CancellationToken ct = default);
    Task<int> ReportVehicleIncidentAsync(VehicleIncidentCreateRequest request, PropertyCaller caller, CancellationToken ct = default);
}

public sealed class VehicleService(
    ApplicationDbContext db,
    IVehicleBillingService billing,
    INotificationService notifications,
    AuditLogService audit,
    TimeProvider clock,
    IStringLocalizer<VehicleResource>? localizer = null) : IVehicleService
{
    public async Task<IReadOnlyList<VehicleRoomOptionDto>> RoomsWithActiveContractAsync(
        int? propertyId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        List<int> allowedPropertyIds;
        List<int>? allowedRoomIds = null;

        if (caller.IsKhachThue)
        {
            var todayOnly = clock.TodayVietnam();
            allowedRoomIds = await VehicleScope.GetTenantRoomIdsAsync(db, caller, todayOnly, ct);
            allowedPropertyIds = await VehicleScope.GetTenantPropertyIdsAsync(db, caller, todayOnly, ct);
        }
        else
        {
            allowedPropertyIds = await VehicleScope.ApplyProperties(db, caller)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        if (propertyId.HasValue)
        {
            allowedPropertyIds = allowedPropertyIds.Where(id => id == propertyId.Value).ToList();
        }

        if (allowedPropertyIds.Count == 0)
        {
            return [];
        }

        var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
        var rows = await (from contract in db.Contracts.AsNoTracking()
                          join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                          join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                          where contract.Status == ContractStatus.Approved
                              && contract.StartDate <= today
                              && contract.EndDate >= today
                              && allowedPropertyIds.Contains(room.PropertyId)
                              && (allowedRoomIds == null || allowedRoomIds.Contains(room.Id))
                          orderby property.Id, room.Code
                          select new
                          {
                              room.Id,
                              room.PropertyId,
                              PropertyName = property.Name,
                              RoomCode = room.Code,
                              contract.ContractId,
                              contract.ContractNumber,
                          })
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            return [];
        }

        var roomIds = rows.Select(r => r.Id).Distinct().ToArray();
        var contractIds = rows.Select(r => r.ContractId).Distinct().ToArray();
        var propIds = rows.Select(r => r.PropertyId).Distinct().ToArray();

        var activeCounts = await db.Vehicles.AsNoTracking()
            .Where(v => roomIds.Contains(v.RoomId) && v.Status == VehicleStatus.Active)
            .GroupBy(v => v.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoomId, x => x.Count, ct);

        var maxPerRoomMap = await db.VehiclePropertySettings.AsNoTracking()
            .Where(s => propIds.Contains(s.PropertyId))
            .ToDictionaryAsync(s => s.PropertyId, s => s.MaxVehiclesPerRoom, ct);

        var members = await (from m in db.ContractMembers.AsNoTracking()
                             join t in db.Tenants.IgnoreQueryFilters().AsNoTracking() on m.TenantId equals t.Id
                             where contractIds.Contains(m.ContractId)
                             select new { m.ContractId, t.Id, t.FullName, t.UserId })
            .ToListAsync(ct);

        var result = new List<VehicleRoomOptionDto>();
        foreach (var group in rows.GroupBy(r => r.Id))
        {
            var row = group.First();
            var tenantList = new Dictionary<int, VehicleTenantOptionDto>();

            foreach (var m in members.Where(m => m.ContractId == row.ContractId))
            {
                tenantList[m.Id] = new VehicleTenantOptionDto(m.Id, m.FullName, m.UserId);
            }

            result.Add(new VehicleRoomOptionDto(
                row.Id,
                row.PropertyId,
                row.PropertyName,
                row.RoomCode,
                row.ContractId,
                row.ContractNumber,
                activeCounts.GetValueOrDefault(row.Id, 0),
                maxPerRoomMap.GetValueOrDefault(row.PropertyId, VehicleRules.DefaultMaxVehiclesPerRoom),
                tenantList.Values.OrderBy(t => t.FullName).ToList()));
        }

        return result;
    }

    public async Task<VehicleListResultDto> ListAsync(
        VehicleFilter filter,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(caller);

        List<int> allowedPropertyIds;
        List<int>? allowedRoomIds = null;

        if (caller.IsKhachThue)
        {
            var todayOnly = clock.TodayVietnam();
            allowedRoomIds = await VehicleScope.GetTenantRoomIdsAsync(db, caller, todayOnly, ct);
            allowedPropertyIds = await VehicleScope.GetTenantPropertyIdsAsync(db, caller, todayOnly, ct);
        }
        else
        {
            allowedPropertyIds = await VehicleScope.ApplyProperties(db, caller)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        if (filter.PropertyId.HasValue)
        {
            allowedPropertyIds = allowedPropertyIds.Where(id => id == filter.PropertyId.Value).ToList();
        }

        if (allowedPropertyIds.Count == 0)
        {
            return new VehicleListResultDto(
                [],
                new VehicleSummaryDto(0, 0, 0, 0, 0, 0m, 0, 0, 0));
        }

        var settingsMap = await db.VehiclePropertySettings.AsNoTracking()
            .Where(s => allowedPropertyIds.Contains(s.PropertyId))
            .ToDictionaryAsync(s => s.PropertyId, ct);

        var zonesMap = await db.ParkingZones.AsNoTracking()
            .Where(z => allowedPropertyIds.Contains(z.PropertyId))
            .ToDictionaryAsync(z => z.Id, ct);

        // Mục 53: lọc/sắp xếp ngay trên SQL. Không tải toàn bộ xe của nhà rồi Where trong bộ nhớ.
        var rows = await BuildRowsQuery(allowedPropertyIds, allowedRoomIds, filter).ToListAsync(ct);

        var dtos = await MapRowsToDtosAsync(rows, settingsMap, zonesMap, ct);

        // Thẻ tổng quan luôn tính trên toàn phạm vi (bỏ qua bộ lọc), gom nhóm bằng SQL.
        var groups = await db.Vehicles.AsNoTracking()
            .Where(v => allowedPropertyIds.Contains(v.PropertyId)
                && (allowedRoomIds == null || allowedRoomIds.Contains(v.RoomId)))
            .GroupBy(v => new { v.PropertyId, v.Status, v.VehicleType, v.ParkingZoneId })
            .Select(g => new
            {
                g.Key.PropertyId,
                g.Key.Status,
                g.Key.VehicleType,
                g.Key.ParkingZoneId,
                Count = g.Count(),
            })
            .ToListAsync(ct);

        var activeCount = 0;
        var inactiveCount = 0;
        var electricCount = 0;
        var manualCount = 0;
        var scooterCount = 0;
        var zoneOccupied = 0;
        var estimatedRevenue = 0m;
        foreach (var group in groups)
        {
            if (group.Status != VehicleStatus.Active)
            {
                inactiveCount += group.Count;
                continue;
            }

            activeCount += group.Count;
            switch (group.VehicleType)
            {
                case VehicleType.ElectricMotorbike: electricCount += group.Count; break;
                case VehicleType.ManualMotorbike: manualCount += group.Count; break;
                case VehicleType.Scooter: scooterCount += group.Count; break;
            }

            if (group.ParkingZoneId.HasValue)
            {
                zoneOccupied += group.Count;
            }

            var setting = settingsMap.GetValueOrDefault(group.PropertyId)
                ?? new VehiclePropertySetting { PropertyId = group.PropertyId };
            var zone = group.ParkingZoneId is { } zoneId ? zonesMap.GetValueOrDefault(zoneId) : null;
            estimatedRevenue += VehicleRules.CalculateMonthlyFee(group.VehicleType, setting, zone) * group.Count;
        }

        var now = clock.GetUtcNow();
        var activeGuestCount = await db.GuestVehicleNotices.AsNoTracking()
            .CountAsync(g => allowedPropertyIds.Contains(g.PropertyId)
                && (allowedRoomIds == null || allowedRoomIds.Contains(g.RoomId))
                && g.Status == GuestVehicleNoticeStatus.Active
                && g.ExpectedTo >= now, ct);

        var summary = new VehicleSummaryDto(
            TotalActiveCount: activeCount,
            TotalInactiveCount: inactiveCount,
            ElectricMotorbikeCount: electricCount,
            ManualMotorbikeCount: manualCount,
            ScooterCount: scooterCount,
            EstimatedMonthlyRevenue: estimatedRevenue,
            ActiveGuestNoticeCount: activeGuestCount,
            TotalZoneCapacity: zonesMap.Values.Where(z => z.IsActive).Sum(z => z.Capacity),
            TotalZoneOccupied: zoneOccupied);

        return new VehicleListResultDto(dtos, summary);
    }

    /// <summary>
    /// Xe + phòng + nhà + hợp đồng + khách thuê trong một truy vấn SQL.
    /// Mọi bộ lọc/sắp xếp phải nằm trước khi chiếu sang <see cref="VehicleRow"/> (record vị trí),
    /// vì EF không dịch được truy cập thành viên trên kết quả chiếu record (mục 53, 54).
    /// Hợp đồng/khách thuê dùng LEFT JOIN và bỏ query filter để xe không biến mất khi hợp đồng bị xóa mềm (mục 57).
    /// </summary>
    private IQueryable<VehicleRow> BuildRowsQuery(
        IReadOnlyCollection<int>? allowedPropertyIds,
        IReadOnlyCollection<int>? allowedRoomIds,
        VehicleFilter? filter = null,
        int? vehicleId = null)
    {
        var vehicles = db.Vehicles.AsNoTracking().AsQueryable();

        if (allowedPropertyIds is not null)
        {
            vehicles = vehicles.Where(v => allowedPropertyIds.Contains(v.PropertyId));
        }

        if (allowedRoomIds is not null)
        {
            vehicles = vehicles.Where(v => allowedRoomIds.Contains(v.RoomId));
        }

        if (vehicleId.HasValue)
        {
            var id = vehicleId.Value;
            vehicles = vehicles.Where(v => v.Id == id);
        }

        if (filter is not null)
        {
            if (filter.RoomId.HasValue)
            {
                var roomId = filter.RoomId.Value;
                vehicles = vehicles.Where(v => v.RoomId == roomId);
            }

            if (filter.ContractId.HasValue)
            {
                var contractId = filter.ContractId.Value;
                vehicles = vehicles.Where(v => v.ContractId == contractId);
            }

            if (filter.VehicleType.HasValue)
            {
                var vehicleType = filter.VehicleType.Value;
                vehicles = vehicles.Where(v => v.VehicleType == vehicleType);
            }

            if (filter.Status.HasValue)
            {
                var status = filter.Status.Value;
                vehicles = vehicles.Where(v => v.Status == status);
            }

            if (filter.ParkingZoneId.HasValue)
            {
                var zoneId = filter.ParkingZoneId.Value;
                vehicles = vehicles.Where(v => v.ParkingZoneId == zoneId);
            }
        }

        var joined = from v in vehicles
                     join r in db.Rooms.IgnoreQueryFilters().AsNoTracking() on v.RoomId equals r.Id
                     join p in db.Properties.IgnoreQueryFilters().AsNoTracking() on v.PropertyId equals p.Id
                     join c in db.Contracts.IgnoreQueryFilters().AsNoTracking() on v.ContractId equals c.ContractId into cGroup
                     from c in cGroup.DefaultIfEmpty()
                     join t in db.Tenants.IgnoreQueryFilters().AsNoTracking() on v.TenantId equals t.Id into tGroup
                     from t in tGroup.DefaultIfEmpty()
                     select new { Vehicle = v, RoomCode = r.Code, PropertyName = p.Name, Contract = c, Tenant = t };

        if (filter is not null && !string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var rawTerm = filter.Keyword.Trim();
            var plateTerm = VehicleRules.NormalizePlateNumber(rawTerm).ToLowerInvariant();
            var rawLower = rawTerm.ToLowerInvariant();

            // So khớp không phân biệt hoa/thường bằng LOWER ở phía SQL Server thay vì kéo về bộ nhớ.
            joined = joined.Where(x =>
                (plateTerm != string.Empty && x.Vehicle.PlateNumber.ToLower().Contains(plateTerm))
                || x.Vehicle.PlateNumber.ToLower().EndsWith(rawLower)
                || x.Vehicle.QrCodeToken.ToLower() == rawLower
                || x.RoomCode.ToLower().Contains(rawLower)
                || (x.Tenant != null && x.Tenant.FullName != null && x.Tenant.FullName.ToLower().Contains(rawLower))
                || (x.Vehicle.Brand != null && x.Vehicle.Brand.ToLower().Contains(rawLower))
                || (x.Vehicle.Color != null && x.Vehicle.Color.ToLower().Contains(rawLower)));
        }

        return joined
            .OrderBy(x => x.Vehicle.Status)
            .ThenBy(x => x.RoomCode)
            .ThenByDescending(x => x.Vehicle.StartedAt)
            .Select(x => new VehicleRow(
                x.Vehicle.Id,
                x.Vehicle.PropertyId,
                x.PropertyName,
                x.Vehicle.RoomId,
                x.RoomCode,
                x.Vehicle.ContractId,
                x.Contract != null ? x.Contract.ContractNumber : string.Empty,
                x.Vehicle.TenantId,
                x.Tenant != null ? x.Tenant.FullName : null,
                x.Vehicle.VehicleType,
                x.Vehicle.PlateNumber,
                x.Vehicle.Brand,
                x.Vehicle.Color,
                x.Vehicle.PhotoRef,
                x.Vehicle.Note,
                x.Vehicle.QrCodeToken,
                x.Vehicle.ParkingZoneId,
                x.Vehicle.ParkingSlotId,
                x.Vehicle.Status,
                x.Vehicle.StartedAt,
                x.Vehicle.EndedAt,
                x.Vehicle.EndReason));
    }

    private async Task<List<VehicleDto>> MapRowsToDtosAsync(
        IReadOnlyList<VehicleRow> rows,
        IReadOnlyDictionary<int, VehiclePropertySetting> settingsMap,
        IReadOnlyDictionary<int, ParkingZone> zonesMap,
        CancellationToken ct)
    {
        var slotIds = rows
            .Where(row => row.ParkingSlotId.HasValue)
            .Select(row => row.ParkingSlotId!.Value)
            .Distinct()
            .ToArray();

        var slotsMap = slotIds.Length == 0
            ? new Dictionary<int, ParkingSlot>()
            : await db.ParkingSlots.AsNoTracking()
                .Where(s => slotIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);

        var result = new List<VehicleDto>(rows.Count);
        foreach (var row in rows)
        {
            var setting = settingsMap.GetValueOrDefault(row.PropertyId)
                ?? new VehiclePropertySetting { PropertyId = row.PropertyId };
            var zone = row.ParkingZoneId is { } zoneId ? zonesMap.GetValueOrDefault(zoneId) : null;
            var slot = row.ParkingSlotId is { } slotId ? slotsMap.GetValueOrDefault(slotId) : null;
            result.Add(MapDto(row, setting, zone, slot));
        }

        return result;
    }

    private static VehicleDto MapDto(VehicleRow row, VehiclePropertySetting setting, ParkingZone? zone, ParkingSlot? slot)
    {
        var fee = row.Status == VehicleStatus.Active
            ? VehicleRules.CalculateMonthlyFee(row.VehicleType, setting, zone)
            : 0m;

        return new VehicleDto(
            row.Id,
            row.PropertyId,
            row.PropertyName,
            row.RoomId,
            row.RoomCode,
            row.ContractId,
            row.ContractNumber,
            row.TenantId,
            row.TenantName,
            row.VehicleType,
            row.PlateNumber,
            row.Brand,
            row.Color,
            row.PhotoRef,
            row.Note,
            row.QrCodeToken,
            row.ParkingZoneId,
            zone?.Name,
            row.ParkingSlotId,
            slot?.Code,
            row.Status,
            fee,
            row.StartedAt,
            row.EndedAt,
            row.EndReason);
    }

    private sealed record VehicleRow(
        int Id,
        int PropertyId,
        string PropertyName,
        int RoomId,
        string RoomCode,
        Guid ContractId,
        string ContractNumber,
        int? TenantId,
        string? TenantName,
        VehicleType VehicleType,
        string PlateNumber,
        string? Brand,
        string? Color,
        string? PhotoRef,
        string? Note,
        string QrCodeToken,
        int? ParkingZoneId,
        int? ParkingSlotId,
        VehicleStatus Status,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        string? EndReason);

    public async Task<IReadOnlyList<VehicleLookupItemDto>> LookupAsync(
        int? propertyId,
        string query,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        List<int> allowedPropertyIds;
        List<int>? allowedRoomIds = null;
        if (caller.IsKhachThue)
        {
            var today = clock.TodayVietnam();
            // Mục 8: Khách thuê chỉ được tra cứu xe thuộc phòng mình đang thuê, không thấy xe phòng khác cùng nhà.
            allowedRoomIds = await VehicleScope.GetTenantRoomIdsAsync(db, caller, today, ct);
            allowedPropertyIds = await VehicleScope.GetTenantPropertyIdsAsync(db, caller, today, ct);
        }
        else
        {
            allowedPropertyIds = await VehicleScope.ApplyProperties(db, caller)
                .Select(p => p.Id)
                .ToListAsync(ct);
        }

        if (propertyId.HasValue)
        {
            allowedPropertyIds = allowedPropertyIds.Where(id => id == propertyId.Value).ToList();
        }

        if (allowedPropertyIds.Count == 0)
        {
            return [];
        }

        var trimmed = query.Trim();
        var normalizedPlate = VehicleRules.NormalizePlateNumber(trimmed);

        var rows = await (from v in db.Vehicles.AsNoTracking()
                          join r in db.Rooms.IgnoreQueryFilters().AsNoTracking() on v.RoomId equals r.Id
                          join p in db.Properties.IgnoreQueryFilters().AsNoTracking() on v.PropertyId equals p.Id
                          join z in db.ParkingZones.AsNoTracking() on v.ParkingZoneId equals z.Id into zGroup
                          from z in zGroup.DefaultIfEmpty()
                          join s in db.ParkingSlots.AsNoTracking() on v.ParkingSlotId equals s.Id into sGroup
                          from s in sGroup.DefaultIfEmpty()
                          join t in db.Tenants.IgnoreQueryFilters().AsNoTracking() on v.TenantId equals t.Id into tGroup
                          from t in tGroup.DefaultIfEmpty()
                          where allowedPropertyIds.Contains(v.PropertyId)
                              && (allowedRoomIds == null || allowedRoomIds.Contains(v.RoomId))
                              && v.Status == VehicleStatus.Active
                              && (v.QrCodeToken == trimmed
                                  || (!string.IsNullOrEmpty(normalizedPlate) && v.PlateNumber.Contains(normalizedPlate))
                                  || r.Code.Contains(trimmed))
                          orderby r.Code, v.PlateNumber
                          select new VehicleLookupItemDto(
                              v.Id,
                              v.PropertyId,
                              p.Name,
                              v.RoomId,
                              r.Code,
                              v.PlateNumber,
                              v.VehicleType,
                              v.Brand,
                              v.Color,
                              z != null ? z.Name : null,
                              s != null ? s.Code : null,
                              // Bảo vệ quyền riêng tư (Mục 9): Khách thuê tra cứu không được thấy tên chủ xe hay SĐT
                              caller.IsKhachThue ? null : (t != null ? t.FullName : null)))
            .Take(20)
            .ToListAsync(ct);

        return rows;
    }

    public async Task<VehicleOperationResult> CreateAsync(
        VehicleCreateRequest request,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        if (!await VehicleScope.CanManageVehiclesAsync(db, request.PropertyId, caller, ct))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.Forbidden);
        }

        var normalizedPlate = VehicleRules.NormalizePlateNumber(request.PlateNumber);
        if (!VehicleRules.IsValidPlateNumber(normalizedPlate))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.InvalidPlateNumber);
        }

        var room = await db.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.RoomId && r.PropertyId == request.PropertyId, ct);
        if (room is null)
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.RoomNotFound);
        }

        var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
        var activeContract = await db.Contracts.AsNoTracking()
            .Where(c => c.RoomId == request.RoomId
                && c.Status == ContractStatus.Approved
                && c.StartDate <= today
                && c.EndDate >= today)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync(ct);
        if (activeContract is null)
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.NoActiveContract);
        }

        // Khách thuê được gán phải thuộc hợp đồng đang hiệu lực của phòng.
        if (request.TenantId.HasValue
            && !await db.ContractMembers.AsNoTracking()
                .AnyAsync(m => m.ContractId == activeContract.ContractId && m.TenantId == request.TenantId.Value, ct))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.TenantNotInRoom);
        }

        var now = clock.GetUtcNow();
        var setting = await VehicleSettingService.EnsureEntityAsync(db, request.PropertyId, now, ct);

        // Chống trùng biển số Active trong cùng nhà trọ
        var duplicatePlate = await db.Vehicles.AsNoTracking()
            .AnyAsync(v => v.PropertyId == request.PropertyId
                && v.PlateNumber == normalizedPlate
                && v.Status == VehicleStatus.Active, ct);
        if (duplicatePlate)
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.DuplicateActivePlate);
        }

        // Kiểm tra giới hạn số xe Active tối đa / phòng
        if (setting.MaxVehiclesPerRoom > 0)
        {
            var currentRoomCount = await db.Vehicles.AsNoTracking()
                .CountAsync(v => v.RoomId == request.RoomId && v.Status == VehicleStatus.Active, ct);
            if (currentRoomCount >= setting.MaxVehiclesPerRoom)
            {
                return VehicleOperationResult.Fail(VehicleErrorCodes.RoomVehicleLimitReached);
            }
        }

        ParkingZone? zone = null;
        ParkingSlot? slot = null;
        if (request.ParkingZoneId.HasValue || request.ParkingSlotId.HasValue)
        {
            if (!setting.EnableParkingZones)
            {
                return VehicleOperationResult.Fail(VehicleErrorCodes.ParkingZonesDisabled);
            }

            var zoneError = await ValidateZoneAndSlotAsync(
                request.PropertyId,
                request.VehicleType,
                request.ParkingZoneId,
                request.ParkingSlotId,
                excludeVehicleId: null,
                ct);
            if (zoneError.ErrorCode is not null)
            {
                return VehicleOperationResult.Fail(zoneError.ErrorCode);
            }

            zone = zoneError.Zone;
            slot = zoneError.Slot;
        }

        var qrToken = VehicleRules.GenerateQrCodeToken();
        while (await db.Vehicles.AsNoTracking().AnyAsync(v => v.QrCodeToken == qrToken, ct))
        {
            qrToken = VehicleRules.GenerateQrCodeToken();
        }

        var defaultTenantId = request.TenantId ?? await db.ContractMembers.AsNoTracking()
            .Where(m => m.ContractId == activeContract.ContractId)
            .Select(m => (int?)m.TenantId)
            .FirstOrDefaultAsync(ct);

        var vehicle = new Vehicle
        {
            PropertyId = request.PropertyId,
            RoomId = request.RoomId,
            ContractId = activeContract.ContractId,
            TenantId = defaultTenantId,
            VehicleType = request.VehicleType,
            PlateNumber = normalizedPlate,
            Brand = NullIfEmpty(request.Brand),
            Color = NullIfEmpty(request.Color),
            PhotoRef = NullIfEmpty(request.PhotoRef),
            Note = NullIfEmpty(request.Note),
            QrCodeToken = qrToken,
            ParkingZoneId = zone?.Id,
            ParkingSlotId = slot?.Id,
            Status = VehicleStatus.Active,
            StartedAt = now,
            CreatedByUserId = caller.UserId,
        };

        // Xe và ô đỗ phải được ghi trong cùng một transaction: tránh xe "mồ côi" khi bước sau lỗi.
        await using var transaction = await BeginWriteTransactionAsync(ct);
        db.Vehicles.Add(vehicle);
        try
        {
            await db.SaveChangesAsync(ct);

            if (slot is not null)
            {
                slot.Status = ParkingSlotStatus.Occupied;
                slot.CurrentVehicleId = vehicle.Id;
                await db.SaveChangesAsync(ct);
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }
        }
        catch (DbUpdateException ex) when (IsDuplicateActivePlateViolation(ex))
        {
            db.Entry(vehicle).State = EntityState.Detached;
            return VehicleOperationResult.Fail(VehicleErrorCodes.DuplicateActivePlate);
        }
        catch
        {
            // Transaction đã rollback; xóa tracker để lần ghi sau không vô tình chèn lại xe dở dang.
            db.ChangeTracker.Clear();
            throw;
        }

        await billing.SyncDraftInvoicesForContractAsync(activeContract.ContractId, ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.Created",
            caller.UserId,
            vehicle.PropertyId,
            $"Plate={vehicle.PlateNumber};Type={vehicle.VehicleType};Room={room.Code};Zone={zone?.Name ?? "-"};Slot={slot?.Code ?? "-"}",
            now,
            ct);

        await NotifyRoomTenantsAsync(
            vehicle.RoomId,
            activeContract.ContractId,
            VehicleLocalization.T(localizer, "NotifVehicleAddedTitle", "Đã đăng ký xe {0}",
                VehicleRules.FormatPlateForDisplay(vehicle.PlateNumber)),
            VehicleLocalization.T(localizer, "NotifVehicleAddedBody", "Xe {0} ({1}) đã được thêm vào phòng {2}.",
                vehicle.PlateNumber,
                VehicleLocalization.T(localizer, vehicle.VehicleType.ToString(), vehicle.VehicleType.ToString()),
                room.Code),
            ct);

        if (zone is not null)
        {
            await CheckAndNotifyZoneNearFullAsync(zone, setting.ZoneNearFullThresholdPercent, ct);
        }

        var dto = await GetVehicleDtoAsync(vehicle.Id, ct);
        return VehicleOperationResult.Success(dto);
    }

    public async Task<VehicleOperationResult> UpdateAsync(
        VehicleUpdateRequest request,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == request.Id, ct);
        if (vehicle is null)
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.VehicleNotFound);
        }

        if (!await VehicleScope.CanManageVehiclesAsync(db, vehicle.PropertyId, caller, ct))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.Forbidden);
        }

        var normalizedPlate = VehicleRules.NormalizePlateNumber(request.PlateNumber);
        if (!VehicleRules.IsValidPlateNumber(normalizedPlate))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.InvalidPlateNumber);
        }

        if (vehicle.Status == VehicleStatus.Active)
        {
            var duplicate = await db.Vehicles.AsNoTracking()
                .AnyAsync(v => v.PropertyId == vehicle.PropertyId
                    && v.PlateNumber == normalizedPlate
                    && v.Status == VehicleStatus.Active
                    && v.Id != vehicle.Id, ct);
            if (duplicate)
            {
                return VehicleOperationResult.Fail(VehicleErrorCodes.DuplicateActivePlate);
            }
        }

        var setting = await VehicleSettingService.EnsureEntityAsync(db, vehicle.PropertyId, clock.GetUtcNow(), ct);

        // Hợp đồng đang hiệu lực của phòng: dùng để xác thực khách thuê và trỏ xe về đúng hợp đồng.
        Guid? currentContractId = null;
        if (vehicle.Status == VehicleStatus.Active)
        {
            var todayDate = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
            currentContractId = await db.Contracts.AsNoTracking()
                .Where(c => c.RoomId == vehicle.RoomId
                    && c.Status == ContractStatus.Approved
                    && c.StartDate <= todayDate
                    && c.EndDate >= todayDate)
                .OrderByDescending(c => c.StartDate)
                .Select(c => (Guid?)c.ContractId)
                .FirstOrDefaultAsync(ct);
        }

        var effectiveContractId = currentContractId ?? vehicle.ContractId;

        // Khách thuê được gán phải thuộc hợp đồng đang hiệu lực của phòng.
        if (request.TenantId.HasValue
            && !await db.ContractMembers.AsNoTracking()
                .AnyAsync(m => m.ContractId == effectiveContractId && m.TenantId == request.TenantId.Value, ct))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.TenantNotInRoom);
        }

        // Ngữ nghĩa trường khu/ô đỗ khi cập nhật:
        //  - ClearParkingAssignment = true → gỡ khu/ô đỗ.
        //  - có gửi zone/slot             → gán theo yêu cầu (kiểm tra đầy đủ).
        //  - không gửi gì                  → giữ nguyên gán hiện tại (tránh xoá nhầm khi chỉ sửa biển số/màu).
        var typeChanged = vehicle.VehicleType != request.VehicleType;
        var explicitParkingRequest = request.ClearParkingAssignment
            || request.ParkingZoneId.HasValue
            || request.ParkingSlotId.HasValue;

        int? targetZoneId;
        int? targetSlotId;
        if (request.ClearParkingAssignment)
        {
            targetZoneId = null;
            targetSlotId = null;
        }
        else if (request.ParkingZoneId.HasValue || request.ParkingSlotId.HasValue)
        {
            targetZoneId = request.ParkingZoneId;
            targetSlotId = request.ParkingSlotId;
        }
        else
        {
            targetZoneId = vehicle.ParkingZoneId;
            targetSlotId = vehicle.ParkingSlotId;
        }

        ParkingZone? newZone = null;
        ParkingSlot? newSlot = null;
        if (targetZoneId.HasValue || targetSlotId.HasValue)
        {
            if (!setting.EnableParkingZones)
            {
                if (explicitParkingRequest)
                {
                    return VehicleOperationResult.Fail(VehicleErrorCodes.ParkingZonesDisabled);
                }

                // Tính năng khu/ô đỗ đã bị tắt sau khi xe được gán: gỡ gán cũ thay vì chặn cập nhật.
                targetZoneId = null;
                targetSlotId = null;
            }
            else if (explicitParkingRequest || typeChanged)
            {
                // Kiểm tra lại cả khi chỉ đổi loại xe để zone hiện tại không còn cho phép loại mới.
                var zoneError = await ValidateZoneAndSlotAsync(
                    vehicle.PropertyId,
                    request.VehicleType,
                    targetZoneId,
                    targetSlotId,
                    excludeVehicleId: vehicle.Id,
                    ct);
                if (zoneError.ErrorCode is not null)
                {
                    return VehicleOperationResult.Fail(zoneError.ErrorCode);
                }

                newZone = zoneError.Zone;
                newSlot = zoneError.Slot;
            }
            else
            {
                // Giữ nguyên gán cũ: chỉ nạp entity để cập nhật trạng thái ô đỗ.
                if (targetZoneId is { } keepZoneId)
                {
                    newZone = await db.ParkingZones.FirstOrDefaultAsync(
                        z => z.Id == keepZoneId && z.PropertyId == vehicle.PropertyId, ct);
                }

                if (targetSlotId is { } keepSlotId)
                {
                    newSlot = await db.ParkingSlots.FirstOrDefaultAsync(s => s.Id == keepSlotId, ct);
                }
            }
        }

        var resolvedZoneId = newZone?.Id ?? targetZoneId;
        var resolvedSlotId = newSlot?.Id ?? targetSlotId;

        // Giải phóng ô đỗ cũ nếu đổi/gỡ ô
        if (vehicle.ParkingSlotId.HasValue && vehicle.ParkingSlotId != resolvedSlotId)
        {
            var oldSlot = await db.ParkingSlots.FirstOrDefaultAsync(s => s.Id == vehicle.ParkingSlotId.Value, ct);
            if (oldSlot is not null)
            {
                oldSlot.Status = ParkingSlotStatus.Available;
                oldSlot.CurrentVehicleId = null;
            }
        }

        var previousContractId = vehicle.ContractId;

        vehicle.TenantId = request.TenantId;
        vehicle.VehicleType = request.VehicleType;
        vehicle.PlateNumber = normalizedPlate;
        vehicle.Brand = NullIfEmpty(request.Brand);
        vehicle.Color = NullIfEmpty(request.Color);
        vehicle.PhotoRef = NullIfEmpty(request.PhotoRef);
        vehicle.Note = NullIfEmpty(request.Note);
        vehicle.ParkingZoneId = resolvedZoneId;
        vehicle.ParkingSlotId = resolvedSlotId;

        // Hợp đồng gốc có thể đã được gia hạn; trỏ xe về hợp đồng đang hiệu lực của phòng
        // để phí gửi xe vào đúng kỳ thay vì hợp đồng cũ đã hết hạn.
        if (vehicle.Status == VehicleStatus.Active && currentContractId.HasValue)
        {
            vehicle.ContractId = currentContractId.Value;
        }

        // Ô đỗ được gán phải đánh dấu đang bị chiếm, không phụ thuộc trạng thái xe.
        if (newSlot is not null)
        {
            newSlot.Status = ParkingSlotStatus.Occupied;
            newSlot.CurrentVehicleId = vehicle.Id;
        }

        await db.SaveChangesAsync(ct);
        if (previousContractId != vehicle.ContractId)
        {
            await billing.SyncDraftInvoicesForContractAsync(previousContractId, ct);
        }

        await billing.SyncDraftInvoicesForContractAsync(vehicle.ContractId, ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.Updated",
            caller.UserId,
            vehicle.PropertyId,
            $"VehicleId={vehicle.Id};Plate={vehicle.PlateNumber};Type={vehicle.VehicleType};Zone={newZone?.Name ?? "-"};Slot={newSlot?.Code ?? "-"}",
            clock.GetUtcNow(),
            ct);

        var dto = await GetVehicleDtoAsync(vehicle.Id, ct);
        return VehicleOperationResult.Success(dto);
    }

    public async Task<VehicleOperationResult> DeactivateAsync(
        int vehicleId,
        string reason,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        if (string.IsNullOrWhiteSpace(reason))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.ReasonRequired);
        }

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct);
        if (vehicle is null)
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.VehicleNotFound);
        }

        if (!await VehicleScope.CanManageVehiclesAsync(db, vehicle.PropertyId, caller, ct))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.Forbidden);
        }

        if (vehicle.Status == VehicleStatus.Inactive)
        {
            var existingDto = await GetVehicleDtoAsync(vehicle.Id, ct);
            return VehicleOperationResult.Success(existingDto);
        }

        var now = clock.GetUtcNow();
        if (vehicle.ParkingSlotId.HasValue)
        {
            var slot = await db.ParkingSlots.FirstOrDefaultAsync(s => s.Id == vehicle.ParkingSlotId.Value, ct);
            if (slot is not null)
            {
                slot.Status = ParkingSlotStatus.Available;
                slot.CurrentVehicleId = null;
            }
        }

        vehicle.Status = VehicleStatus.Inactive;
        vehicle.EndedAt = now;
        vehicle.EndReason = reason.Trim();
        vehicle.ParkingSlotId = null;

        await db.SaveChangesAsync(ct);
        await billing.SyncDraftInvoicesForContractAsync(vehicle.ContractId, ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.Deactivated",
            caller.UserId,
            vehicle.PropertyId,
            $"VehicleId={vehicle.Id};Plate={vehicle.PlateNumber};Reason={vehicle.EndReason}",
            now,
            ct);

        await NotifyRoomTenantsAsync(
            vehicle.RoomId,
            vehicle.ContractId,
            VehicleLocalization.T(localizer, "NotifVehicleDeactivatedTitle", "Đã ngừng gửi xe {0}",
                VehicleRules.FormatPlateForDisplay(vehicle.PlateNumber)),
            VehicleLocalization.T(localizer, "NotifVehicleDeactivatedBody",
                "Xe {0} đã chuyển sang trạng thái ngừng gửi. Lý do: {1}",
                vehicle.PlateNumber, vehicle.EndReason),
            ct);

        var dto = await GetVehicleDtoAsync(vehicle.Id, ct);
        return VehicleOperationResult.Success(dto);
    }

    public async Task<VehicleOperationResult> ReactivateAsync(
        int vehicleId,
        int? parkingZoneId,
        int? parkingSlotId,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId, ct);
        if (vehicle is null)
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.VehicleNotFound);
        }

        if (!await VehicleScope.CanManageVehiclesAsync(db, vehicle.PropertyId, caller, ct))
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.Forbidden);
        }

        var today = clock.TodayVietnam().ToDateTime(TimeOnly.MinValue);
        var activeContract = await db.Contracts.AsNoTracking()
            .Where(c => c.RoomId == vehicle.RoomId
                && c.Status == ContractStatus.Approved
                && c.StartDate <= today
                && c.EndDate >= today)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync(ct);
        if (activeContract is null)
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.NoActiveContract);
        }

        var duplicatePlate = await db.Vehicles.AsNoTracking()
            .AnyAsync(v => v.PropertyId == vehicle.PropertyId
                && v.PlateNumber == vehicle.PlateNumber
                && v.Status == VehicleStatus.Active
                && v.Id != vehicle.Id, ct);
        if (duplicatePlate)
        {
            return VehicleOperationResult.Fail(VehicleErrorCodes.DuplicateActivePlate);
        }

        var now = clock.GetUtcNow();
        var setting = await VehicleSettingService.EnsureEntityAsync(db, vehicle.PropertyId, now, ct);
        if (setting.MaxVehiclesPerRoom > 0)
        {
            var currentRoomCount = await db.Vehicles.AsNoTracking()
                .CountAsync(v => v.RoomId == vehicle.RoomId && v.Status == VehicleStatus.Active, ct);
            if (currentRoomCount >= setting.MaxVehiclesPerRoom)
            {
                return VehicleOperationResult.Fail(VehicleErrorCodes.RoomVehicleLimitReached);
            }
        }

        var targetZoneId = parkingZoneId ?? vehicle.ParkingZoneId;
        var targetSlotId = parkingSlotId;
        ParkingZone? zone = null;
        ParkingSlot? slot = null;

        if (setting.EnableParkingZones && (targetZoneId.HasValue || targetSlotId.HasValue))
        {
            var zoneError = await ValidateZoneAndSlotAsync(
                vehicle.PropertyId,
                vehicle.VehicleType,
                targetZoneId,
                targetSlotId,
                excludeVehicleId: vehicle.Id,
                ct);
            if (zoneError.ErrorCode is not null)
            {
                return VehicleOperationResult.Fail(zoneError.ErrorCode);
            }

            zone = zoneError.Zone;
            slot = zoneError.Slot;
        }

        vehicle.ContractId = activeContract.ContractId;
        vehicle.Status = VehicleStatus.Active;
        vehicle.EndedAt = null;
        vehicle.EndReason = null;
        vehicle.ParkingZoneId = zone?.Id;
        vehicle.ParkingSlotId = slot?.Id;

        if (slot is not null)
        {
            slot.Status = ParkingSlotStatus.Occupied;
            slot.CurrentVehicleId = vehicle.Id;
        }

        await db.SaveChangesAsync(ct);
        await billing.SyncDraftInvoicesForContractAsync(vehicle.ContractId, ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.Reactivated",
            caller.UserId,
            vehicle.PropertyId,
            $"VehicleId={vehicle.Id};Plate={vehicle.PlateNumber}",
            now,
            ct);

        var dto = await GetVehicleDtoAsync(vehicle.Id, ct);
        return VehicleOperationResult.Success(dto);
    }

    public async Task<int> DeactivateContractVehiclesAsync(
        Guid contractId,
        string reason,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var activeVehicles = await db.Vehicles
            .Where(v => v.ContractId == contractId && v.Status == VehicleStatus.Active)
            .ToListAsync(ct);

        if (activeVehicles.Count == 0)
        {
            return 0;
        }

        var propertyId = activeVehicles[0].PropertyId;
        if (!await VehicleScope.CanManageVehiclesAsync(db, propertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var slotIds = activeVehicles
            .Where(v => v.ParkingSlotId.HasValue)
            .Select(v => v.ParkingSlotId!.Value)
            .Distinct()
            .ToArray();

        if (slotIds.Length > 0)
        {
            var slots = await db.ParkingSlots.Where(s => slotIds.Contains(s.Id)).ToListAsync(ct);
            foreach (var slot in slots)
            {
                slot.Status = ParkingSlotStatus.Available;
                slot.CurrentVehicleId = null;
            }
        }

        var now = clock.GetUtcNow();
        var cleanReason = string.IsNullOrWhiteSpace(reason) ? "Thanh lý / kết thúc hợp đồng" : reason.Trim();
        foreach (var vehicle in activeVehicles)
        {
            vehicle.Status = VehicleStatus.Inactive;
            vehicle.EndedAt = now;
            vehicle.EndReason = cleanReason;
            vehicle.ParkingSlotId = null;
        }

        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.ContractVehiclesDeactivated",
            caller.UserId,
            propertyId,
            $"ContractId={contractId};Count={activeVehicles.Count};Reason={cleanReason}",
            now,
            ct);

        return activeVehicles.Count;
    }

    public async Task<int> ReportVehicleIncidentAsync(
        VehicleIncidentCreateRequest request,
        PropertyCaller caller,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(caller);

        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.RoomId, ct)
            ?? throw new InvalidOperationException(VehicleErrorCodes.RoomNotFound);

        if (caller.IsKhachThue)
        {
            var tenantRoomIds = await VehicleScope.GetTenantRoomIdsAsync(db, caller, clock.TodayVietnam(), ct);
            if (!tenantRoomIds.Contains(room.Id))
            {
                throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
            }
        }
        else if (!await VehicleScope.CanAccessPropertyAsync(db, room.PropertyId, caller, ct))
        {
            throw new UnauthorizedAccessException(VehicleErrorCodes.Forbidden);
        }

        var normalizedPlate = VehicleRules.NormalizePlateNumber(request.PlateNumber);

        // Sự cố có thể gắn với một xe cụ thể; phải kiểm tra xe thuộc đúng phòng để không lộ/ghép nhầm.
        int? vehicleId = null;
        string? structuredPlate = null;
        if (request.VehicleId is { } vid && vid > 0)
        {
            var vehicle = await db.Vehicles.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Id == vid && v.RoomId == room.Id, ct)
                ?? throw new InvalidOperationException(VehicleErrorCodes.VehicleNotFound);

            vehicleId = vehicle.Id;
            structuredPlate = vehicle.PlateNumber;
            if (string.IsNullOrEmpty(normalizedPlate))
            {
                normalizedPlate = vehicle.PlateNumber;
            }
        }
        else if (!string.IsNullOrEmpty(normalizedPlate))
        {
            structuredPlate = normalizedPlate;
        }

        var plateTag = string.IsNullOrEmpty(normalizedPlate) ? "XE-LA" : normalizedPlate;
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? $"[Xe {plateTag}] Sự cố bãi xe"
            : $"[Xe {plateTag}] {request.Title.Trim()}";
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Sự cố liên quan đến phương tiện biển số {plateTag} tại phòng {room.Code}."
            : request.Description.Trim();

        var now = clock.GetUtcNow();
        var incident = new MaintenanceIncident
        {
            RoomId = room.Id,
            VehicleId = vehicleId,
            VehiclePlateNumber = structuredPlate,
            ReportedByUserId = caller.UserId,
            Title = title.Length > 200 ? title[..200] : title,
            Description = description,
            Category = IncidentCategory.Other,
            Urgency = IncidentUrgency.Normal,
            Status = IncidentStatus.New,
            CreatedAt = now.UtcDateTime,
        };

        db.MaintenanceIncidents.Add(incident);
        await db.SaveChangesAsync(ct);

        await audit.RecordAsync(
            AuditLogCategory.Business,
            "Vehicle.IncidentReported",
            caller.UserId,
            room.PropertyId,
            $"IncidentId={incident.Id};Plate={plateTag};Room={room.Code}",
            now,
            ct);

        return incident.Id;
    }

    private async Task<(string? ErrorCode, ParkingZone? Zone, ParkingSlot? Slot)> ValidateZoneAndSlotAsync(
        int propertyId,
        VehicleType vehicleType,
        int? zoneId,
        int? slotId,
        int? excludeVehicleId,
        CancellationToken ct)
    {
        ParkingSlot? slot = null;
        if (slotId is { } sId && sId > 0)
        {
            slot = await db.ParkingSlots.FirstOrDefaultAsync(s => s.Id == sId, ct);
            if (slot is null)
            {
                return (VehicleErrorCodes.SlotNotFound, null, null);
            }

            zoneId ??= slot.ParkingZoneId;
            if (slot.ParkingZoneId != zoneId.Value)
            {
                return (VehicleErrorCodes.SlotNotFound, null, null);
            }

            if (slot.Status == ParkingSlotStatus.Maintenance)
            {
                return (VehicleErrorCodes.SlotNotAvailable, null, null);
            }

            var occupiedByOther = await db.Vehicles.AsNoTracking()
                .AnyAsync(v => v.ParkingSlotId == slot.Id
                    && v.Status == VehicleStatus.Active
                    && (!excludeVehicleId.HasValue || v.Id != excludeVehicleId.Value), ct);
            if (occupiedByOther || (slot.CurrentVehicleId.HasValue && slot.CurrentVehicleId != excludeVehicleId))
            {
                return (VehicleErrorCodes.SlotCurrentlyOccupied, null, null);
            }
        }

        if (zoneId is not { } zId || zId <= 0)
        {
            return (null, null, null);
        }

        var zone = await db.ParkingZones.FirstOrDefaultAsync(z => z.Id == zId && z.PropertyId == propertyId, ct);
        if (zone is null || !zone.IsActive)
        {
            return (VehicleErrorCodes.ZoneNotFound, null, null);
        }

        if (!VehicleRules.IsVehicleTypeAllowed(vehicleType, zone.AllowedVehicleTypes))
        {
            return (VehicleErrorCodes.VehicleTypeNotAllowedInZone, null, null);
        }

        var occupiedInZone = await db.Vehicles.AsNoTracking()
            .CountAsync(v => v.ParkingZoneId == zone.Id
                && v.Status == VehicleStatus.Active
                && (!excludeVehicleId.HasValue || v.Id != excludeVehicleId.Value), ct);
        if (occupiedInZone >= zone.Capacity)
        {
            return (VehicleErrorCodes.ZoneFull, null, null);
        }

        return (null, zone, slot);
    }

    private async Task NotifyRoomTenantsAsync(
        int roomId,
        Guid contractId,
        string title,
        string body,
        CancellationToken ct)
    {
        var userIds = await (from m in db.ContractMembers.AsNoTracking()
                             join t in db.Tenants.IgnoreQueryFilters().AsNoTracking() on m.TenantId equals t.Id
                             where m.ContractId == contractId && t.UserId != null
                             select t.UserId!)
            .Distinct()
            .ToListAsync(ct);

        foreach (var userId in userIds)
        {
            await notifications.NotifyAsync(userId, title, body, "/my/vehicles");
        }
    }

    private async Task CheckAndNotifyZoneNearFullAsync(
        ParkingZone zone,
        int thresholdPercent,
        CancellationToken ct)
    {
        if (zone.Capacity <= 0)
        {
            return;
        }

        var occupied = await db.Vehicles.AsNoTracking()
            .CountAsync(v => v.ParkingZoneId == zone.Id && v.Status == VehicleStatus.Active, ct);
        var percent = (int)Math.Round(occupied * 100.0 / zone.Capacity);
        if (percent < thresholdPercent)
        {
            return;
        }

        // Chống spam: mỗi khu/ngưỡng chỉ báo một lần trong mỗi khung thời gian (bug 43).
        if (!ZoneNearFullThrottle.TryAcquire(zone.Id, thresholdPercent, clock.GetUtcNow()))
        {
            return;
        }

        var ownerId = await db.Properties.AsNoTracking()
            .Where(p => p.Id == zone.PropertyId)
            .Select(p => p.OwnerId)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(ownerId))
        {
            await notifications.NotifyAsync(
                ownerId,
                VehicleLocalization.T(localizer, "NotifZoneNearFullTitle",
                    "Khu đỗ xe \"{0}\" đạt {1}% sức chứa", zone.Name, percent),
                VehicleLocalization.T(localizer, "NotifZoneNearFullBody",
                    "Khu \"{0}\" hiện có {1}/{2} xe đang gửi.", zone.Name, occupied, zone.Capacity),
                "/vehicles/zones");
        }
    }

    /// <summary>Mục 54: lấy đúng một xe theo Id thay vì nạp toàn bộ danh sách.</summary>
    private async Task<VehicleDto?> GetVehicleDtoAsync(int id, CancellationToken ct)
    {
        var row = await BuildRowsQuery(null, null, vehicleId: id).FirstOrDefaultAsync(ct);
        if (row is null)
        {
            return null;
        }

        var setting = await db.VehiclePropertySettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PropertyId == row.PropertyId, ct)
            ?? new VehiclePropertySetting { PropertyId = row.PropertyId };
        var zone = row.ParkingZoneId is { } zoneId
            ? await db.ParkingZones.AsNoTracking().FirstOrDefaultAsync(z => z.Id == zoneId, ct)
            : null;
        var slot = row.ParkingSlotId is { } slotId
            ? await db.ParkingSlots.AsNoTracking().FirstOrDefaultAsync(s => s.Id == slotId, ct)
            : null;

        return MapDto(row, setting, zone, slot);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Mở transaction khi chạy trên nhà cung cấp quan hệ và chưa có transaction bao ngoài.
    /// EF InMemory (unit test) không hỗ trợ transaction nên trả null và để thao tác chạy trực tiếp.
    /// </summary>
    private async Task<IDbContextTransaction?> BeginWriteTransactionAsync(CancellationToken ct)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null)
        {
            return null;
        }

        return await db.Database.BeginTransactionAsync(ct);
    }

    /// <summary>
    /// Chỉ coi DbUpdateException là trùng biển số khi SQL Server báo vi phạm unique index của Vehicles
    /// trên cột PlateNumber; các lỗi FK/check/QR-token khác phải nổi lên để không bị che giấu.
    /// </summary>
    private static bool IsDuplicateActivePlateViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 } sql
                && (sql.Message.Contains("PlateNumber", StringComparison.OrdinalIgnoreCase)
                    || sql.Message.Contains("IX_Vehicles_PropertyId_PlateNumber", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
