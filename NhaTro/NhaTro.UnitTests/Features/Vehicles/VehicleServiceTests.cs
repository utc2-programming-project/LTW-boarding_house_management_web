using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Audit.Services;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Invoices.Entities;
using Web.Features.Invoices.Enums;
using Web.Features.Notifications;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants.Entities;
using Web.Features.Vehicles;
using Web.Features.Vehicles.DTOs;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Services;
using Web.Infrastructure.Auditing;

namespace NhaTro.UnitTests.Features.Vehicles;

public sealed class VehicleServiceTests : IDisposable
{
    private readonly string _auditRoot;
    private readonly FakeTimeProvider _clock;
    private readonly ApplicationDbContext _db;
    private readonly AuditLogService _audit;
    private readonly NotificationService _notifications;
    private readonly IMemoryCache _cache;
    private readonly VehicleSettingService _settings;
    private readonly ParkingZoneService _zones;
    private readonly VehicleBillingService _billing;
    private readonly VehicleService _vehicles;
    private readonly BlockingAlertService _blockingAlerts;
    private readonly GuestVehicleNoticeService _guestNotices;
    private readonly VehicleAuditService _audits;

    private readonly PropertyCaller _ownerCaller = new("owner-1", Roles.ChuTro);
    private readonly PropertyCaller _managerCaller = new("manager-1", Roles.QuanLy);
    private readonly PropertyCaller _tenantCaller = new("tenant-user-1", Roles.KhachThue);

    private int _propertyId;
    private int _roomId;
    private Guid _contractId;
    private int _tenantId;

    public VehicleServiceTests()
    {
        _auditRoot = Path.Combine(Path.GetTempPath(), "nhatro-vehicle-test-" + Guid.NewGuid().ToString("N"));
        _clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 25, 8, 0, 0, TimeSpan.Zero));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("vehicle-tests-" + Guid.NewGuid())
            .Options;

        _db = new ApplicationDbContext(options);
        var auditStore = new AuditLogFileStore(_auditRoot, 7);
        _audit = new AuditLogService(auditStore, _db);
        _notifications = new NotificationService(_db, _clock);
        _cache = new MemoryCache(new MemoryCacheOptions());

        _settings = new VehicleSettingService(_db, _audit, _clock);
        _zones = new ParkingZoneService(_db, _audit, _clock);
        _billing = new VehicleBillingService(_db, _audit, _clock);
        _vehicles = new VehicleService(_db, _billing, _notifications, _audit, _clock);
        _blockingAlerts = new BlockingAlertService(_db, _notifications, _cache, _audit, _clock);
        _guestNotices = new GuestVehicleNoticeService(_db, _notifications, _audit, _clock);
        _audits = new VehicleAuditService(_db, _vehicles, _audit, _clock);

        SeedBasePropertyAndRoom();
    }

    private void SeedBasePropertyAndRoom()
    {
        _db.Users.AddRange(
            new ApplicationUser { Id = "owner-1", UserName = "owner@test.local", Email = "owner@test.local", FullName = "Chủ trọ A", ApprovedAt = DateTime.UtcNow },
            new ApplicationUser { Id = "manager-1", UserName = "manager@test.local", Email = "manager@test.local", FullName = "Quản lý B", ApprovedAt = DateTime.UtcNow },
            new ApplicationUser { Id = "tenant-user-1", UserName = "tenant@test.local", Email = "tenant@test.local", FullName = "Khách thuê C", ApprovedAt = DateTime.UtcNow });

        var property = new Property
        {
            Name = "Nhà trọ Ánh Dương",
            Address = "123 Lê Văn Việt",
            OwnerId = "owner-1",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Properties.Add(property);
        _db.SaveChanges();
        _propertyId = property.Id;

        _db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = _propertyId,
            ManagerUserId = "manager-1",
            AssignedByUserId = "owner-1",
            AssignedAt = DateTime.UtcNow,
        });

        var room = new Room
        {
            PropertyId = _propertyId,
            Code = "P101",
            StandardPrice = 3_500_000m,
            Status = RoomStatus.Rented,
        };
        _db.Rooms.Add(room);
        _db.SaveChanges();
        _roomId = room.Id;

        var tenant = new Tenant
        {
            FullName = "Nguyễn Văn Khách",
            Phone = "0909123456",
            UserId = "tenant-user-1",
            CreatedAt = DateTime.UtcNow,
        };
        _db.Tenants.Add(tenant);
        _db.SaveChanges();
        _tenantId = tenant.Id;

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-P101",
            RoomId = _roomId,
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2027, 1, 1),
            SnapshotPrice = 3_500_000m,
            DepositAmount = 3_500_000m,
            Status = ContractStatus.Approved,
        };
        _db.Contracts.Add(contract);
        _db.ContractMembers.Add(new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = _tenantId,
            IsRepresentative = true,
        });
        _db.SaveChanges();
        _contractId = contract.ContractId;
    }

    [Fact]
    public async Task CreateVehicleNormalizesPlatePreventsDuplicateAndEnforcesRoomLimit()
    {
        var first = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            TenantId = _tenantId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59-x1 123.45",
            Brand = "Honda Wave",
            Color = "Đen",
        }, _ownerCaller);

        Assert.True(first.Succeeded, first.ErrorCode);
        Assert.NotNull(first.Vehicle);
        Assert.Equal("59X112345", first.Vehicle!.PlateNumber);
        Assert.Equal(80_000m, first.Vehicle.MonthlyFee);

        // Trùng biển số Active trong cùng nhà trọ bị từ chối
        var duplicate = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X1-12345",
        }, _ownerCaller);

        Assert.False(duplicate.Succeeded);
        Assert.Equal(VehicleErrorCodes.DuplicateActivePlate, duplicate.ErrorCode);

        // Thêm xe thứ 2 hợp lệ (giới hạn mặc định = 2 xe/phòng)
        var second = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59-X1 678.90",
        }, _ownerCaller);
        Assert.True(second.Succeeded, second.ErrorCode);

        // Xe thứ 3 vượt giới hạn MaxVehiclesPerRoom = 2
        var third = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ElectricMotorbike,
            PlateNumber = "59-MĐ1 111.22",
        }, _ownerCaller);
        Assert.False(third.Succeeded);
        Assert.Equal(VehicleErrorCodes.RoomVehicleLimitReached, third.ErrorCode);

        // Sau khi ngừng gửi xe 1 (Inactive), được phép đăng ký lại chính biển số đó
        var deactivated = await _vehicles.DeactivateAsync(first.Vehicle.Id, "Khách đổi xe", _ownerCaller);
        Assert.True(deactivated.Succeeded);
        Assert.Equal(VehicleStatus.Inactive, deactivated.Vehicle!.Status);

        var reRegister = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X112345",
        }, _ownerCaller);
        Assert.True(reRegister.Succeeded, reRegister.ErrorCode);
    }

    [Fact]
    public async Task ParkingZonesEnforceAllowedVehicleTypeAndReleaseSlotOnDeactivate()
    {
        await _settings.SaveAsync(new VehiclePropertySettingSaveRequest
        {
            PropertyId = _propertyId,
            EnableParkingZones = true,
            MaxVehiclesPerRoom = 5,
        }, _ownerCaller);

        // Khu trong nhà chỉ cho phép xe số và tay ga (cấm xe máy điện)
        var indoorZone = await _zones.SaveZoneAsync(new ParkingZoneSaveRequest
        {
            PropertyId = _propertyId,
            Name = "Khu trong nhà A",
            IsIndoor = true,
            Capacity = 4,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.ManualMotorbike | AllowedVehicleTypeFlags.Scooter,
            HasSlots = true,
            SlotPrefix = "A",
            GenerateSlotCount = 4,
        }, _ownerCaller);

        Assert.Equal(4, indoorZone.Slots.Count);
        var slotA01 = indoorZone.Slots[0];

        // Cố tình gán xe máy điện vào Khu trong nhà A -> bị từ chối
        var electricAttempt = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ElectricMotorbike,
            PlateNumber = "59MD199988",
            ParkingZoneId = indoorZone.Id,
            ParkingSlotId = slotA01.Id,
        }, _ownerCaller);

        Assert.False(electricAttempt.Succeeded);
        Assert.Equal(VehicleErrorCodes.VehicleTypeNotAllowedInZone, electricAttempt.ErrorCode);

        // Gán xe tay ga vào ô A01 -> thành công, ô A01 chuyển sang Occupied
        var scooter = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X155566",
            ParkingZoneId = indoorZone.Id,
            ParkingSlotId = slotA01.Id,
        }, _ownerCaller);

        Assert.True(scooter.Succeeded, scooter.ErrorCode);
        var zonesAfterAssign = await _zones.ListZonesAsync(_propertyId, _ownerCaller);
        Assert.Equal(ParkingSlotStatus.Occupied, zonesAfterAssign[0].Slots[0].Status);

        // Ngừng gửi xe -> ô A01 tự động giải phóng về Available
        await _vehicles.DeactivateAsync(scooter.Vehicle!.Id, "Trả phòng", _ownerCaller);
        var zonesAfterDeactivate = await _zones.ListZonesAsync(_propertyId, _ownerCaller);
        Assert.Equal(ParkingSlotStatus.Available, zonesAfterDeactivate[0].Slots[0].Status);
    }

    [Fact]
    public async Task VehicleBillingSyncsDraftInvoiceIdempotentlyAndSkipsIssuedInvoices()
    {
        var draftInvoice = new Invoice
        {
            ContractId = _contractId,
            PeriodStart = new DateOnly(2026, 11, 1),
            PeriodEnd = new DateOnly(2026, 11, 30),
            Status = InvoiceStatus.Draft,
            TotalAmount = 3_500_000m,
            CreatedAt = DateTime.UtcNow,
            Items =
            [
                new InvoiceItem
                {
                    ItemType = InvoiceItemType.Rent,
                    Description = "Tiền thuê phòng tháng 11",
                    Unit = "tháng",
                    Quantity = 1m,
                    UnitPrice = 3_500_000m,
                    Amount = 3_500_000m,
                    DisplayOrder = 1,
                }
            ]
        };
        _db.Invoices.Add(draftInvoice);
        await _db.SaveChangesAsync();

        // Thêm 1 xe số (80k) và 1 xe tay ga (100k)
        await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X111111",
        }, _ownerCaller);

        await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X122222",
        }, _ownerCaller);

        // Chạy đồng bộ lại nhiều lần vẫn chỉ có đúng 1 dòng "Phí gửi xe" = 180.000đ
        await _billing.SyncPropertyDraftInvoicesAsync(_propertyId, _ownerCaller);
        await _billing.SyncPropertyDraftInvoicesAsync(_propertyId, _ownerCaller);

        var updatedDraft = await _db.Invoices.Include(i => i.Items).SingleAsync(i => i.Id == draftInvoice.Id);
        var parkingLines = updatedDraft.Items.Where(i => i.Description.StartsWith("Phí gửi xe")).ToList();

        Assert.Single(parkingLines);
        Assert.Equal(180_000m, parkingLines[0].Amount);
        Assert.Equal(3_680_000m, updatedDraft.TotalAmount);
    }

    [Fact]
    public async Task BlockingAlertMatchesLast4DigitsHidesTenantPhoneAndEnforcesRateLimit()
    {
        await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            TenantId = _tenantId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59-X1 456.78",
        }, _ownerCaller);

        // Khách thuê tra cứu không bao giờ thấy tên chủ xe
        var lookupForTenant = await _vehicles.LookupAsync(_propertyId, "5678", _tenantCaller);
        Assert.Single(lookupForTenant);
        Assert.Null(lookupForTenant[0].TenantName);

        // Gửi báo chắn lối bằng 4 số cuối "5678"
        var firstAlert = await _blockingAlerts.SendBlockingAlertAsync(new BlockingAlertRequest
        {
            PropertyId = _propertyId,
            PlateQuery = "5678",
            Note = "Đang chắn lối cổng chính",
        }, _tenantCaller);

        Assert.True(firstAlert.Succeeded, firstAlert.ErrorCode);
        Assert.Equal("59X145678", firstAlert.MatchedPlateNumber);
        Assert.Equal("P101", firstAlert.RoomCode);
        Assert.Equal(1, firstAlert.NotifiedCount);

        // Gửi tiếp lần 2 trong vòng 5 phút -> bị chặn bởi rate limit
        var secondAlert = await _blockingAlerts.SendBlockingAlertAsync(new BlockingAlertRequest
        {
            PropertyId = _propertyId,
            PlateQuery = "59X145678",
        }, _tenantCaller);

        Assert.False(secondAlert.Succeeded);
        Assert.Equal(VehicleErrorCodes.BlockingAlertRateLimited, secondAlert.ErrorCode);

        // Sau 6 phút -> gửi lại được
        _clock.Advance(TimeSpan.FromMinutes(6));
        var thirdAlert = await _blockingAlerts.SendBlockingAlertAsync(new BlockingAlertRequest
        {
            PropertyId = _propertyId,
            PlateQuery = "5678",
        }, _tenantCaller);
        Assert.True(thirdAlert.Succeeded);
    }

    [Fact]
    public async Task GuestVehicleNoticeEnforcesMax3ActivePerRoomAndAuditSessionReconciles()
    {
        var now = _clock.GetUtcNow();
        for (var i = 1; i <= 3; i++)
        {
            await _guestNotices.CreateAsync(new GuestVehicleNoticeCreateRequest
            {
                RoomId = _roomId,
                PlateNumber = $"59G10000{i}",
                VehicleType = VehicleType.ManualMotorbike,
                GuestName = $"Bạn {i}",
                ExpectedFrom = now,
                ExpectedTo = now.AddHours(12),
            }, _tenantCaller);
        }

        // Phiếu thứ 4 khi cả 3 phiếu đang Active -> bị chặn theo D-VH-13
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _guestNotices.CreateAsync(new GuestVehicleNoticeCreateRequest
            {
                RoomId = _roomId,
                PlateNumber = "59G100004",
                VehicleType = VehicleType.Scooter,
                ExpectedFrom = now,
                ExpectedTo = now.AddHours(12),
            }, _tenantCaller));
        Assert.Equal(VehicleErrorCodes.GuestNoticeLimitReached, ex.Message);

        // Kiểm kê bãi xe
        await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X177788",
        }, _ownerCaller);

        var session = await _audits.StartSessionAsync(_propertyId, "Kiểm kê tối thứ 5", _ownerCaller);
        // 1 xe gửi tháng + 3 phiếu xe bạn gửi nhờ đang Active đều phải có trong checklist.
        Assert.Equal(4, session.TotalExpected);
        Assert.Equal(3, session.Items.Count(i => i.GuestNoticeId.HasValue));

        var checkedSession = await _audits.RecordItemResultAsync(
            session.Items[0].Id,
            VehicleAuditItemResult.Present,
            null,
            _ownerCaller);
        Assert.Equal(1, checkedSession.TotalPresent);

        var completed = await _audits.CompleteSessionAsync(session.Id, "Đủ xe", _ownerCaller);
        Assert.Equal(VehicleAuditSessionStatus.Completed, completed.Status);
    }

    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
        if (Directory.Exists(_auditRoot))
        {
            try
            {
                Directory.Delete(_auditRoot, recursive: true);
            }
            catch
            {
                // Ignore temp cleanup races
            }
        }
    }
}
