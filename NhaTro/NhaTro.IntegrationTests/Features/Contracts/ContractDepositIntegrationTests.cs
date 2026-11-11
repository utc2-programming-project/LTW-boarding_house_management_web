using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Contracts.Services;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Infrastructure.Time;

namespace NhaTro.IntegrationTests.Features.Contracts;

/// <summary>P4 persistence tests: these use the SQL Server fixture, not EF InMemory.</summary>
public sealed class ContractDepositIntegrationTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task Owner_deposit_update_persists_and_writes_audit()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        const string owner = "p4-deposit-owner";
        await SqlServerFixture.SeedAccountAsync(db, owner, Roles.ChuTro);
        var property = new Property { Name = "P4 deposit", OwnerId = owner };
        db.Properties.Add(property); await db.SaveChangesAsync();
        var room = new Room { PropertyId = property.Id, Code = "P4-101", StandardPrice = 1_000_000m };
        db.Rooms.Add(room); await db.SaveChangesAsync();
        var contract = new Contract { ContractNumber = "HD-P4-DEPOSIT", RoomId = room.Id, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1), SnapshotPrice = 1_000_000m, DepositAmount = 200_000m, Status = ContractStatus.Approved };
        db.Contracts.Add(contract); await db.SaveChangesAsync();

        var service = new ContractService(db, new FixedClock());
        await service.UpdateDepositAsync(contract.ContractId, 300_000m, new PropertyCaller(owner, Roles.ChuTro));
        db.ChangeTracker.Clear();
        Assert.Equal(300_000m, await db.Contracts.Where(x => x.ContractId == contract.ContractId).Select(x => x.DepositAmount).SingleAsync());
        Assert.True(await db.AuditLogEntries.AnyAsync(x => x.Action == "ContractDepositUpdated"));
    }

    [SqlServerFact]
    public async Task Owner_deposit_update_rejects_negative_and_keeps_amount()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        const string owner = "p4-deposit-negative-owner";
        await SqlServerFixture.SeedAccountAsync(db, owner, Roles.ChuTro);
        var property = new Property { Name = "P4 negative", OwnerId = owner };
        db.Properties.Add(property); await db.SaveChangesAsync();
        var room = new Room { PropertyId = property.Id, Code = "P4-102", StandardPrice = 1_000_000m };
        db.Rooms.Add(room); await db.SaveChangesAsync();
        var contract = new Contract { ContractNumber = "HD-P4-NEGATIVE", RoomId = room.Id, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1), SnapshotPrice = 1_000_000m, DepositAmount = 200_000m, Status = ContractStatus.Approved };
        db.Contracts.Add(contract); await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ContractValidationException>(() => new ContractService(db, new FixedClock())
            .UpdateDepositAsync(contract.ContractId, -0.4m, new PropertyCaller(owner, Roles.ChuTro)));
        Assert.Equal(ContractErrorCodes.InvalidDepositAmount, error.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Equal(200_000m, await db.Contracts.Where(x => x.ContractId == contract.ContractId).Select(x => x.DepositAmount).SingleAsync());
    }

    [SqlServerFact]
    public async Task Owner_deposit_update_rejects_pending_contract()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        const string owner = "p4-deposit-pending-owner";
        await SqlServerFixture.SeedAccountAsync(db, owner, Roles.ChuTro);
        var property = new Property { Name = "P4 pending", OwnerId = owner };
        db.Properties.Add(property); await db.SaveChangesAsync();
        var room = new Room { PropertyId = property.Id, Code = "P4-103", StandardPrice = 1_000_000m };
        db.Rooms.Add(room); await db.SaveChangesAsync();
        var contract = new Contract { ContractNumber = "HD-P4-PENDING", RoomId = room.Id, StartDate = new DateTime(2026, 1, 1), EndDate = new DateTime(2027, 1, 1), SnapshotPrice = 1_000_000m, DepositAmount = 200_000m, Status = ContractStatus.Pending };
        db.Contracts.Add(contract); await db.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<ContractOperationException>(() => new ContractService(db, new FixedClock())
            .UpdateDepositAsync(contract.ContractId, 300_000m, new PropertyCaller(owner, Roles.ChuTro)));
        Assert.Equal(ContractErrorCodes.ContractNotActive, error.ErrorCode);
    }

    [SqlServerFact]
    public async Task UpdateDepositAsync_RejectsLiquidatedAndExpiredContracts()
    {
        var seeded = await SeedLifecycleContractAsync(
            ContractStatus.Liquidated, RoomStatus.Available);
        await using var db = seeded.Db;
        var service = new ContractService(db, new FixedClock());

        var liquidatedError = await Assert.ThrowsAsync<ContractOperationException>(() => service
            .UpdateDepositAsync(seeded.Contract.ContractId, 300_000m, OwnerCaller));
        Assert.Equal(ContractErrorCodes.ContractNotActive, liquidatedError.ErrorCode);

        var expired = await SeedLifecycleContractAsync(
            ContractStatus.Approved, RoomStatus.Rented,
            startDate: new DateTime(2025, 1, 1), endDate: new DateTime(2025, 12, 31));
        await using var expiredDb = expired.Db;
        var expiredService = new ContractService(expiredDb, new FixedClock());

        var expiredError = await Assert.ThrowsAsync<ContractOperationException>(() => expiredService
            .UpdateDepositAsync(expired.Contract.ContractId, 300_000m, OwnerCaller));
        Assert.Equal(ContractErrorCodes.ContractNotActive, expiredError.ErrorCode);
        expiredDb.ChangeTracker.Clear();
        Assert.Equal(200_000m, await DepositAsync(expiredDb, expired.Contract.ContractId));
    }

    // ---------- SW-08: Quản lý đổi cọc phải chờ Chủ trọ duyệt ----------

    [SqlServerFact]
    public async Task ManagerDepositUpdate_CreatesSw08RequestWithoutChangingData()
    {
        var seeded = await SeedLifecycleContractAsync();
        await using var db = seeded.Db;
        var (contracts, _) = NewLifecycleServices(db, RoomChangeDecision.RequiresApproval);

        var result = await contracts.UpdateDepositAsync(
            seeded.Contract.ContractId, 350_000m, ManagerCaller);

        Assert.Equal(ContractActionOutcome.PendingApproval, result.Outcome);
        Assert.NotNull(result.ApprovalRequestId);
        db.ChangeTracker.Clear();
        var request = await db.ManagerApprovalRequests.AsNoTracking().SingleAsync();
        Assert.Equal("SW-08", request.OperationCode);
        Assert.Equal(ApprovalTargetType.Contract, request.TargetType);
        Assert.Equal(seeded.Contract.ContractId.ToString("D"), request.TargetKey);
        Assert.Equal(seeded.Property.Id, request.PropertyId);

        var payload = JsonSerializer.Deserialize<ContractApprovalPayload>(request.PayloadJson, PayloadJson);
        Assert.NotNull(payload);
        Assert.Equal(ContractApprovalActions.UpdateDeposit, payload.Action);
        Assert.Equal(200_000m, payload.ExpectedDepositAmount);
        Assert.Equal(350_000m, payload.DepositAmount);
        Assert.Equal(200_000m, await DepositAsync(db, seeded.Contract.ContractId));
    }

    [SqlServerFact]
    public async Task ManagerDepositUpdate_ApproveAppliesOnce()
    {
        var seeded = await SeedLifecycleContractAsync();
        await using var db = seeded.Db;
        var (contracts, _) = NewLifecycleServices(db, RoomChangeDecision.RequiresApproval);
        var pending = await contracts.UpdateDepositAsync(
            seeded.Contract.ContractId, 350_000m, ManagerCaller);

        var first = await ApproveAsync(pending.ApprovalRequestId!.Value);
        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal(ApprovalRequestStatus.Approved, first.Request!.Status);

        db.ChangeTracker.Clear();
        Assert.Equal(350_000m, await DepositAsync(db, seeded.Contract.ContractId));
        Assert.Equal(1, await db.AuditLogEntries.CountAsync(
            entry => entry.Action == ContractLifecycleOperations.ContractAuditActions.DepositUpdated));

        var second = await ApproveAsync(pending.ApprovalRequestId!.Value);
        Assert.False(second.Succeeded);
        Assert.Equal(ApprovalErrorCodes.AlreadyDecided, second.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Equal(350_000m, await DepositAsync(db, seeded.Contract.ContractId));
        Assert.Equal(1, await db.AuditLogEntries.CountAsync(
            entry => entry.Action == ContractLifecycleOperations.ContractAuditActions.DepositUpdated));
    }

    [SqlServerFact]
    public async Task ManagerDepositUpdate_ApproveFailsWhenDepositChangedAfterRequest()
    {
        var seeded = await SeedLifecycleContractAsync();
        await using var db = seeded.Db;
        var (contracts, _) = NewLifecycleServices(db, RoomChangeDecision.RequiresApproval);
        var pending = await contracts.UpdateDepositAsync(
            seeded.Contract.ContractId, 350_000m, ManagerCaller);

        // Người khác sửa cọc bằng context khác trong lúc yêu cầu còn Chờ duyệt.
        await using (var other = fixture.CreateContext())
        {
            var stale = await other.Contracts.SingleAsync(
                item => item.ContractId == seeded.Contract.ContractId);
            stale.DepositAmount = 400_000m;
            await other.SaveChangesAsync();
        }

        var approved = await ApproveAsync(pending.ApprovalRequestId!.Value);

        Assert.False(approved.Succeeded);
        Assert.Equal(ContractErrorCodes.InvalidStatus, approved.ErrorCode);
        Assert.Equal(ContractErrorMessages.DepositChangedAfterRequest, approved.ErrorMessage);
        db.ChangeTracker.Clear();
        Assert.Equal(400_000m, await DepositAsync(db, seeded.Contract.ContractId));
        Assert.Equal(ApprovalRequestStatus.Pending, await db.ManagerApprovalRequests.AsNoTracking()
            .Where(request => request.Id == pending.ApprovalRequestId!.Value)
            .Select(request => request.Status)
            .SingleAsync());
    }

    [SqlServerFact]
    public async Task ManagerDepositUpdate_IsBlockedWhenSw08Denies()
    {
        var seeded = await SeedLifecycleContractAsync();
        await using var db = seeded.Db;
        var (contracts, _) = NewLifecycleServices(db, RoomChangeDecision.Denied);

        var error = await Assert.ThrowsAsync<ContractOperationException>(() => contracts
            .UpdateDepositAsync(seeded.Contract.ContractId, 350_000m, ManagerCaller));

        Assert.Equal(ContractErrorCodes.DepositApprovalDenied, error.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Equal(200_000m, await DepositAsync(db, seeded.Contract.ContractId));
        Assert.Empty(await db.ManagerApprovalRequests.AsNoTracking().ToListAsync());
    }

    [SqlServerFact]
    public async Task ManagerDepositUpdate_IsBlockedWhenAnotherApprovalIsPending()
    {
        var seeded = await SeedLifecycleContractAsync();
        await using var db = seeded.Db;
        var (contracts, _) = NewLifecycleServices(db, RoomChangeDecision.RequiresApproval);

        var renewal = await contracts.RenewContractAsync(
            seeded.Contract.ContractId,
            new RenewContractRequestDto { EndDate = new DateTime(2028, 1, 1) },
            ManagerCaller);
        Assert.Equal(ContractActionOutcome.PendingApproval, renewal.Outcome);

        var error = await Assert.ThrowsAsync<ContractOperationException>(() => contracts
            .UpdateDepositAsync(seeded.Contract.ContractId, 350_000m, ManagerCaller));

        Assert.Equal(ContractErrorCodes.DuplicatePendingRequest, error.ErrorCode);
        db.ChangeTracker.Clear();
        Assert.Equal(200_000m, await DepositAsync(db, seeded.Contract.ContractId));
        Assert.Single(await db.ManagerApprovalRequests.AsNoTracking().ToListAsync());
    }

    [SqlServerFact]
    public async Task Detail_ReportsPendingApprovalAndCanEditDeposit()
    {
        var seeded = await SeedLifecycleContractAsync();
        await using var db = seeded.Db;
        var (contracts, _) = NewLifecycleServices(db, RoomChangeDecision.RequiresApproval);
        var pending = await contracts.UpdateDepositAsync(
            seeded.Contract.ContractId, 350_000m, ManagerCaller);

        db.ChangeTracker.Clear();
        var before = await contracts.GetAsync(seeded.Contract.ContractId, OwnerCaller);
        Assert.Equal(ContractPendingApproval.DepositUpdate, before.PendingApproval);
        Assert.False(before.CanEditDeposit);
        Assert.False(before.CanRenew);
        Assert.False(before.CanLiquidate);

        var approved = await ApproveAsync(pending.ApprovalRequestId!.Value);
        Assert.True(approved.Succeeded, approved.ErrorMessage);

        db.ChangeTracker.Clear();
        var after = await contracts.GetAsync(seeded.Contract.ContractId, OwnerCaller);
        Assert.Equal(ContractPendingApproval.None, after.PendingApproval);
        Assert.True(after.CanEditDeposit);
        Assert.True(after.CanRenew);
        Assert.True(after.CanLiquidate);
        Assert.Equal(350_000m, after.DepositAmount);
    }

    [SqlServerFact]
    public async Task ContractApprovalSummary_ScopesToOwnerAndAssignedManager()
    {
        var seeded = await SeedLifecycleContractAsync();
        await using var db = seeded.Db;
        var (contracts, approvals) = NewLifecycleServices(db, RoomChangeDecision.RequiresApproval);
        await contracts.UpdateDepositAsync(seeded.Contract.ContractId, 350_000m, ManagerCaller);
        db.ChangeTracker.Clear();
        var summaries = new ContractApprovalSummaryService(db);

        var ownerInbox = await approvals.ListInboxAsync(OwnerApprover);
        Assert.True(ownerInbox.Succeeded);
        var ownerSummary = Assert.Single(await summaries.GetAsync(ownerInbox.Items, Owner, Roles.ChuTro)).Value;
        Assert.Equal(seeded.Contract.ContractNumber, ownerSummary.ContractNumber);
        Assert.Equal(seeded.Room.Code, ownerSummary.RoomCode);
        Assert.Equal(seeded.Property.Name, ownerSummary.PropertyName);
        Assert.Equal(ContractApprovalActions.UpdateDeposit, ownerSummary.Action);
        Assert.Equal(200_000m, ownerSummary.CurrentDeposit);
        Assert.Equal(350_000m, ownerSummary.ProposedDeposit);
        Assert.Null(ownerSummary.NewEndDate);

        var managerMine = await approvals.ListMineAsync(new ApprovalCaller(Manager, Roles.QuanLy));
        Assert.True(managerMine.Succeeded);
        Assert.Single(await summaries.GetAsync(managerMine.Items, Manager, Roles.QuanLy));

        // Chủ trọ khác không nằm trong phạm vi thì không thấy tóm tắt nào.
        var otherOwner = new ApprovalCaller(OtherOwner, Roles.ChuTro);
        var otherInbox = await approvals.ListInboxAsync(otherOwner);
        Assert.True(otherInbox.Succeeded);
        Assert.Empty(await summaries.GetAsync(otherInbox.Items, OtherOwner, Roles.ChuTro));
    }

    private const string Owner = "p4-deposit-lifecycle-owner";
    private const string Manager = "p4-deposit-lifecycle-manager";
    private const string OtherOwner = "p4-deposit-lifecycle-other-owner";

    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    private static PropertyCaller OwnerCaller => new(Owner, Roles.ChuTro);
    private static PropertyCaller ManagerCaller => new(Manager, Roles.QuanLy);
    private static ApprovalCaller OwnerApprover => new(Owner, Roles.ChuTro);

    private sealed record SeededContract(
        ApplicationDbContext Db, Property Property, Room Room, Contract Contract);

    private async Task<SeededContract> SeedLifecycleContractAsync(
        ContractStatus status = ContractStatus.Approved,
        RoomStatus roomStatus = RoomStatus.Rented,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        await fixture.ResetAsync();
        var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, Manager, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwner, Roles.ChuTro);

        var property = new Property { Name = "P4 deposit lifecycle", OwnerId = Owner };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var room = new Room
        {
            PropertyId = property.Id,
            Code = "P4-DEP-201",
            StandardPrice = 1_000_000m,
            Status = roomStatus,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = property.Id,
            ManagerUserId = Manager,
            AssignedByUserId = Owner,
        });

        var contract = new Contract
        {
            ContractNumber = "HD-P4-DEP-201",
            RoomId = room.Id,
            StartDate = startDate ?? new DateTime(2026, 1, 1),
            EndDate = endDate ?? new DateTime(2027, 1, 1),
            SnapshotPrice = 1_000_000m,
            DepositAmount = 200_000m,
            Status = status,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        return new SeededContract(db, property, room, contract);
    }

    private static (ContractService Contracts, ManagerApprovalService Approvals) NewLifecycleServices(
        ApplicationDbContext db, RoomChangeDecision decision)
    {
        var clock = new FixedClock();
        var approvals = NewApprovals(db);
        return (new ContractService(db, clock, new FixedTogglePolicy(decision), approvals), approvals);
    }

    private static ManagerApprovalService NewApprovals(ApplicationDbContext db) =>
        new(db,
            new FixedClock(),
            [new ContractApprovalApplier(new FixedClock()), new ContractDepositApprovalApplier(new FixedClock())],
            NullLogger<ManagerApprovalService>.Instance);

    /// <summary>Duyệt bằng context mới, đúng như mỗi request scoped thật trong production.</summary>
    private async Task<ApprovalResult> ApproveAsync(Guid requestId)
    {
        await using var db = fixture.CreateContext();
        return await NewApprovals(db).ApproveAsync(requestId, OwnerApprover);
    }

    private static Task<decimal> DepositAsync(ApplicationDbContext db, Guid contractId) =>
        db.Contracts.AsNoTracking()
            .Where(contract => contract.ContractId == contractId)
            .Select(contract => contract.DepositAmount)
            .SingleAsync();

    private sealed class FixedTogglePolicy(RoomChangeDecision decision) : IManagerTogglePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(
            ManagerAction action, string role, CancellationToken ct = default) =>
            Task.FromResult(decision);

        public Task<RoomChangeDecision> EvaluateAsync(
            string code, string role, CancellationToken ct = default) =>
            Task.FromResult(decision);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
