using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Settings;
using Web.Features.Settings.Entities;
using Web.Features.Settings.Enums;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;

namespace Web.Data.Seeding;

/// <summary>
/// VHT-49 (3A) — dữ liệu demo nền cho 2 nhà trọ, 4 vai trò và 2 kỳ đối chiếu.
/// Chỉ chạy qua tham số <c>--seed-demo</c> hoặc test; không tự chạy khi app khởi động.
/// Mọi bản ghi tìm theo khóa tự nhiên để chạy lại không nhân đôi dòng.
/// </summary>
public static class DemoDataSeeder
{
    public const string DefaultPassword = "Demo@123456";

    public const string AdminEmail = "admin@demo.local";
    public const string OwnerAEmail = "chutro.a@demo.local";
    public const string OwnerBEmail = "chutro.b@demo.local";
    public const string ManagerAEmail = "quanly.a@demo.local";
    public const string TenantAEmail = "khachthue.a@demo.local";
    public const string TenantBEmail = "khachthue.b@demo.local";

    /// <summary>Danh sách tài khoản demo dùng cho seeder và bảng đăng nhập nhanh.</summary>
    public static readonly (string Email, string Role)[] Accounts =
    [
        (AdminEmail, Roles.Admin),
        (OwnerAEmail, Roles.ChuTro),
        (OwnerBEmail, Roles.ChuTro),
        (ManagerAEmail, Roles.QuanLy),
        (TenantAEmail, Roles.KhachThue),
        (TenantBEmail, Roles.KhachThue),
    ];

    private static readonly IReadOnlyDictionary<string, string> DisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AdminEmail] = "Quản trị Demo",
            [OwnerAEmail] = "Chủ trọ Demo A",
            [OwnerBEmail] = "Chủ trọ Demo B",
            [ManagerAEmail] = "Quản lý Demo A",
            [TenantAEmail] = "Khách thuê Demo A",
            [TenantBEmail] = "Khách thuê Demo B",
        };

    public const string PropertyAName = "Nhà trọ Demo 3A - Bình Thạnh";
    public const string PropertyBName = "Nhà trọ Demo 3A - Thủ Đức";
    public const string RoomA101Code = "A101";
    public const string RoomA102Code = "A102";
    public const string RoomA103Code = "A103";
    public const string RoomB201Code = "B201";
    public const string RoomB202Code = "B202";

    public const string ContractANumber = "DEMO-A101-2026";
    public const string ContractBNumber = "DEMO-B201-2026";

    public const string ElectricScope = "VN-EVN-HOUSEHOLD";
    public const string WaterScope = "HCMC-BENTHANH";

    public static readonly DateOnly Period1Start = new(2026, 9, 1);
    public static readonly DateOnly Period1End = new(2026, 9, 30);
    public static readonly DateOnly Period2Start = new(2026, 10, 1);
    public static readonly DateOnly Period2End = new(2026, 10, 31);

    public static readonly IReadOnlyDictionary<string, decimal> ExpectedPeriodTotals =
        new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            ["A101-2026-09"] = 3_433_300m,
            ["A101-2026-10"] = 3_458_100m,
            ["B201-2026-09"] = 4_377_500m,
            ["B201-2026-10"] = 4_390_400m,
        };

    private static readonly DateTime FixedCreatedAt = new(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTimeOffset FixedChangedAt = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FixedToggleAt = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    private static readonly Dictionary<string, decimal> ServiceTotalsByRoom = new(StringComparer.Ordinal)
    {
        [RoomA101Code] = 180_000m,
        [RoomB201Code] = 190_000m,
    };

    public sealed record DemoSeedSummary(
        int Roles,
        int Users,
        int Properties,
        int Rooms,
        int Contracts,
        int Readings,
        int ReadingAudits,
        int Services,
        int PendingApprovals,
        int TrashItems,
        IReadOnlyDictionary<string, decimal> PeriodTotals);

    public static async Task<DemoSeedSummary> EnsureAsync(
        ApplicationDbContext db,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var roleIds = await EnsureRolesAsync(db, cancellationToken);
            var users = await EnsureUsersAsync(db, roleIds, password, cancellationToken);
            var ownerA = users[OwnerAEmail];
            var ownerB = users[OwnerBEmail];
            var managerA = users[ManagerAEmail];
            var tenantUserA = users[TenantAEmail];
            var tenantUserB = users[TenantBEmail];

            var propertyA = await EnsurePropertyAsync(
                db, PropertyAName, "Số 1 Ung Văn Khiêm, Phường Bình Thạnh, Thành phố Hồ Chí Minh", ownerA.Id, cancellationToken);
            var propertyB = await EnsurePropertyAsync(
                db, PropertyBName, "Số 2 Võ Văn Ngân, Phường Thủ Đức, Thành phố Hồ Chí Minh", ownerB.Id, cancellationToken);

            var roomA101 = await EnsureRoomAsync(
                db, propertyA.Id, RoomA101Code, 1, 25m, 3_000_000m, RoomStatus.Rented, false, ownerA.Id, cancellationToken);
            var roomA102 = await EnsureRoomAsync(
                db, propertyA.Id, RoomA102Code, 1, 22m, 3_200_000m, RoomStatus.Available, false, ownerA.Id, cancellationToken);
            var roomA103 = await EnsureRoomAsync(
                db, propertyA.Id, RoomA103Code, 1, 20m, 3_000_000m, RoomStatus.Available, true, ownerA.Id, cancellationToken);
            var roomB201 = await EnsureRoomAsync(
                db, propertyB.Id, RoomB201Code, 2, 28m, 4_000_000m, RoomStatus.Rented, false, ownerB.Id, cancellationToken);
            var roomB202 = await EnsureRoomAsync(
                db, propertyB.Id, RoomB202Code, 2, 26m, 4_200_000m, RoomStatus.Available, false, ownerB.Id, cancellationToken);

            var tenantA = await EnsureTenantAsync(
                db, "Khách thuê Demo A", "0000000001", "000000000001", tenantUserA.Id,
                "Số 3 Nguyễn Trãi, Phường Bến Thành, Thành phố Hồ Chí Minh", false, ownerA.Id, cancellationToken);
            var tenantB = await EnsureTenantAsync(
                db, "Khách thuê Demo B", "0000000002", "000000000002", tenantUserB.Id,
                "Số 4 Lê Lợi, Phường Sài Gòn, Thành phố Hồ Chí Minh", false, ownerB.Id, cancellationToken);
            var tenantTrash = await EnsureTenantAsync(
                db, "Khách Demo đã xóa", "0000000003", "000000000003", null,
                "Số 5 Cách Mạng Tháng 8, Phường Bàn Cờ, Thành phố Hồ Chí Minh", true, ownerA.Id, cancellationToken);

            await EnsureTenantOwnerLinkAsync(db, tenantA.Id, ownerA.Id, cancellationToken);
            await EnsureTenantOwnerLinkAsync(db, tenantB.Id, ownerB.Id, cancellationToken);
            await EnsureTenantOwnerLinkAsync(db, tenantTrash.Id, ownerA.Id, cancellationToken);
            await EnsureManagerAssignmentAsync(db, propertyA.Id, managerA.Id, ownerA.Id, cancellationToken);

            await EnsureContractAsync(
                db, ContractANumber, roomA101.Id, tenantA.Id, 3_000_000m, 6_000_000m, cancellationToken);
            await EnsureContractAsync(
                db, ContractBNumber, roomB201.Id, tenantB.Id, 4_000_000m, 8_000_000m, cancellationToken);

            var readingA1 = await EnsureReadingAsync(
                db, roomA101.Id, Period1Start, Period1End, 1_000m, 1_105m, 100m, 105m, managerA.Id, cancellationToken);
            await EnsureReadingAsync(
                db, roomA101.Id, Period2Start, Period2End, 1_105m, 1_215m, 105m, 111m, managerA.Id, cancellationToken);
            await EnsureReadingAsync(
                db, roomB201.Id, Period1Start, Period1End, 2_000m, 2_080m, 50m, 54m, ownerB.Id, cancellationToken);
            await EnsureReadingAsync(
                db, roomB201.Id, Period2Start, Period2End, 2_080m, 2_160m, 54m, 59m, ownerB.Id, cancellationToken);

            await EnsureReadingAuditAsync(db, readingA1.Id, ownerA.Id, cancellationToken);

            await EnsureServiceCatalogAsync(
                db, propertyA.Id, "Internet", "tháng", "FixedMonthly", 100_000m, ownerA.Id, false, cancellationToken);
            await EnsureServiceCatalogAsync(
                db, propertyA.Id, "Giữ xe", "tháng", "FixedMonthly", 50_000m, ownerA.Id, false, cancellationToken);
            await EnsureServiceCatalogAsync(
                db, propertyA.Id, "Vệ sinh", "tháng", "FixedMonthly", 30_000m, ownerA.Id, false, cancellationToken);
            await EnsureServiceCatalogAsync(
                db, propertyB.Id, "Internet", "tháng", "FixedMonthly", 100_000m, ownerB.Id, false, cancellationToken);
            await EnsureServiceCatalogAsync(
                db, propertyB.Id, "Giữ xe", "tháng", "FixedMonthly", 60_000m, ownerB.Id, false, cancellationToken);
            await EnsureServiceCatalogAsync(
                db, propertyB.Id, "Vệ sinh", "tháng", "FixedMonthly", 30_000m, ownerB.Id, false, cancellationToken);
            await EnsureServiceCatalogAsync(
                db, propertyA.Id, "Truyền hình cáp", "tháng", "FixedMonthly", 50_000m, ownerA.Id, true, cancellationToken);

            await EnsureTogglesAsync(db, ownerA.Id, cancellationToken);
            await EnsurePendingApprovalAsync(db, propertyA.Id, roomA102.Id, managerA.Id, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            var periodTotals = await CalculatePeriodTotalsAsync(db, cancellationToken);
            EnsureExpectedTotals(periodTotals);

            await transaction.CommitAsync(cancellationToken);

            return new DemoSeedSummary(
                Roles: roleIds.Count,
                Users: users.Count,
                Properties: 2,
                Rooms: 5,
                Contracts: 2,
                Readings: 4,
                ReadingAudits: 1,
                Services: 6,
                PendingApprovals: 1,
                TrashItems: 3,
                PeriodTotals: periodTotals);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static async Task<Dictionary<string, string>> EnsureRolesAsync(
        ApplicationDbContext db, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var roleName in Roles.All)
        {
            var role = await db.Roles.FirstOrDefaultAsync(item => item.Name == roleName, cancellationToken);
            if (role is null)
            {
                role = new IdentityRole(roleName)
                {
                    NormalizedName = roleName.ToUpperInvariant(),
                };
                db.Roles.Add(role);
                await db.SaveChangesAsync(cancellationToken);
            }

            result[roleName] = role.Id;
        }

        return result;
    }

    private static async Task<Dictionary<string, ApplicationUser>> EnsureUsersAsync(
        ApplicationDbContext db,
        IReadOnlyDictionary<string, string> roleIds,
        string password,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, ApplicationUser>(StringComparer.Ordinal);
        var hasher = new PasswordHasher<ApplicationUser>();
        foreach (var (email, roleName) in Accounts)
        {
            var fullName = DisplayNames[email];
            var normalized = email.ToUpperInvariant();
            var user = await db.Users.FirstOrDefaultAsync(item => item.NormalizedEmail == normalized, cancellationToken);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid().ToString(),
                    UserName = email,
                    NormalizedUserName = normalized,
                    Email = email,
                    NormalizedEmail = normalized,
                    EmailConfirmed = true,
                    ApprovedAt = DateTime.UtcNow,
                    SecurityStamp = Guid.NewGuid().ToString(),
                    ConcurrencyStamp = Guid.NewGuid().ToString(),
                    FullName = fullName,
                };
                db.Users.Add(user);
            }
            else
            {
                user.FullName = fullName;
                user.EmailConfirmed = true;
                user.ApprovedAt ??= DateTime.UtcNow;
                user.IsDeleted = false;
            }

            user.PasswordHash = hasher.HashPassword(user, password);
            await db.SaveChangesAsync(cancellationToken);

            var roleId = roleIds[roleName];
            if (!await db.UserRoles.AnyAsync(
                    item => item.UserId == user.Id && item.RoleId == roleId, cancellationToken))
            {
                db.UserRoles.Add(new IdentityUserRole<string> { UserId = user.Id, RoleId = roleId });
            }

            result[email] = user;
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task<Property> EnsurePropertyAsync(
        ApplicationDbContext db,
        string name,
        string address,
        string ownerId,
        CancellationToken cancellationToken)
    {
        var property = await db.Properties.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Name == name, cancellationToken);
        if (property is null)
        {
            property = new Property
            {
                Name = name,
                Address = address,
                OwnerId = ownerId,
                IsActive = true,
                CreatedAt = FixedCreatedAt,
            };
            db.Properties.Add(property);
        }
        else
        {
            property.Address = address;
            property.OwnerId = ownerId;
            property.IsActive = true;
            property.IsDeleted = false;
            property.DeletedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return property;
    }

    private static async Task<Room> EnsureRoomAsync(
        ApplicationDbContext db,
        int propertyId,
        string code,
        int floor,
        decimal area,
        decimal price,
        RoomStatus status,
        bool deleted,
        string actorId,
        CancellationToken cancellationToken)
    {
        var room = await db.Rooms.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.PropertyId == propertyId && item.Code == code, cancellationToken);
        if (room is null)
        {
            room = new Room
            {
                PropertyId = propertyId,
                Code = code,
                Floor = floor,
                AreaM2 = area,
                StandardPrice = price,
                Status = status,
                IsActive = true,
                CreatedAt = FixedCreatedAt,
                IsDeleted = deleted,
                DeletedAt = deleted ? FixedCreatedAt : null,
                DeletedBy = deleted ? actorId : null,
            };
            db.Rooms.Add(room);
        }
        else
        {
            room.Floor = floor;
            room.AreaM2 = area;
            room.StandardPrice = price;
            room.Status = status;
            room.IsActive = true;
            room.IsDeleted = deleted;
            room.DeletedAt = deleted ? FixedCreatedAt : null;
            room.DeletedBy = deleted ? actorId : null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return room;
    }

    private static async Task<Tenant> EnsureTenantAsync(
        ApplicationDbContext db,
        string fullName,
        string phone,
        string citizenId,
        string? userId,
        string address,
        bool deleted,
        string actorId,
        CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.CitizenId == citizenId, cancellationToken);
        if (tenant is null)
        {
            tenant = new Tenant
            {
                FullName = fullName,
                Phone = phone,
                CitizenId = citizenId,
                PermanentAddress = address,
                UserId = userId,
                IsActive = !deleted,
                CreatedAt = FixedCreatedAt,
                IsDeleted = deleted,
                DeletedAt = deleted ? FixedCreatedAt : null,
                DeletedByUserId = deleted ? actorId : null,
            };
            db.Tenants.Add(tenant);
        }
        else
        {
            tenant.FullName = fullName;
            tenant.Phone = phone;
            tenant.PermanentAddress = address;
            tenant.UserId = userId;
            tenant.IsActive = !deleted;
            tenant.IsDeleted = deleted;
            tenant.DeletedAt = deleted ? FixedCreatedAt : null;
            tenant.DeletedByUserId = deleted ? actorId : null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return tenant;
    }

    private static async Task EnsureTenantOwnerLinkAsync(
        ApplicationDbContext db,
        int tenantId,
        string ownerId,
        CancellationToken cancellationToken)
    {
        if (await db.TenantOwnerLinks.AnyAsync(
                item => item.TenantId == tenantId && item.OwnerId == ownerId, cancellationToken))
        {
            return;
        }

        db.TenantOwnerLinks.Add(new TenantOwnerLink
        {
            TenantId = tenantId,
            OwnerId = ownerId,
            CreatedAt = FixedCreatedAt,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureManagerAssignmentAsync(
        ApplicationDbContext db,
        int propertyId,
        string managerId,
        string ownerId,
        CancellationToken cancellationToken)
    {
        if (await db.PropertyManagers.AnyAsync(
                item => item.PropertyId == propertyId
                    && item.ManagerUserId == managerId
                    && item.RevokedAt == null,
                cancellationToken))
        {
            return;
        }

        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = propertyId,
            ManagerUserId = managerId,
            AssignedAt = FixedCreatedAt,
            AssignedByUserId = ownerId,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureContractAsync(
        ApplicationDbContext db,
        string contractNumber,
        int roomId,
        int tenantId,
        decimal price,
        decimal deposit,
        CancellationToken cancellationToken)
    {
        var contract = await db.Contracts
            .FirstOrDefaultAsync(item => item.ContractNumber == contractNumber, cancellationToken);
        if (contract is null)
        {
            contract = new Contract
            {
                ContractNumber = contractNumber,
                RoomId = roomId,
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2027, 12, 31),
                SnapshotPrice = price,
                DepositAmount = deposit,
                Status = ContractStatus.Approved,
            };
            db.Contracts.Add(contract);
            await db.SaveChangesAsync(cancellationToken);
        }
        // Đã có hợp đồng demo: giữ nguyên để không kích hoạt trigger chồng lấn khi chạy lại.

        if (!await db.ContractMembers.AnyAsync(
                item => item.ContractId == contract.ContractId && item.TenantId == tenantId, cancellationToken))
        {
            db.ContractMembers.Add(new ContractMember
            {
                ContractId = contract.ContractId,
                TenantId = tenantId,
            });
        }

        var room = await db.Rooms.FirstAsync(item => item.Id == roomId, cancellationToken);
        room.Status = RoomStatus.Rented;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<UtilityReading> EnsureReadingAsync(
        ApplicationDbContext db,
        int roomId,
        DateOnly periodStart,
        DateOnly periodEnd,
        decimal electricStart,
        decimal electricEnd,
        decimal waterStart,
        decimal waterEnd,
        string recordedByUserId,
        CancellationToken cancellationToken)
    {
        var reading = await db.UtilityReadings.FirstOrDefaultAsync(
            item => item.RoomId == roomId && item.PeriodStart == periodStart, cancellationToken);
        if (reading is null)
        {
            reading = new UtilityReading
            {
                RoomId = roomId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                ElectricStart = electricStart,
                ElectricEnd = electricEnd,
                WaterStart = waterStart,
                WaterEnd = waterEnd,
                RecordedByUserId = recordedByUserId,
                CreatedAt = FixedChangedAt,
            };
            db.UtilityReadings.Add(reading);
        }
        else
        {
            reading.PeriodEnd = periodEnd;
            reading.ElectricStart = electricStart;
            reading.ElectricEnd = electricEnd;
            reading.WaterStart = waterStart;
            reading.WaterEnd = waterEnd;
            reading.RecordedByUserId = recordedByUserId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return reading;
    }

    private static async Task EnsureReadingAuditAsync(
        ApplicationDbContext db,
        int readingId,
        string actorId,
        CancellationToken cancellationToken)
    {
        if (await db.UtilityReadingAudits.AnyAsync(
                item => item.UtilityReadingId == readingId, cancellationToken))
        {
            return;
        }

        db.UtilityReadingAudits.Add(new UtilityReadingAudit
        {
            UtilityReadingId = readingId,
            OldElectricEnd = 1_100m,
            NewElectricEnd = 1_105m,
            OldWaterEnd = 104m,
            NewWaterEnd = 105m,
            Reason = "Điều chỉnh chỉ số demo 3A",
            ChangedByUserId = actorId,
            ChangedAt = FixedChangedAt,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<ServiceCatalog> EnsureServiceCatalogAsync(
        ApplicationDbContext db,
        int propertyId,
        string name,
        string unit,
        string calculationMethod,
        decimal price,
        string ownerId,
        bool deleted,
        CancellationToken cancellationToken)
    {
        var catalogs = await db.ServiceCatalogs.IgnoreQueryFilters()
            .Include(item => item.PriceHistory)
            .Where(item => item.PropertyId == propertyId)
            .ToListAsync(cancellationToken);
        var catalog = catalogs.FirstOrDefault(item =>
            item.PriceHistory.Any(version => version.Name == name));

        if (catalog is null)
        {
            catalog = new ServiceCatalog
            {
                PropertyId = propertyId,
                Description = $"Demo 3A: {name}",
                IsActive = true,
                IsReadOnly = false,
                CreatedAt = FixedCreatedAt,
                IsDeleted = deleted,
                DeletedAt = deleted ? FixedCreatedAt : null,
                DeletedBy = deleted ? ownerId : null,
            };
            db.ServiceCatalogs.Add(catalog);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            catalog.Description = $"Demo 3A: {name}";
            catalog.IsActive = !deleted;
            catalog.IsDeleted = deleted;
            catalog.DeletedAt = deleted ? FixedCreatedAt : null;
            catalog.DeletedBy = deleted ? ownerId : null;
        }

        var effectiveDate = new DateOnly(2026, 1, 1);
        var version = catalog.PriceHistory.FirstOrDefault(item => item.EffectiveDate == effectiveDate)
            ?? await db.ServicePriceHistories.FirstOrDefaultAsync(
                item => item.ServiceCatalogId == catalog.Id && item.EffectiveDate == effectiveDate,
                cancellationToken);
        if (version is null)
        {
            version = new ServicePriceHistory
            {
                ServiceCatalogId = catalog.Id,
                Name = name,
                Unit = unit,
                CalculationMethod = calculationMethod,
                UnitPrice = price,
                EffectiveDate = effectiveDate,
                CreatedAt = FixedCreatedAt,
                CreatedByUserId = ownerId,
                Note = "Dữ liệu demo 3A",
            };
            db.ServicePriceHistories.Add(version);
        }
        else
        {
            version.Name = name;
            version.Unit = unit;
            version.CalculationMethod = calculationMethod;
            version.UnitPrice = price;
            version.CreatedByUserId = ownerId;
            version.Note = "Dữ liệu demo 3A";
        }

        await db.SaveChangesAsync(cancellationToken);
        return catalog;
    }

    private static async Task EnsureTogglesAsync(
        ApplicationDbContext db,
        string actorId,
        CancellationToken cancellationToken)
    {
        var existing = await db.ManagerToggles.ToDictionaryAsync(item => item.Code, cancellationToken);
        foreach (var definition in ManagerToggleCatalog.All)
        {
            if (!existing.ContainsKey(definition.Code))
            {
                var added = new ManagerToggle
                {
                    Code = definition.Code,
                    Mode = definition.DefaultMode,
                    UpdatedAt = FixedToggleAt,
                };
                db.ManagerToggles.Add(added);
                existing[definition.Code] = added;
            }
        }

        var changed = new Dictionary<string, ManagerToggleMode>(StringComparer.Ordinal)
        {
            ["SW-01"] = ManagerToggleMode.Allow,
            ["SW-02"] = ManagerToggleMode.Approve,
            ["SW-03"] = ManagerToggleMode.Deny,
        };
        foreach (var (code, mode) in changed)
        {
            var row = existing.GetValueOrDefault(code)
                ?? await db.ManagerToggles.FirstOrDefaultAsync(item => item.Code == code, cancellationToken);
            if (row is null)
            {
                continue;
            }

            row.Mode = mode;
            row.UpdatedAt = FixedToggleAt;
            row.UpdatedByUserId = actorId;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsurePendingApprovalAsync(
        ApplicationDbContext db,
        int propertyId,
        int roomId,
        string managerId,
        CancellationToken cancellationToken)
    {
        var targetKey = roomId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (await db.ManagerApprovalRequests.AnyAsync(
                item => item.OperationCode == "SW-02"
                    && item.TargetType == ApprovalTargetType.Room
                    && item.TargetKey == targetKey
                    && item.Status == ApprovalRequestStatus.Pending,
                cancellationToken))
        {
            return;
        }

        db.ManagerApprovalRequests.Add(new ManagerApprovalRequest
        {
            OperationCode = "SW-02",
            TargetType = ApprovalTargetType.Room,
            TargetKey = targetKey,
            PropertyId = propertyId,
            PayloadJson = JsonSerializer.Serialize(new { roomId, standardPrice = 3_200_000m }),
            Status = ApprovalRequestStatus.Pending,
            RequestedByUserId = managerId,
            RequestedAt = FixedCreatedAt,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Dictionary<string, decimal>> CalculatePeriodTotalsAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var tariffService = new UtilityTariffService(db);
        var contracts = await db.Contracts.AsNoTracking()
            .ToDictionaryAsync(item => item.ContractNumber, cancellationToken);
        var rooms = await db.Rooms.AsNoTracking()
            .Where(item => item.Code == RoomA101Code || item.Code == RoomB201Code)
            .ToDictionaryAsync(item => item.Code, cancellationToken);
        var readings = await db.UtilityReadings.AsNoTracking()
            .Where(item => item.RoomId == rooms[RoomA101Code].Id || item.RoomId == rooms[RoomB201Code].Id)
            .ToListAsync(cancellationToken);

        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (roomCode, contractNumber) in new[]
        {
            (RoomA101Code, ContractANumber),
            (RoomB201Code, ContractBNumber),
        })
        {
            var room = rooms[roomCode];
            var contract = contracts[contractNumber];
            foreach (var reading in readings.Where(item => item.RoomId == room.Id))
            {
                var electric = await tariffService.CalculateForPeriodAsync(
                    TariffType.Electric,
                    ElectricScope,
                    reading.PeriodStart,
                    reading.PeriodEnd,
                    reading.ElectricEnd - reading.ElectricStart,
                    residentCount: 1,
                    cancellationToken: cancellationToken);
                var water = await tariffService.CalculateForPeriodAsync(
                    TariffType.Water,
                    WaterScope,
                    reading.PeriodStart,
                    reading.PeriodEnd,
                    reading.WaterEnd - reading.WaterStart,
                    residentCount: 1,
                    cancellationToken: cancellationToken);

                var key = $"{roomCode}-{reading.PeriodStart:yyyy-MM}";
                result[key] = contract.SnapshotPrice
                    + ServiceTotalsByRoom[roomCode]
                    + electric.Total
                    + water.Total;
            }
        }

        return result;
    }

    private static void EnsureExpectedTotals(IReadOnlyDictionary<string, decimal> actual)
    {
        foreach (var (key, expected) in ExpectedPeriodTotals)
        {
            if (!actual.TryGetValue(key, out var value) || value != expected)
            {
                throw new InvalidOperationException(
                    $"Đáp án demo 3A sai ở '{key}': mong đợi {expected}, thực tế {value}. Kiểm tra lại biểu giá/chỉ số.");
            }
        }
    }
}
