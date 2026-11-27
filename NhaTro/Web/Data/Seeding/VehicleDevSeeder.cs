using Microsoft.EntityFrameworkCore;
using Web.Features.Contracts.Enums;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;

namespace Web.Data.Seeding;

/// <summary>
/// Gieo dữ liệu mẫu cho module Quản lý xe ở môi trường Development khi bảng Vehicles đang trống
/// và nhà trọ đã có hợp đồng đang hiệu lực (Approved). Không ghi đè dữ liệu sẵn có.
/// </summary>
public static class VehicleDevSeeder
{
    public static async Task EnsureAsync(IServiceProvider services, ILogger logger)
    {
        var dbFactory = services.GetService<IDbContextFactory<ApplicationDbContext>>();
        if (dbFactory is null)
        {
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Vehicles.AnyAsync())
        {
            return;
        }

        var today = DateTime.UtcNow.Date;

        var activeRooms = await (from contract in db.Contracts.AsNoTracking()
                                 join room in db.Rooms.AsNoTracking() on contract.RoomId equals room.Id
                                 join property in db.Properties.AsNoTracking() on room.PropertyId equals property.Id
                                 where contract.Status == ContractStatus.Approved
                                     && contract.StartDate <= today
                                     && contract.EndDate >= today
                                     && !room.IsDeleted
                                     && !property.IsDeleted
                                 orderby property.Id, room.Id
                                 select new
                                 {
                                     property.Id,
                                     property.OwnerId,
                                     property.Name,
                                     RoomId = room.Id,
                                     RoomCode = room.Code,
                                     contract.ContractId,
                                 })
            .Take(3)
            .ToListAsync();

        if (activeRooms.Count == 0)
        {
            return;
        }

        var primaryPropertyId = activeRooms[0].Id;
        var ownerId = activeRooms[0].OwnerId;
        var propertyRooms = activeRooms.Where(r => r.Id == primaryPropertyId).ToList();
        var now = DateTimeOffset.UtcNow;

        var setting = await db.VehiclePropertySettings
            .FirstOrDefaultAsync(s => s.PropertyId == primaryPropertyId);
        if (setting is null)
        {
            setting = new VehiclePropertySetting
            {
                PropertyId = primaryPropertyId,
                ElectricMotorbikeMonthlyFee = 50_000m,
                ManualMotorbikeMonthlyFee = 80_000m,
                ScooterMonthlyFee = 100_000m,
                MaxVehiclesPerRoom = 3,
                EnableParkingZones = true,
                ZoneNearFullThresholdPercent = 85,
                AllowTenantGuestNotice = true,
                AllowBlockingAlert = true,
                AllowManagerManageVehicles = true,
                UpdatedByUserId = ownerId,
                UpdatedAt = now,
            };
            db.VehiclePropertySettings.Add(setting);
        }
        else if (!setting.EnableParkingZones)
        {
            // Chủ trọ đã tắt khu đỗ: tôn trọng cấu hình, không gieo khu/ô đỗ mẫu.
            return;
        }

        var indoorZone = new ParkingZone
        {
            PropertyId = primaryPropertyId,
            Name = "Khu trong nhà A",
            IsIndoor = true,
            Capacity = 8,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.ManualMotorbike | AllowedVehicleTypeFlags.Scooter,
            HasSlots = true,
            IsFree = false,
            IsActive = true,
            CreatedAt = now,
        };

        var outdoorZone = new ParkingZone
        {
            PropertyId = primaryPropertyId,
            Name = "Bãi ngoài trời B (An toàn xe điện)",
            IsIndoor = false,
            Capacity = 6,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.All,
            HasSlots = true,
            IsFree = false,
            IsActive = true,
            CreatedAt = now,
        };

        db.ParkingZones.AddRange(indoorZone, outdoorZone);
        await db.SaveChangesAsync();

        var indoorSlots = new List<ParkingSlot>();
        for (var i = 1; i <= 8; i++)
        {
            indoorSlots.Add(new ParkingSlot
            {
                ParkingZoneId = indoorZone.Id,
                Code = $"A{i:D2}",
                Status = i == 8 ? ParkingSlotStatus.Maintenance : ParkingSlotStatus.Available,
            });
        }

        var outdoorSlots = new List<ParkingSlot>();
        for (var i = 1; i <= 6; i++)
        {
            outdoorSlots.Add(new ParkingSlot
            {
                ParkingZoneId = outdoorZone.Id,
                Code = $"B{i:D2}",
                Status = ParkingSlotStatus.Available,
            });
        }

        db.ParkingSlots.AddRange(indoorSlots);
        db.ParkingSlots.AddRange(outdoorSlots);
        await db.SaveChangesAsync();

        var room1 = propertyRooms[0];
        var room2 = propertyRooms.Count > 1 ? propertyRooms[1] : room1;

        var tenant1Id = await db.ContractMembers.AsNoTracking()
            .Where(m => m.ContractId == room1.ContractId)
            .Select(m => (int?)m.TenantId)
            .FirstOrDefaultAsync();

        var tenant2Id = await db.ContractMembers.AsNoTracking()
            .Where(m => m.ContractId == room2.ContractId)
            .Select(m => (int?)m.TenantId)
            .FirstOrDefaultAsync();

        var v1 = new Vehicle
        {
            PropertyId = primaryPropertyId,
            RoomId = room1.RoomId,
            ContractId = room1.ContractId,
            TenantId = tenant1Id,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X112345",
            Brand = "Honda Wave Alpha",
            Color = "Đen Bạc",
            Note = "Xe đi học hằng ngày",
            QrCodeToken = VehicleRules.GenerateQrCodeToken(),
            ParkingZoneId = indoorZone.Id,
            ParkingSlotId = indoorSlots[0].Id,
            Status = VehicleStatus.Active,
            StartedAt = now.AddDays(-20),
            CreatedByUserId = ownerId,
        };

        var v2 = new Vehicle
        {
            PropertyId = primaryPropertyId,
            RoomId = room1.RoomId,
            ContractId = room1.ContractId,
            TenantId = tenant1Id,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X267890",
            Brand = "Honda Vision",
            Color = "Trắng",
            QrCodeToken = VehicleRules.GenerateQrCodeToken(),
            ParkingZoneId = indoorZone.Id,
            ParkingSlotId = indoorSlots[1].Id,
            Status = VehicleStatus.Active,
            StartedAt = now.AddDays(-15),
            CreatedByUserId = ownerId,
        };

        var v3 = new Vehicle
        {
            PropertyId = primaryPropertyId,
            RoomId = room2.RoomId,
            ContractId = room2.ContractId,
            TenantId = tenant2Id,
            VehicleType = VehicleType.ElectricMotorbike,
            PlateNumber = "59MD188899",
            Brand = "VinFast Evo200",
            Color = "Xanh Rêu",
            Note = "Đậu bãi ngoài trời B",
            QrCodeToken = VehicleRules.GenerateQrCodeToken(),
            ParkingZoneId = outdoorZone.Id,
            ParkingSlotId = outdoorSlots[0].Id,
            Status = VehicleStatus.Active,
            StartedAt = now.AddDays(-10),
            CreatedByUserId = ownerId,
        };

        db.Vehicles.AddRange(v1, v2, v3);
        await db.SaveChangesAsync();

        indoorSlots[0].Status = ParkingSlotStatus.Occupied;
        indoorSlots[0].CurrentVehicleId = v1.Id;
        indoorSlots[1].Status = ParkingSlotStatus.Occupied;
        indoorSlots[1].CurrentVehicleId = v2.Id;
        outdoorSlots[0].Status = ParkingSlotStatus.Occupied;
        outdoorSlots[0].CurrentVehicleId = v3.Id;

        if (tenant1Id.HasValue)
        {
            db.GuestVehicleNotices.Add(new GuestVehicleNotice
            {
                PropertyId = primaryPropertyId,
                RoomId = room1.RoomId,
                TenantId = tenant1Id.Value,
                CreatedByUserId = ownerId,
                PlateNumber = "59G199999",
                VehicleType = VehicleType.ManualMotorbike,
                GuestName = "Trần Minh Khoa (Bạn cùng lớp)",
                ExpectedFrom = now.AddHours(-2),
                ExpectedTo = now.AddHours(14),
                Note = "Ở lại làm đồ án qua đêm",
                Status = GuestVehicleNoticeStatus.Active,
                CreatedAt = now.AddHours(-2),
            });
        }

        await db.SaveChangesAsync();

        logger.LogInformation(
            "Da gieo du lieu mau Quan ly xe (3 xe, 2 khu do, 14 o do, 1 xe gui nho) cho nha tro {PropertyName}.",
            activeRooms[0].Name);
    }
}
