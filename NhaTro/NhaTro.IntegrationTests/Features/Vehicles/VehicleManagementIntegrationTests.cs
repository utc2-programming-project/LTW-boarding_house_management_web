using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Audit.Services;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
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

namespace NhaTro.IntegrationTests.Features.Vehicles;

public sealed class VehicleManagementIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task FilteredUniqueIndexAllowsMultipleInactiveHistoryButBlocksDuplicateActivePlate()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();

        var owner = new ApplicationUser
        {
            Id = "owner-vh-1",
            UserName = "ownervh@test.local",
            Email = "ownervh@test.local",
            FullName = "Chủ trọ Xe",
            ApprovedAt = DateTime.UtcNow,
        };
        db.Users.Add(owner);

        var property = new Property
        {
            Name = "Nhà trọ Kiểm thử Xe",
            Address = "100 Võ Văn Ngân",
            OwnerId = owner.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var room = new Room
        {
            PropertyId = property.Id,
            Code = "P201",
            StandardPrice = 3_000_000m,
            Status = RoomStatus.Rented,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var tenant = new Tenant
        {
            FullName = "Trần Văn Thuê",
            Phone = "0912345678",
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = "HD-P201",
            RoomId = room.Id,
            StartDate = DateTime.UtcNow.Date.AddMonths(-1),
            EndDate = DateTime.UtcNow.Date.AddMonths(11),
            SnapshotPrice = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = tenant.Id,
            IsRepresentative = true,
        });
        await db.SaveChangesAsync();

        var auditRoot = Path.Combine(Path.GetTempPath(), "nhatro-vh-it-" + Guid.NewGuid().ToString("N"));
        try
        {
            var clock = TimeProvider.System;
            var audit = new AuditLogService(new AuditLogFileStore(auditRoot, 7), db);
            var notifications = new NotificationService(db, clock);
            var billing = new VehicleBillingService(db, audit, clock);
            var service = new VehicleService(db, billing, notifications, audit, clock);
            var caller = new PropertyCaller(owner.Id, Roles.ChuTro);

            var c1 = await service.CreateAsync(new VehicleCreateRequest
            {
                PropertyId = property.Id,
                RoomId = room.Id,
                VehicleType = VehicleType.ManualMotorbike,
                PlateNumber = "59-X1 888.88",
            }, caller);
            Assert.True(c1.Succeeded, c1.ErrorCode);

            await service.DeactivateAsync(c1.Vehicle!.Id, "Ngừng lần 1", caller);

            var c2 = await service.CreateAsync(new VehicleCreateRequest
            {
                PropertyId = property.Id,
                RoomId = room.Id,
                VehicleType = VehicleType.ManualMotorbike,
                PlateNumber = "59X188888",
            }, caller);
            Assert.True(c2.Succeeded, c2.ErrorCode);

            await service.DeactivateAsync(c2.Vehicle!.Id, "Ngừng lần 2", caller);

            var c3 = await service.CreateAsync(new VehicleCreateRequest
            {
                PropertyId = property.Id,
                RoomId = room.Id,
                VehicleType = VehicleType.Scooter,
                PlateNumber = "59-X1 888.88",
            }, caller);
            Assert.True(c3.Succeeded, c3.ErrorCode);

            var allRecords = await db.Vehicles
                .Where(v => v.PropertyId == property.Id && v.PlateNumber == "59X188888")
                .ToListAsync();
            Assert.Equal(3, allRecords.Count);
            Assert.Equal(2, allRecords.Count(v => v.Status == VehicleStatus.Inactive));
            Assert.Equal(1, allRecords.Count(v => v.Status == VehicleStatus.Active));
        }
        finally
        {
            if (Directory.Exists(auditRoot))
            {
                Directory.Delete(auditRoot, recursive: true);
            }
        }
    }

    /// <summary>Mục 53/55: lọc danh sách và khớp biển số phải dịch được sang SQL thật (ToLower/Contains/EndsWith).</summary>
    [SqlServerFact]
    public async Task ListAndBlockingAlertFiltersExecuteOnSqlServer()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var (owner, property, room, _, _) = await SeedPropertyAsync(db, "filter");

        var auditRoot = Path.Combine(Path.GetTempPath(), "nhatro-vh-it-" + Guid.NewGuid().ToString("N"));
        try
        {
            var clock = TimeProvider.System;
            var audit = new AuditLogService(new AuditLogFileStore(auditRoot, 7), db);
            var notifications = new NotificationService(db, clock);
            var billing = new VehicleBillingService(db, audit, clock);
            var service = new VehicleService(db, billing, notifications, audit, clock);
            using var cache = new MemoryCache(new MemoryCacheOptions());
            var blocking = new BlockingAlertService(db, notifications, cache, audit, clock);
            var caller = new PropertyCaller(owner.Id, Roles.ChuTro);

            db.VehiclePropertySettings.Add(new Web.Features.Vehicles.Entities.VehiclePropertySetting
            {
                PropertyId = property.Id,
                MaxVehiclesPerRoom = 5,
            });
            await db.SaveChangesAsync();

            foreach (var plate in new[] { "59-X1 450.01", "59X145002", "59X135002" })
            {
                var created = await service.CreateAsync(new VehicleCreateRequest
                {
                    PropertyId = property.Id,
                    RoomId = room.Id,
                    VehicleType = VehicleType.Scooter,
                    PlateNumber = plate,
                }, caller);
                Assert.True(created.Succeeded, created.ErrorCode);
            }

            var byKeyword = await service.ListAsync(new VehicleFilter { Keyword = "5001" }, caller);
            var single = Assert.Single(byKeyword.Items);
            Assert.Equal("59X145001", single.PlateNumber);

            var byRoom = await service.ListAsync(new VehicleFilter { Keyword = room.Code }, caller);
            Assert.Equal(3, byRoom.Items.Count);

            // Hai xe cùng 4 số cuối 5002 -> báo chắn lối phải trả lỗi mơ hồ thay vì báo sai xe.
            var ambiguous = await blocking.SendBlockingAlertAsync(new BlockingAlertRequest
            {
                PropertyId = property.Id,
                PlateQuery = "5002",
            }, caller);
            Assert.False(ambiguous.Succeeded);
            Assert.Equal(VehicleErrorCodes.BlockingAlertAmbiguousPlate, ambiguous.ErrorCode);

            var tooShort = await blocking.SendBlockingAlertAsync(new BlockingAlertRequest
            {
                PropertyId = property.Id,
                PlateQuery = "500",
            }, caller);
            Assert.False(tooShort.Succeeded);
            Assert.Equal(VehicleErrorCodes.InvalidPlateNumber, tooShort.ErrorCode);
        }
        finally
        {
            if (Directory.Exists(auditRoot))
            {
                Directory.Delete(auditRoot, recursive: true);
            }
        }
    }

    /// <summary>Mục 57: hợp đồng bị xóa mềm không được làm xe biến mất khỏi danh sách.</summary>
    [SqlServerFact]
    public async Task SoftDeletedContractKeepsVehicleVisible()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var (owner, property, room, _, contract) = await SeedPropertyAsync(db, "softdel");

        var auditRoot = Path.Combine(Path.GetTempPath(), "nhatro-vh-it-" + Guid.NewGuid().ToString("N"));
        try
        {
            var clock = TimeProvider.System;
            var audit = new AuditLogService(new AuditLogFileStore(auditRoot, 7), db);
            var notifications = new NotificationService(db, clock);
            var billing = new VehicleBillingService(db, audit, clock);
            var service = new VehicleService(db, billing, notifications, audit, clock);
            var caller = new PropertyCaller(owner.Id, Roles.ChuTro);

            var created = await service.CreateAsync(new VehicleCreateRequest
            {
                PropertyId = property.Id,
                RoomId = room.Id,
                VehicleType = VehicleType.ManualMotorbike,
                PlateNumber = "59X188777",
            }, caller);
            Assert.True(created.Succeeded, created.ErrorCode);

            contract.IsDeleted = true;
            contract.DeletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            var list = await service.ListAsync(new VehicleFilter(), caller);
            var vehicle = Assert.Single(list.Items);
            Assert.Equal("59X188777", vehicle.PlateNumber);
            Assert.Equal("HD-softdel", vehicle.ContractNumber);
        }
        finally
        {
            if (Directory.Exists(auditRoot))
            {
                Directory.Delete(auditRoot, recursive: true);
            }
        }
    }

    /// <summary>QuickAdd: khi tạo xe thất bại thì không ghi dòng kiểm kê nào và không để lại xe rác.</summary>
    [SqlServerFact]
    public async Task QuickAddDuringAuditDoesNotLeavePartialWrites()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        var (owner, property, _, _, _) = await SeedPropertyAsync(db, "quickadd");

        var auditRoot = Path.Combine(Path.GetTempPath(), "nhatro-vh-it-" + Guid.NewGuid().ToString("N"));
        try
        {
            var clock = TimeProvider.System;
            var audit = new AuditLogService(new AuditLogFileStore(auditRoot, 7), db);
            var notifications = new NotificationService(db, clock);
            var billing = new VehicleBillingService(db, audit, clock);
            var service = new VehicleService(db, billing, notifications, audit, clock);
            var audits = new VehicleAuditService(db, service, audit, clock);
            var caller = new PropertyCaller(owner.Id, Roles.ChuTro);

            var session = await audits.StartSessionAsync(property.Id, "Kiểm kê", caller);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                audits.QuickAddVehicleAsync(session.Id, new VehicleCreateRequest
                {
                    RoomId = 999_999,
                    VehicleType = VehicleType.Scooter,
                    PlateNumber = "59X177777",
                }, caller));
            Assert.Equal(VehicleErrorCodes.RoomNotFound, error.Message);

            Assert.Equal(0, await db.Vehicles.CountAsync());
            var sessionInDb = await db.VehicleAuditSessions.Include(s => s.Items).SingleAsync(s => s.Id == session.Id);
            Assert.Empty(sessionInDb.Items);
            Assert.Equal(0, sessionInDb.TotalExpected);
        }
        finally
        {
            if (Directory.Exists(auditRoot))
            {
                Directory.Delete(auditRoot, recursive: true);
            }
        }
    }

    private static async Task<(ApplicationUser Owner, Property Property, Room Room, Tenant Tenant, Contract Contract)>
        SeedPropertyAsync(ApplicationDbContext db, string suffix)
    {
        var owner = new ApplicationUser
        {
            Id = $"owner-vh-{suffix}",
            UserName = $"ownervh-{suffix}@test.local",
            Email = $"ownervh-{suffix}@test.local",
            FullName = "Chủ trọ Xe",
            ApprovedAt = DateTime.UtcNow,
        };
        db.Users.Add(owner);

        var property = new Property
        {
            Name = $"Nhà trọ Kiểm thử {suffix}",
            Address = "100 Võ Văn Ngân",
            OwnerId = owner.Id,
            CreatedAt = DateTime.UtcNow,
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var room = new Room
        {
            PropertyId = property.Id,
            Code = "P201",
            StandardPrice = 3_000_000m,
            Status = RoomStatus.Rented,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var tenant = new Tenant
        {
            FullName = "Trần Văn Thuê",
            Phone = "0912345678",
            CreatedAt = DateTime.UtcNow,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = $"HD-{suffix}",
            RoomId = room.Id,
            StartDate = DateTime.UtcNow.Date.AddMonths(-1),
            EndDate = DateTime.UtcNow.Date.AddMonths(11),
            SnapshotPrice = 3_000_000m,
            DepositAmount = 3_000_000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = tenant.Id,
            IsRepresentative = true,
        });
        await db.SaveChangesAsync();

        return (owner, property, room, tenant, contract);
    }
}
