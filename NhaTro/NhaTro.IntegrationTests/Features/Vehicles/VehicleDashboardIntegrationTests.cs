using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Services;

namespace NhaTro.IntegrationTests.Features.Vehicles;

public sealed class VehicleDashboardIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly DateTimeOffset FrozenNow = new(2026, 11, 15, 3, 0, 0, TimeSpan.Zero);

    [SqlServerFact]
    public async Task Dashboard_kpis_count_active_vehicles_zones_and_current_month_parking_revenue()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();

        var property = await SeedPropertyAsync(db, "owner-vd-1", "Nhà trọ Dashboard");
        var room = new Room { PropertyId = property.Id, Code = "P101", StandardPrice = 3_000_000m, Status = RoomStatus.Rented };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            ContractNumber = "HD-VD-1",
            RoomId = room.Id,
            StartDate = FrozenNow.Date.AddMonths(-2),
            EndDate = FrozenNow.Date.AddMonths(4),
            SnapshotPrice = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);

        var activeZone = new ParkingZone
        {
            PropertyId = property.Id, Name = "Khu A", Capacity = 10, HasSlots = false,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.All, IsActive = true, CreatedAt = FrozenNow,
        };
        var inactiveZone = new ParkingZone
        {
            PropertyId = property.Id, Name = "Khu B", Capacity = 5, HasSlots = false,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.All, IsActive = false, CreatedAt = FrozenNow,
        };
        db.ParkingZones.AddRange(activeZone, inactiveZone);
        await db.SaveChangesAsync();

        db.Vehicles.AddRange(
            NewVehicle(property.Id, room.Id, contract.ContractId, VehicleType.ElectricMotorbike, "59E100001", activeZone.Id),
            NewVehicle(property.Id, room.Id, contract.ContractId, VehicleType.ElectricMotorbike, "59E100002", activeZone.Id),
            NewVehicle(property.Id, room.Id, contract.ContractId, VehicleType.ManualMotorbike, "59M100003", activeZone.Id),
            NewVehicle(property.Id, room.Id, contract.ContractId, VehicleType.Scooter, "59S100004", null),
            NewVehicle(property.Id, room.Id, contract.ContractId, VehicleType.Scooter, "59S100005", inactiveZone.Id, VehicleStatus.Inactive));

        var issued = new Invoice
        {
            ContractId = contract.ContractId,
            PeriodStart = new DateOnly(2026, 11, 1),
            PeriodEnd = new DateOnly(2026, 11, 30),
            Status = InvoiceStatus.Issued,
            TotalAmount = 3_150_000m,
        };
        issued.Items.Add(new InvoiceItem { Description = "Phí gửi xe (3 xe)", Amount = 150_000m, DisplayOrder = 1 });
        issued.Items.Add(new InvoiceItem { Description = "Tiền phòng", Amount = 3_000_000m, DisplayOrder = 2 });

        var draft = new Invoice
        {
            ContractId = contract.ContractId,
            PeriodStart = new DateOnly(2026, 12, 1),
            PeriodEnd = new DateOnly(2026, 12, 31),
            Status = InvoiceStatus.Draft,
            TotalAmount = 3_000_000m,
        };
        draft.Items.Add(new InvoiceItem { Description = "Phí gửi xe (3 xe)", Amount = 999_000m, DisplayOrder = 1 });

        var previousMonth = new Invoice
        {
            ContractId = contract.ContractId,
            PeriodStart = new DateOnly(2026, 10, 1),
            PeriodEnd = new DateOnly(2026, 10, 31),
            Status = InvoiceStatus.Paid,
            TotalAmount = 100_000m,
        };
        previousMonth.Items.Add(new InvoiceItem { Description = "Phí gửi xe (1 xe)", Amount = 100_000m, DisplayOrder = 1 });

        db.Invoices.AddRange(issued, draft, previousMonth);
        await db.SaveChangesAsync();

        var service = new VehicleDashboardService(db, new FixedTimeProvider(FrozenNow));
        var kpis = await service.GetKpisAsync(property.Id, new PropertyCaller("owner-vd-1", Roles.ChuTro));

        Assert.Equal(4, kpis.ActiveTotal);
        Assert.Equal(2, kpis.ElectricMotorbike);
        Assert.Equal(1, kpis.ManualMotorbike);
        Assert.Equal(1, kpis.Scooter);
        Assert.Equal(10, kpis.ZoneCapacity);
        Assert.Equal(3, kpis.ZoneOccupied);
        Assert.Equal(150_000m, kpis.MonthlyParkingRevenue);
    }

    [SqlServerFact]
    public async Task Dashboard_kpis_reject_property_outside_caller_scope()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();

        var mine = await SeedPropertyAsync(db, "owner-vd-2", "Nhà của tôi");
        var other = await SeedPropertyAsync(db, "owner-vd-3", "Nhà người khác");

        var service = new VehicleDashboardService(db, new FixedTimeProvider(FrozenNow));
        var kpis = await service.GetKpisAsync(mine.Id, new PropertyCaller("owner-vd-2", Roles.ChuTro));
        Assert.Equal(0, kpis.ActiveTotal);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.GetKpisAsync(other.Id, new PropertyCaller("owner-vd-2", Roles.ChuTro)));
    }

    private static async Task<Property> SeedPropertyAsync(ApplicationDbContext db, string ownerId, string name)
    {
        if (!db.Users.Local.Any(u => u.Id == ownerId) && !await db.Users.AnyAsync(u => u.Id == ownerId))
        {
            db.Users.Add(new ApplicationUser
            {
                Id = ownerId,
                UserName = $"{ownerId}@test.local",
                Email = $"{ownerId}@test.local",
                FullName = name,
                ApprovedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var property = new Property { Name = name, Address = "1 Đường Test", OwnerId = ownerId, CreatedAt = DateTime.UtcNow };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        return property;
    }

    private static Vehicle NewVehicle(
        int propertyId, int roomId, Guid contractId, VehicleType type, string plate, int? zoneId,
        VehicleStatus status = VehicleStatus.Active) => new()
        {
            PropertyId = propertyId,
            RoomId = roomId,
            ContractId = contractId,
            VehicleType = type,
            PlateNumber = plate,
            QrCodeToken = $"qr-{plate}",
            ParkingZoneId = zoneId,
            Status = status,
            StartedAt = FrozenNow.AddDays(-10),
            CreatedByUserId = "owner-vd-1",
        };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}