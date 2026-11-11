using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.Services;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Services;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants.Entities;
using Web.Features.Utilities.Entities;

namespace NhaTro.IntegrationTests.Features.Contracts;

/// <summary>
/// P4 — gia hạn / thanh lý trên SQL Server thật: copy đăng ký dịch vụ theo tháng, chốt yêu cầu
/// gia hạn của khách, và quy tắc trạng thái phòng khi thanh lý.
/// </summary>
public sealed class ContractLifecycleP4IntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task RenewContractAsync_CopiesFutureServiceRegistrations()
    {
        var seed = await SeedAsync();
        await using var db = seed.Db;

        var catalog = new ServiceCatalog { PropertyId = seed.Property.Id };
        db.ServiceCatalogs.Add(catalog);
        await db.SaveChangesAsync();

        // Tháng trước tháng bắt đầu hợp đồng mới thì không copy; đúng tháng và sau thì copy.
        db.ContractServiceRegistrations.AddRange(
            NewRegistration(seed.Contract.ContractId, catalog.Id, new DateOnly(2027, 9, 1)),
            NewRegistration(seed.Contract.ContractId, catalog.Id, new DateOnly(2027, 10, 1)),
            NewRegistration(seed.Contract.ContractId, catalog.Id, new DateOnly(2027, 11, 1)));
        await db.SaveChangesAsync();

        var service = new ContractService(db, LifecycleClock);
        var result = await service.RenewContractAsync(
            seed.Contract.ContractId,
            new RenewContractRequestDto { EndDate = new DateTime(2028, 10, 21) },
            OwnerCaller);

        Assert.Equal(ContractActionOutcome.Applied, result.Outcome);
        var renewalId = result.ContractId!.Value;

        db.ChangeTracker.Clear();
        var copied = await db.ContractServiceRegistrations.AsNoTracking()
            .Where(item => item.ContractId == renewalId)
            .Select(item => item.MonthStart)
            .OrderBy(month => month)
            .ToListAsync();
        Assert.Equal([new DateOnly(2027, 10, 1), new DateOnly(2027, 11, 1)], copied);
        Assert.All(
            await db.ContractServiceRegistrations.AsNoTracking()
                .Where(item => item.ContractId == renewalId).ToListAsync(),
            item => Assert.Equal(Owner, item.RegisteredByUserId));

        // Hợp đồng cũ giữ nguyên đủ ba dòng, không bị nhân đôi.
        Assert.Equal(3, await db.ContractServiceRegistrations.AsNoTracking()
            .CountAsync(item => item.ContractId == seed.Contract.ContractId));
    }

    [SqlServerFact]
    public async Task RenewContractAsync_SettlesPendingTenantRenewalRequest()
    {
        var seed = await SeedAsync();
        await using var db = seed.Db;

        var tenant = new Tenant { FullName = "P4 tenant", Phone = "0900000201" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        db.ContractRenewalRequests.Add(new ContractRenewalRequest
        {
            ContractId = seed.Contract.ContractId,
            RequestedByTenantId = tenant.Id,
            RequestedMonths = 6,
            Status = RenewalRequestStatus.Pending,
            RequestedAt = new DateTime(2026, 10, 1),
        });
        await db.SaveChangesAsync();

        var service = new ContractService(db, LifecycleClock);
        var result = await service.RenewContractAsync(
            seed.Contract.ContractId,
            new RenewContractRequestDto { EndDate = new DateTime(2028, 10, 21) },
            OwnerCaller);

        Assert.Equal(ContractActionOutcome.Applied, result.Outcome);
        db.ChangeTracker.Clear();
        var request = await db.ContractRenewalRequests.AsNoTracking().SingleAsync();
        Assert.Equal(RenewalRequestStatus.Approved, request.Status);
        Assert.Equal(Owner, request.DecidedByUserId);
        Assert.NotNull(request.DecidedAt);
        Assert.Null(request.RejectReason);
    }

    [SqlServerFact]
    public async Task LiquidateContractAsync_KeepsRoomUnderMaintenance()
    {
        var seed = await SeedAsync(roomStatus: RoomStatus.Maintenance);
        await using var db = seed.Db;
        var service = new ContractService(db, LifecycleClock);

        var result = await service.LiquidateContractAsync(seed.Contract.ContractId, OwnerCaller);

        Assert.Equal(ContractActionOutcome.Applied, result.Outcome);
        db.ChangeTracker.Clear();
        Assert.Equal(ContractStatus.Liquidated, await db.Contracts.AsNoTracking()
            .Where(item => item.ContractId == seed.Contract.ContractId)
            .Select(item => item.Status)
            .SingleAsync());
        Assert.Equal(RoomStatus.Maintenance, await db.Rooms.AsNoTracking()
            .Where(item => item.Id == seed.Room.Id).Select(item => item.Status).SingleAsync());
    }

    [SqlServerFact]
    public async Task LiquidateContractAsync_KeepsRoomRentedWhenAnotherContractCoversToday()
    {
        var seed = await SeedAsync();
        await using var db = seed.Db;

        // Trigger chống chồng lịch không cho hai hợp đồng Approved phủ nhau; tắt tạm để dựng
        // đúng tình huống dữ liệu cũ (hai hợp đồng cùng phòng đang hiệu lực hôm nay).
        await db.Database.ExecuteSqlRawAsync(
            "DROP TRIGGER IF EXISTS [dbo].[TR_Contracts_PreventApprovedOverlap];");
        db.Contracts.Add(new Contract
        {
            ContractNumber = "HD-P4-OVERLAP",
            RoomId = seed.Room.Id,
            StartDate = new DateTime(2026, 10, 1),
            EndDate = new DateTime(2027, 10, 1),
            SnapshotPrice = 1_000_000m,
            DepositAmount = 200_000m,
            Status = ContractStatus.Approved,
        });
        await db.SaveChangesAsync();

        var service = new ContractService(db, LifecycleClock);
        var result = await service.LiquidateContractAsync(seed.Contract.ContractId, OwnerCaller);

        Assert.Equal(ContractActionOutcome.Applied, result.Outcome);
        db.ChangeTracker.Clear();
        Assert.Equal(RoomStatus.Rented, await db.Rooms.AsNoTracking()
            .Where(item => item.Id == seed.Room.Id).Select(item => item.Status).SingleAsync());
        Assert.Equal(ContractStatus.Liquidated, await db.Contracts.AsNoTracking()
            .Where(item => item.ContractId == seed.Contract.ContractId)
            .Select(item => item.Status)
            .SingleAsync());
    }

    [SqlServerFact]
    public async Task LiquidateContractAsync_RejectsPendingOrExpiredContract()
    {
        var seed = await SeedAsync();
        await using var db = seed.Db;

        var (pendingRoom, pendingContract) = await AddContractAsync(
            db, seed.Property.Id, "P4-PEND", ContractStatus.Pending,
            new DateTime(2026, 10, 22), new DateTime(2027, 10, 21));
        var (expiredRoom, expiredContract) = await AddContractAsync(
            db, seed.Property.Id, "P4-EXP", ContractStatus.Approved,
            new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));

        var service = new ContractService(db, LifecycleClock);

        var pendingError = await Assert.ThrowsAsync<ContractOperationException>(() =>
            service.LiquidateContractAsync(pendingContract.ContractId, OwnerCaller));
        Assert.Equal(ContractErrorCodes.ContractNotActive, pendingError.ErrorCode);

        var expiredError = await Assert.ThrowsAsync<ContractOperationException>(() =>
            service.LiquidateContractAsync(expiredContract.ContractId, OwnerCaller));
        Assert.Equal(ContractErrorCodes.ContractNotActive, expiredError.ErrorCode);

        db.ChangeTracker.Clear();
        Assert.Equal(RoomStatus.Rented, await db.Rooms.AsNoTracking()
            .Where(item => item.Id == pendingRoom.Id).Select(item => item.Status).SingleAsync());
        Assert.Equal(RoomStatus.Rented, await db.Rooms.AsNoTracking()
            .Where(item => item.Id == expiredRoom.Id).Select(item => item.Status).SingleAsync());
        Assert.Equal(ContractStatus.Pending, await db.Contracts.AsNoTracking()
            .Where(item => item.ContractId == pendingContract.ContractId)
            .Select(item => item.Status).SingleAsync());
        Assert.Equal(ContractStatus.Approved, await db.Contracts.AsNoTracking()
            .Where(item => item.ContractId == expiredContract.ContractId)
            .Select(item => item.Status).SingleAsync());
    }

    [SqlServerFact]
    public async Task ManagerLiquidation_RevalidatesStateAtApprovalTime()
    {
        var seed = await SeedAsync();
        await using var db = seed.Db;
        var approvals = NewApprovals(db, LifecycleClock);
        var service = new ContractService(
            db, LifecycleClock, new FixedTogglePolicy(RoomChangeDecision.RequiresApproval), approvals);

        var result = await service.LiquidateContractAsync(seed.Contract.ContractId, ManagerCaller);
        Assert.Equal(ContractActionOutcome.PendingApproval, result.Outcome);
        Assert.NotNull(result.ApprovalRequestId);

        // Hợp đồng đã bị thanh lý ở nơi khác trước khi Chủ trọ bấm duyệt.
        await using (var other = fixture.CreateContext())
        {
            var contract = await other.Contracts.SingleAsync(
                item => item.ContractId == seed.Contract.ContractId);
            contract.Status = ContractStatus.Liquidated;
            await other.SaveChangesAsync();
        }

        // Duyệt bằng context mới, đúng như mỗi request scoped thật trong production.
        await using var approveDb = fixture.CreateContext();
        var decision = await NewApprovals(approveDb, LifecycleClock)
            .ApproveAsync(result.ApprovalRequestId!.Value, new ApprovalCaller(Owner, Roles.ChuTro));

        Assert.False(decision.Succeeded);
        Assert.Equal(ContractErrorCodes.ContractNotActive, decision.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Equal(RoomStatus.Rented, await db.Rooms.AsNoTracking()
            .Where(item => item.Id == seed.Room.Id).Select(item => item.Status).SingleAsync());
    }

    private const string Owner = "p4-lifecycle-owner";
    private const string Manager = "p4-lifecycle-manager";

    private static PropertyCaller OwnerCaller => new(Owner, Roles.ChuTro);
    private static PropertyCaller ManagerCaller => new(Manager, Roles.QuanLy);

    private static TimeProvider LifecycleClock => new FixedTimeProvider(
        new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed record SeedData(ApplicationDbContext Db, Property Property, Room Room, Contract Contract);

    private async Task<SeedData> SeedAsync(RoomStatus roomStatus = RoomStatus.Rented)
    {
        await fixture.ResetAsync();
        var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Manager, Roles.QuanLy);

        var property = new Property { Name = "P4 lifecycle property", OwnerId = Owner };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = property.Id,
            ManagerUserId = Manager,
            AssignedByUserId = Owner,
        });

        var (room, contract) = await AddContractAsync(
            db, property.Id, "P4-LC-101", ContractStatus.Approved,
            new DateTime(2026, 10, 22), new DateTime(2027, 10, 21), roomStatus);

        return new SeedData(db, property, room, contract);
    }

    private static async Task<(Room Room, Contract Contract)> AddContractAsync(
        ApplicationDbContext db,
        int propertyId,
        string roomCode,
        ContractStatus status,
        DateTime startDate,
        DateTime endDate,
        RoomStatus roomStatus = RoomStatus.Rented)
    {
        var room = new Room
        {
            PropertyId = propertyId,
            Code = roomCode,
            StandardPrice = 1_000_000m,
            Status = roomStatus,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            ContractNumber = $"HD-{roomCode}",
            RoomId = room.Id,
            StartDate = startDate,
            EndDate = endDate,
            SnapshotPrice = 1_000_000m,
            DepositAmount = 200_000m,
            Status = status,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return (room, contract);
    }

    private static ContractServiceRegistration NewRegistration(
        Guid contractId, int catalogId, DateOnly monthStart) => new()
    {
        ContractId = contractId,
        ServiceCatalogId = catalogId,
        MonthStart = monthStart,
        RegisteredByUserId = Owner,
        CreatedAt = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
    };

    private static ManagerApprovalService NewApprovals(ApplicationDbContext db, TimeProvider clock) =>
        new(db,
            clock,
            [new ContractApprovalApplier(clock), new ContractDepositApprovalApplier(clock)],
            NullLogger<ManagerApprovalService>.Instance);

    private sealed class FixedTogglePolicy(RoomChangeDecision decision) : IManagerTogglePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(
            ManagerAction action, string role, CancellationToken ct = default) =>
            Task.FromResult(decision);

        public Task<RoomChangeDecision> EvaluateAsync(
            string code, string role, CancellationToken ct = default) =>
            Task.FromResult(decision);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
