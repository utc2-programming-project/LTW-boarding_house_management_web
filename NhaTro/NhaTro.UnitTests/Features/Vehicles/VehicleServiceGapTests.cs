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
using Web.Features.Vehicles.Rules;
using Web.Features.Vehicles.Services;
using Web.Infrastructure.Auditing;
using Web.Infrastructure.Time;

namespace NhaTro.UnitTests.Features.Vehicles;

/// <summary>
/// Bù các lỗ hổng test của module xe: hợp đồng hết hạn/chưa bắt đầu, sync hóa đơn nháp nhiều kỳ,
/// phí = 0 còn dòng cũ, UpdateAsync giữ/xóa khu-ô, đổi loại xe trong khu cấm, gán ô khi kích hoạt lại,
/// quyền Quản lý theo cấu hình, BlockedOwnerIds, khách thuê nhiều nhà báo chắn lối,
/// thời gian phiếu khách + timezone thông báo, kiểm kê nhiều phiên và QuickAdd.
/// </summary>
public sealed class VehicleServiceGapTests : IDisposable
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

    public VehicleServiceGapTests()
    {
        _auditRoot = Path.Combine(Path.GetTempPath(), "nhatro-vehicle-gap-" + Guid.NewGuid().ToString("N"));
        _clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 25, 8, 0, 0, TimeSpan.Zero));

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("vehicle-gap-" + Guid.NewGuid())
            .Options;

        _db = new ApplicationDbContext(options);
        _audit = new AuditLogService(new AuditLogFileStore(_auditRoot, 7), _db);
        _notifications = new NotificationService(_db, _clock);
        _cache = new MemoryCache(new MemoryCacheOptions());

        _settings = new VehicleSettingService(_db, _audit, _clock);
        _zones = new ParkingZoneService(_db, _audit, _clock);
        _billing = new VehicleBillingService(_db, _audit, _clock);
        _vehicles = new VehicleService(_db, _billing, _notifications, _audit, _clock);
        _blockingAlerts = new BlockingAlertService(_db, _notifications, _cache, _audit, _clock);
        _guestNotices = new GuestVehicleNoticeService(_db, _notifications, _audit, _clock);
        _audits = new VehicleAuditService(_db, _vehicles, _audit, _clock);

        Seed();
    }

    private void Seed()
    {
        _db.Users.AddRange(
            new ApplicationUser { Id = "owner-1", UserName = "owner@test.local", Email = "owner@test.local", FullName = "Chủ trọ A", ApprovedAt = DateTime.UtcNow },
            new ApplicationUser { Id = "owner-2", UserName = "owner2@test.local", Email = "owner2@test.local", FullName = "Chủ trọ B", ApprovedAt = DateTime.UtcNow },
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

        _contractId = AddContract(_roomId, new DateTime(2026, 1, 1), new DateTime(2027, 1, 1), "HD-P101");
        _db.ContractMembers.Add(new ContractMember
        {
            ContractId = _contractId,
            TenantId = _tenantId,
            IsRepresentative = true,
        });
        _db.SaveChanges();
    }

    private Guid AddContract(int roomId, DateTime start, DateTime end, string number)
    {
        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = number,
            RoomId = roomId,
            StartDate = start,
            EndDate = end,
            SnapshotPrice = 3_500_000m,
            DepositAmount = 3_500_000m,
            Status = ContractStatus.Approved,
        };
        _db.Contracts.Add(contract);
        _db.SaveChanges();
        return contract.ContractId;
    }

    private (int PropertyId, int RoomId) AddPropertyWithRoom(string ownerId, string propertyName, string roomCode)
    {
        var property = new Property
        {
            Name = propertyName,
            Address = "1 Nguyễn Trãi",
            OwnerId = ownerId,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Properties.Add(property);
        _db.SaveChanges();

        var room = new Room
        {
            PropertyId = property.Id,
            Code = roomCode,
            StandardPrice = 3_000_000m,
            Status = RoomStatus.Rented,
        };
        _db.Rooms.Add(room);
        _db.SaveChanges();
        return (property.Id, room.Id);
    }

    private Invoice AddDraftInvoice(Guid contractId, DateOnly start, DateOnly end, params (string Description, decimal Amount)[] items)
    {
        var order = 1;
        var invoice = new Invoice
        {
            ContractId = contractId,
            PeriodStart = start,
            PeriodEnd = end,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            Items = items.Select(item => new InvoiceItem
            {
                ItemType = item.Description.StartsWith(VehicleRules.ParkingInvoiceLinePrefix)
                    ? InvoiceItemType.Service
                    : InvoiceItemType.Rent,
                Description = item.Description,
                Unit = "tháng",
                Quantity = 1m,
                UnitPrice = item.Amount,
                Amount = item.Amount,
                DisplayOrder = order++,
            }).ToList(),
        };
        invoice.TotalAmount = invoice.Items.Sum(i => i.Amount);
        _db.Invoices.Add(invoice);
        return invoice;
    }

    [Fact]
    public async Task CreateRejectsExpiredAndNotYetStartedContracts()
    {
        var (expiredPropertyId, expiredRoomId) = AddPropertyWithRoom("owner-1", "Nhà trọ hết hạn", "E101");
        AddContract(expiredRoomId, new DateTime(2025, 1, 1), new DateTime(2026, 11, 24), "HD-E101");

        var expired = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = expiredPropertyId,
            RoomId = expiredRoomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X199001",
        }, _ownerCaller);
        Assert.False(expired.Succeeded);
        Assert.Equal(VehicleErrorCodes.NoActiveContract, expired.ErrorCode);

        var (upcomingPropertyId, upcomingRoomId) = AddPropertyWithRoom("owner-1", "Nhà trọ chưa bắt đầu", "U101");
        AddContract(upcomingRoomId, new DateTime(2026, 12, 1), new DateTime(2027, 6, 1), "HD-U101");

        var upcoming = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = upcomingPropertyId,
            RoomId = upcomingRoomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X199002",
        }, _ownerCaller);
        Assert.False(upcoming.Succeeded);
        Assert.Equal(VehicleErrorCodes.NoActiveContract, upcoming.ErrorCode);
    }

    [Fact]
    public async Task BillingSyncsEachDraftPeriodByVehicleTimeWindow()
    {
        var november = AddDraftInvoice(_contractId, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
            ("Tiền thuê phòng tháng 11", 3_500_000m));
        var december = AddDraftInvoice(_contractId, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31),
            ("Tiền thuê phòng tháng 12", 3_500_000m));
        await _db.SaveChangesAsync();

        var created = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X130001",
        }, _ownerCaller);
        Assert.True(created.Succeeded, created.ErrorCode);

        // Xe ngừng gửi ngay trong tháng 11: tháng 11 vẫn tính phí, tháng 12 không còn dòng phí.
        await _vehicles.DeactivateAsync(created.Vehicle!.Id, "Trả xe sớm", _ownerCaller);
        await _billing.SyncDraftInvoicesForContractAsync(_contractId);

        var nov = await _db.Invoices.Include(i => i.Items).SingleAsync(i => i.Id == november.Id);
        var dec = await _db.Invoices.Include(i => i.Items).SingleAsync(i => i.Id == december.Id);

        var novParking = Assert.Single(nov.Items, i => VehicleRules.IsParkingFeeLine(i.Description));
        Assert.Equal(80_000m, novParking.Amount);
        Assert.Equal(3_580_000m, nov.TotalAmount);

        Assert.DoesNotContain(dec.Items, i => VehicleRules.IsParkingFeeLine(i.Description));
        Assert.Equal(3_500_000m, dec.TotalAmount);
    }

    [Fact]
    public async Task BillingZeroesOrRemovesLegacyParkingLineWhenNoVehicles()
    {
        // Hóa đơn chỉ có dòng phí gửi xe cũ: đưa về 0 thay vì giữ số tiền cũ (mục 18).
        var onlyParking = AddDraftInvoice(_contractId, new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
            ("Phí gửi xe tháng 11", 100_000m));
        // Hóa đơn còn dòng khác: xóa hẳn dòng phí gửi xe cũ.
        var mixed = AddDraftInvoice(_contractId, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31),
            ("Tiền thuê phòng tháng 12", 3_500_000m),
            ("Phí gửi xe tháng 12", 100_000m));
        await _db.SaveChangesAsync();

        var updated = await _billing.SyncDraftInvoicesForContractAsync(_contractId);
        Assert.Equal(2, updated);

        var onlyParkingAfter = await _db.Invoices.Include(i => i.Items).SingleAsync(i => i.Id == onlyParking.Id);
        var onlyLine = Assert.Single(onlyParkingAfter.Items);
        Assert.Equal(0m, onlyLine.Amount);
        Assert.Equal(0m, onlyLine.UnitPrice);
        Assert.Equal(0m, onlyParkingAfter.TotalAmount);

        var mixedAfter = await _db.Invoices.Include(i => i.Items).SingleAsync(i => i.Id == mixed.Id);
        Assert.DoesNotContain(mixedAfter.Items, i => VehicleRules.IsParkingFeeLine(i.Description));
        Assert.Equal(3_500_000m, mixedAfter.TotalAmount);
    }

    [Fact]
    public async Task UpdateKeepsParkingAssignmentWhenOmittedAndClearsWhenExplicit()
    {
        await _settings.SaveAsync(new VehiclePropertySettingSaveRequest
        {
            PropertyId = _propertyId,
            EnableParkingZones = true,
            MaxVehiclesPerRoom = 5,
        }, _ownerCaller);

        var zone = await _zones.SaveZoneAsync(new ParkingZoneSaveRequest
        {
            PropertyId = _propertyId,
            Name = "Khu A",
            Capacity = 4,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.ManualMotorbike | AllowedVehicleTypeFlags.Scooter,
            HasSlots = true,
            SlotPrefix = "A",
            GenerateSlotCount = 4,
        }, _ownerCaller);

        var created = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X140001",
            ParkingZoneId = zone.Id,
            ParkingSlotId = zone.Slots[0].Id,
        }, _ownerCaller);
        Assert.True(created.Succeeded, created.ErrorCode);

        // Không gửi khu/ô trong request cập nhật → giữ nguyên gán cũ, không xoá nhầm.
        var kept = await _vehicles.UpdateAsync(new VehicleUpdateRequest
        {
            Id = created.Vehicle!.Id,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X140001",
            Color = "Trắng",
        }, _ownerCaller);
        Assert.True(kept.Succeeded, kept.ErrorCode);
        Assert.Equal(zone.Id, kept.Vehicle!.ParkingZoneId);
        Assert.Equal(zone.Slots[0].Id, kept.Vehicle.ParkingSlotId);

        var zonesAfterKeep = await _zones.ListZonesAsync(_propertyId, _ownerCaller);
        Assert.Equal(ParkingSlotStatus.Occupied, zonesAfterKeep[0].Slots[0].Status);

        // ClearParkingAssignment = true → gỡ cả khu và ô, ô về Available.
        var cleared = await _vehicles.UpdateAsync(new VehicleUpdateRequest
        {
            Id = created.Vehicle.Id,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X140001",
            ClearParkingAssignment = true,
        }, _ownerCaller);
        Assert.True(cleared.Succeeded, cleared.ErrorCode);
        Assert.Null(cleared.Vehicle!.ParkingZoneId);
        Assert.Null(cleared.Vehicle.ParkingSlotId);

        var zonesAfterClear = await _zones.ListZonesAsync(_propertyId, _ownerCaller);
        Assert.Equal(ParkingSlotStatus.Available, zonesAfterClear[0].Slots[0].Status);
    }

    [Fact]
    public async Task UpdateRejectsVehicleTypeChangeNotAllowedByCurrentZone()
    {
        await _settings.SaveAsync(new VehiclePropertySettingSaveRequest
        {
            PropertyId = _propertyId,
            EnableParkingZones = true,
            MaxVehiclesPerRoom = 5,
        }, _ownerCaller);

        var indoor = await _zones.SaveZoneAsync(new ParkingZoneSaveRequest
        {
            PropertyId = _propertyId,
            Name = "Khu trong nhà",
            IsIndoor = true,
            Capacity = 4,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.ManualMotorbike | AllowedVehicleTypeFlags.Scooter,
            HasSlots = true,
            SlotPrefix = "I",
            GenerateSlotCount = 4,
        }, _ownerCaller);

        var created = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X150001",
            ParkingZoneId = indoor.Id,
            ParkingSlotId = indoor.Slots[0].Id,
        }, _ownerCaller);
        Assert.True(created.Succeeded, created.ErrorCode);

        // Đổi sang xe máy điện trong khu cấm xe điện → phải bị chặn dù không đổi khu/ô.
        var changed = await _vehicles.UpdateAsync(new VehicleUpdateRequest
        {
            Id = created.Vehicle!.Id,
            VehicleType = VehicleType.ElectricMotorbike,
            PlateNumber = "59X150001",
        }, _ownerCaller);

        Assert.False(changed.Succeeded);
        Assert.Equal(VehicleErrorCodes.VehicleTypeNotAllowedInZone, changed.ErrorCode);
    }

    [Fact]
    public async Task ReactivateAssignsSlotAndMarksItOccupied()
    {
        await _settings.SaveAsync(new VehiclePropertySettingSaveRequest
        {
            PropertyId = _propertyId,
            EnableParkingZones = true,
            MaxVehiclesPerRoom = 5,
        }, _ownerCaller);

        var zone = await _zones.SaveZoneAsync(new ParkingZoneSaveRequest
        {
            PropertyId = _propertyId,
            Name = "Khu B",
            Capacity = 4,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.Scooter,
            HasSlots = true,
            SlotPrefix = "B",
            GenerateSlotCount = 4,
        }, _ownerCaller);

        var created = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X160001",
        }, _ownerCaller);
        Assert.True(created.Succeeded, created.ErrorCode);

        await _vehicles.DeactivateAsync(created.Vehicle!.Id, "Ngừng tạm", _ownerCaller);

        // Kích hoạt lại xe đang Inactive và gán ô đỗ → ô phải chuyển Occupied đúng xe.
        var reactivated = await _vehicles.ReactivateAsync(
            created.Vehicle.Id, zone.Id, zone.Slots[1].Id, _ownerCaller);
        Assert.True(reactivated.Succeeded, reactivated.ErrorCode);
        Assert.Equal(VehicleStatus.Active, reactivated.Vehicle!.Status);
        Assert.Equal(zone.Slots[1].Id, reactivated.Vehicle.ParkingSlotId);

        var zonesAfter = await _zones.ListZonesAsync(_propertyId, _ownerCaller);
        var slot = zonesAfter[0].Slots.Single(s => s.Id == zone.Slots[1].Id);
        Assert.Equal(ParkingSlotStatus.Occupied, slot.Status);
        Assert.Equal(created.Vehicle.Id, slot.CurrentVehicleId);
    }

    [Fact]
    public async Task ManagerLosesVehicleManagementWhenPropertyDisallows()
    {
        var managed = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X170001",
        }, _managerCaller);
        Assert.True(managed.Succeeded, managed.ErrorCode);

        await _settings.SaveAsync(new VehiclePropertySettingSaveRequest
        {
            PropertyId = _propertyId,
            AllowManagerManageVehicles = false,
        }, _ownerCaller);

        var blocked = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X170002",
        }, _managerCaller);
        Assert.False(blocked.Succeeded);
        Assert.Equal(VehicleErrorCodes.Forbidden, blocked.ErrorCode);

        // Chủ trọ vẫn quản lý bình thường.
        var ownerStill = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X170003",
        }, _ownerCaller);
        Assert.True(ownerStill.Succeeded, ownerStill.ErrorCode);
    }

    [Fact]
    public async Task ManagerWithBlockedOwnerCannotListVehiclesOfThatOwner()
    {
        await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X180001",
        }, _ownerCaller);

        var normal = await _vehicles.ListAsync(new VehicleFilter(), _managerCaller);
        Assert.Single(normal.Items);

        var blockedCaller = _managerCaller with { BlockedOwnerIds = ["owner-1"] };
        var blocked = await _vehicles.ListAsync(new VehicleFilter(), blockedCaller);
        Assert.Empty(blocked.Items);
        Assert.Equal(0, blocked.Summary.TotalActiveCount);
    }

    [Fact]
    public async Task TenantWithTwoPropertiesCanReportBlockingAlertInBothButNotThird()
    {
        var (secondPropertyId, secondRoomId) = AddPropertyWithRoom("owner-1", "Nhà trọ thứ hai", "P201");
        var secondContractId = AddContract(secondRoomId, new DateTime(2026, 1, 1), new DateTime(2027, 1, 1), "HD-P201");
        _db.ContractMembers.Add(new ContractMember
        {
            ContractId = secondContractId,
            TenantId = _tenantId,
            IsRepresentative = true,
        });
        await _db.SaveChangesAsync();

        var firstVehicle = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            TenantId = _tenantId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X190001",
        }, _ownerCaller);
        Assert.True(firstVehicle.Succeeded, "vehicle1: " + firstVehicle.ErrorCode);

        var secondVehicle = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = secondPropertyId,
            RoomId = secondRoomId,
            TenantId = _tenantId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X190002",
        }, _ownerCaller);
        Assert.True(secondVehicle.Succeeded, "vehicle2: " + secondVehicle.ErrorCode);

        var firstAlert = await _blockingAlerts.SendBlockingAlertAsync(new BlockingAlertRequest
        {
            PropertyId = _propertyId,
            PlateQuery = "0001",
        }, _tenantCaller);
        Assert.True(firstAlert.Succeeded, firstAlert.ErrorCode);

        var secondAlert = await _blockingAlerts.SendBlockingAlertAsync(new BlockingAlertRequest
        {
            PropertyId = secondPropertyId,
            PlateQuery = "0002",
        }, _tenantCaller);
        Assert.True(secondAlert.Succeeded, secondAlert.ErrorCode);

        var (thirdPropertyId, _) = AddPropertyWithRoom("owner-2", "Nhà trọ không thuê", "P301");
        var forbidden = await _blockingAlerts.SendBlockingAlertAsync(new BlockingAlertRequest
        {
            PropertyId = thirdPropertyId,
            PlateQuery = "0001",
        }, _tenantCaller);
        Assert.False(forbidden.Succeeded);
        Assert.Equal(VehicleErrorCodes.Forbidden, forbidden.ErrorCode);
    }

    [Fact]
    public async Task GuestNoticeValidatesTimeRangeAndNotifiesVietnamLocalTime()
    {
        var now = _clock.GetUtcNow();

        var past = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _guestNotices.CreateAsync(new GuestVehicleNoticeCreateRequest
            {
                RoomId = _roomId,
                PlateNumber = "59G200001",
                VehicleType = VehicleType.ManualMotorbike,
                ExpectedFrom = now.AddHours(-2),
                ExpectedTo = now.AddHours(2),
            }, _tenantCaller));
        Assert.Equal(VehicleErrorCodes.InvalidTimeRange, past.Message);

        var tooLong = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _guestNotices.CreateAsync(new GuestVehicleNoticeCreateRequest
            {
                RoomId = _roomId,
                PlateNumber = "59G200002",
                VehicleType = VehicleType.ManualMotorbike,
                ExpectedFrom = now,
                ExpectedTo = now.AddDays(8),
            }, _tenantCaller));
        Assert.Equal(VehicleErrorCodes.InvalidTimeRange, tooLong.Message);

        var expectedFrom = now.AddHours(4);
        var notice = await _guestNotices.CreateAsync(new GuestVehicleNoticeCreateRequest
        {
            RoomId = _roomId,
            PlateNumber = "59G200003",
            VehicleType = VehicleType.ManualMotorbike,
            GuestName = "Bạn Hà",
            ExpectedFrom = expectedFrom,
            ExpectedTo = expectedFrom.AddHours(6),
        }, _tenantCaller);

        Assert.Equal(GuestVehicleNoticeStatus.Active, notice.Status);
        Assert.Equal(expectedFrom, notice.ExpectedFrom);

        // Thông báo cho chủ trọ hiển thị giờ Việt Nam (UTC+7), không phải giờ UTC thô.
        var notification = await _db.AppNotifications
            .SingleAsync(n => n.RecipientUserId == "owner-1");
        var expectedLocal = VietnamTime.ToVietnam(expectedFrom.UtcDateTime).ToString("dd/MM/yyyy HH:mm");
        Assert.Contains(expectedLocal, notification.Body);
    }

    [Fact]
    public async Task AuditSessionsListedInBatchWithCorrectCounts()
    {
        var first = await _audits.StartSessionAsync(_propertyId, "Kiểm kê sáng", _ownerCaller);
        Assert.Equal(0, first.TotalExpected);
        await _audits.CompleteSessionAsync(first.Id, "Bãi trống", _ownerCaller);

        await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X210001",
        }, _ownerCaller);

        var second = await _audits.StartSessionAsync(_propertyId, "Kiểm kê chiều", _ownerCaller);
        Assert.Equal(1, second.TotalExpected);

        var sessions = await _audits.ListSessionsAsync(_propertyId, _ownerCaller);
        Assert.Equal(2, sessions.Count);
        Assert.All(sessions, s =>
        {
            Assert.Equal("Nhà trọ Ánh Dương", s.PropertyName);
            Assert.Equal("Chủ trọ A", s.AuditedByName);
        });
        Assert.Equal(1, sessions.Single(s => s.Id == second.Id).TotalExpected);
        Assert.Equal(VehicleAuditSessionStatus.Completed, sessions.Single(s => s.Id == first.Id).Status);
    }

    [Fact]
    public async Task QuickAddDuringAuditAppendsPresentItem()
    {
        await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.ManualMotorbike,
            PlateNumber = "59X220001",
        }, _ownerCaller);

        var session = await _audits.StartSessionAsync(_propertyId, "Kiểm kê nhanh", _ownerCaller);
        Assert.Equal(1, session.TotalExpected);

        var updated = await _audits.QuickAddVehicleAsync(session.Id, new VehicleCreateRequest
        {
            RoomId = _roomId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X220002",
        }, _ownerCaller);

        Assert.Equal(2, updated.TotalExpected);
        Assert.Equal(1, updated.TotalPresent);
        var added = Assert.Single(updated.Items, i => i.PlateNumberSnapshot == "59X220002");
        Assert.Equal(VehicleAuditItemResult.Present, added.Result);
        Assert.NotNull(added.VehicleId);
    }

    [Fact]
    public async Task ZoneSlotPrefixIsPersistedWhenZoneHasNoSlotsYet()
    {
        await _settings.SaveAsync(new VehiclePropertySettingSaveRequest
        {
            PropertyId = _propertyId,
            EnableParkingZones = true,
            MaxVehiclesPerRoom = 5,
        }, _ownerCaller);

        var zone = await _zones.SaveZoneAsync(new ParkingZoneSaveRequest
        {
            PropertyId = _propertyId,
            Name = "Khu C",
            Capacity = 6,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.Scooter,
            HasSlots = false,
        }, _ownerCaller);
        Assert.Null(zone.SlotPrefix);
        Assert.Empty(zone.Slots);

        // Bật ô đỗ sau đó: tiền tố phải được lưu lại để lần sửa khu kế tiếp không reset về "A".
        var slots = await _zones.GenerateSlotsAsync(zone.Id, "C", 2, _ownerCaller);
        Assert.Equal(new[] { "C01", "C02" }, slots.Select(s => s.Code).OrderBy(c => c).ToArray());

        var zones = await _zones.ListZonesAsync(_propertyId, _ownerCaller);
        Assert.Equal("C", Assert.Single(zones, z => z.Id == zone.Id).SlotPrefix);

        // Sửa khu mà không gửi lại tiền tố → giữ nguyên "C" đã lưu.
        var updated = await _zones.SaveZoneAsync(new ParkingZoneSaveRequest
        {
            Id = zone.Id,
            PropertyId = _propertyId,
            Name = "Khu C",
            Capacity = 6,
            AllowedVehicleTypes = AllowedVehicleTypeFlags.Scooter,
            HasSlots = true,
            GenerateSlotCount = 2,
        }, _ownerCaller);

        Assert.Equal("C", updated.SlotPrefix);
        Assert.All(updated.Slots, s => Assert.StartsWith("C", s.Code));
    }

    [Fact]
    public async Task ReportIncidentLinksRegisteredVehicleAndRejectsForeignRoom()
    {
        var created = await _vehicles.CreateAsync(new VehicleCreateRequest
        {
            PropertyId = _propertyId,
            RoomId = _roomId,
            VehicleType = VehicleType.Scooter,
            PlateNumber = "59X230001",
        }, _ownerCaller);
        Assert.True(created.Succeeded, created.ErrorCode);

        var otherRoom = new Room
        {
            PropertyId = _propertyId,
            Code = "Z999",
            Status = RoomStatus.Available,
            CreatedAt = DateTime.UtcNow,
        };
        _db.Rooms.Add(otherRoom);
        await _db.SaveChangesAsync();

        var incidentId = await _vehicles.ReportVehicleIncidentAsync(new VehicleIncidentCreateRequest
        {
            RoomId = _roomId,
            VehicleId = created.Vehicle!.Id,
            PlateNumber = "",
            Title = "Xe bị xước",
        }, _ownerCaller);

        var incident = await _db.MaintenanceIncidents.AsNoTracking().SingleAsync(i => i.Id == incidentId);
        Assert.Equal(created.Vehicle.Id, incident.VehicleId);
        Assert.Equal(VehicleRules.NormalizePlateNumber("59X230001"), incident.VehiclePlateNumber);

        // Gắn xe của phòng khác vào sự cố phải bị chặn (tránh ghép nhầm/lộ dữ liệu).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _vehicles.ReportVehicleIncidentAsync(new VehicleIncidentCreateRequest
            {
                RoomId = otherRoom.Id,
                VehicleId = created.Vehicle.Id,
                PlateNumber = "59X230001",
                Title = "Sai phòng",
            }, _ownerCaller));
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