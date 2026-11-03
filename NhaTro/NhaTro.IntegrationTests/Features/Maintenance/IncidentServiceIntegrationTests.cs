using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals.Services;
using Web.Features.Approvals.Entities;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Maintenance.DTOs;
using Web.Features.Maintenance;
using Web.Features.Maintenance.Entities;
using Web.Features.Maintenance.Enums;
using Web.Features.Maintenance.Services;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants.Entities;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Maintenance;

public sealed class IncidentServiceIntegrationTests(SqlServerFixture fixture) : IClassFixture<SqlServerFixture>
{
    private const string OwnerId = "incident-owner";

    [SqlServerFact]
    public async Task Owner_CreatesReadsAndAdvancesIncidentInSql()
    {
        await fixture.ResetAsync();
        var (roomId, propertyId) = await SeedRoomAsync();
        var service = new IncidentService(fixture, TimeProvider.System, null!, null!);
        var owner = new PropertyCaller(OwnerId, Roles.ChuTro);

        var created = await service.CreateAsync(ValidRequest(roomId), owner);

        Assert.True(created.Succeeded);
        var incidentId = Assert.IsType<IncidentDetailDto>(created.Incident).Id;
        Assert.Equal(IncidentStatus.New, created.Incident.Status);
        Assert.Single(created.Incident.History);
        Assert.Single(await service.ListAsync(owner));
        Assert.Equal(incidentId, (await service.GetAsync(incidentId, owner)).Incident!.Id);

        var skipped = await service.UpdateAsync(incidentId,
            new IncidentUpdateRequest { Status = IncidentStatus.Completed }, owner);
        Assert.False(skipped.Succeeded);
        Assert.Equal(IncidentErrorCodes.InvalidTransition, skipped.ErrorCode);

        var advanced = await service.UpdateAsync(incidentId,
            new IncidentUpdateRequest
            {
                Status = IncidentStatus.InProgress,
                AssigneeName = "Kỹ thuật",
                TechnicalNotes = "Đã kiểm tra thiết bị.",
                RepairCost = 250_000m,
            }, owner);
        Assert.True(advanced.Succeeded);
        Assert.Equal(IncidentStatus.InProgress, advanced.Incident!.Status);
        Assert.Equal("Kỹ thuật", advanced.Incident.AssigneeName);
        Assert.Equal("Đã kiểm tra thiết bị.", advanced.Incident.TechnicalNotes);
        Assert.Equal(250_000m, advanced.Incident.RepairCost);
        Assert.Equal(2, advanced.Incident.History.Count);

        var fieldUpdate = await service.UpdateAsync(incidentId,
            new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, owner);
        Assert.True(fieldUpdate.Succeeded);
        Assert.Equal("Kỹ thuật", fieldUpdate.Incident!.AssigneeName);
        Assert.Equal("Đã kiểm tra thiết bị.", fieldUpdate.Incident.TechnicalNotes);
        Assert.Equal(250_000m, fieldUpdate.Incident.RepairCost);
        Assert.Equal(IncidentHistoryAction.Updated, fieldUpdate.Incident.History[^1].Action);
        Assert.Equal(3, fieldUpdate.Incident.History.Count);

        await using var db = fixture.CreateContext();
        Assert.Equal(IncidentStatus.InProgress,
            await db.MaintenanceIncidents.Where(item => item.Id == incidentId).Select(item => item.Status).SingleAsync());
        Assert.Equal(3, await db.MaintenanceIncidentHistories.CountAsync(item => item.IncidentId == incidentId));
        Assert.Equal(propertyId, advanced.Incident.PropertyId);
    }

    [SqlServerFact]
    public async Task Update_PersistsTechnicalNotesAtHistoryColumnBoundaries()
    {
        await fixture.ResetAsync();
        var (roomId, _) = await SeedRoomAsync();
        var service = new IncidentService(fixture, TimeProvider.System, null!, null!);
        var owner = new PropertyCaller(OwnerId, Roles.ChuTro);
        var created = await service.CreateAsync(ValidRequest(roomId), owner);
        var incidentId = Assert.IsType<IncidentDetailDto>(created.Incident).Id;

        foreach (var length in new[] { 1000, 1001, 2000 })
        {
            var notes = new string('x', length);
            var updated = await service.UpdateAsync(incidentId,
                new IncidentUpdateRequest { Status = IncidentStatus.New, TechnicalNotes = notes }, owner);

            Assert.True(updated.Succeeded, $"Update with {length} note characters failed: {updated.ErrorCode}");
            await using var db = fixture.CreateContext();
            var historyNote = await db.MaintenanceIncidentHistories.AsNoTracking()
                .Where(item => item.IncidentId == incidentId)
                .OrderByDescending(item => item.Id)
                .Select(item => item.Note)
                .FirstAsync();
            Assert.Equal(notes, historyNote);
        }
    }

    [SqlServerFact]
    public async Task TenantCreatesOnlyForOwnActiveContractRoomAndReceivesMaskedDetails()
    {
        await fixture.ResetAsync();
        var (activeRoomId, propertyId) = await SeedRoomAsync();
        const string activeUser = "incident-tenant-active";
        const string expiredUser = "incident-tenant-expired";
        const string pendingUser = "incident-tenant-pending";
        const string otherUser = "incident-tenant-other";
        var otherRoomId = 0;
        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, activeUser, Roles.KhachThue);
            await SqlServerFixture.SeedAccountAsync(db, expiredUser, Roles.KhachThue);
            await SqlServerFixture.SeedAccountAsync(db, pendingUser, Roles.KhachThue);
            await SqlServerFixture.SeedAccountAsync(db, otherUser, Roles.KhachThue);
            otherRoomId = await AddRoomAsync(db, propertyId);
            var today = DateTime.UtcNow.Date;
            await AddTenantContractAsync(db, activeRoomId, activeUser, ContractStatus.Approved, today.AddDays(-2), today.AddDays(2));
            await AddTenantContractAsync(db, activeRoomId, expiredUser, ContractStatus.Approved, today.AddDays(-10), today.AddDays(-1));
            await AddTenantContractAsync(db, activeRoomId, pendingUser, ContractStatus.Pending, today.AddDays(-1), today.AddDays(10));
            await AddTenantContractAsync(db, otherRoomId, otherUser, ContractStatus.Approved, today.AddDays(-1), today.AddDays(10));

            var privateIncident = NewIncident(activeRoomId, OwnerId);
            privateIncident.AssigneeName = "Technician A";
            privateIncident.TechnicalNotes = "Internal note";
            privateIncident.RepairCost = 500_000m;
            privateIncident.History.Single().Note = "History internal note";
            db.MaintenanceIncidents.Add(privateIncident);
            await db.SaveChangesAsync();
        }

        var service = new IncidentService(fixture, TimeProvider.System, null!, null!);
        var activeCaller = new PropertyCaller(activeUser, Roles.KhachThue);
        var created = await service.CreateAsync(ValidRequest(activeRoomId), activeCaller);
        Assert.True(created.Succeeded);
        Assert.Null(created.Incident!.AssigneeName);
        Assert.Null(created.Incident.TechnicalNotes);
        Assert.Null(created.Incident.RepairCost);
        Assert.All(created.Incident.History, history => Assert.Null(history.ActorUserId));

        var existingId = (await service.ListAsync(activeCaller)).Single(item => item.ReportedByUserId == OwnerId).Id;
        var detail = (await service.GetAsync(existingId, activeCaller)).Incident!;
        Assert.Null(detail.AssigneeName);
        Assert.Null(detail.TechnicalNotes);
        Assert.Null(detail.RepairCost);
        Assert.All(detail.History, history =>
        {
            Assert.Null(history.ActorUserId);
            Assert.Null(history.Note);
        });
        Assert.Equal(IncidentErrorCodes.Forbidden,
            (await service.UpdateAsync(existingId, new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, activeCaller)).ErrorCode);

        var expired = await service.CreateAsync(ValidRequest(activeRoomId), new PropertyCaller(expiredUser, Roles.KhachThue));
        var pending = await service.CreateAsync(ValidRequest(activeRoomId), new PropertyCaller(pendingUser, Roles.KhachThue));
        var otherRoom = await service.CreateAsync(ValidRequest(otherRoomId), activeCaller);
        Assert.Equal(IncidentErrorCodes.RoomNotFound, expired.ErrorCode);
        Assert.Equal(IncidentErrorCodes.RoomNotFound, pending.ErrorCode);
        Assert.Equal(IncidentErrorCodes.RoomNotFound, otherRoom.ErrorCode);
    }

    [SqlServerFact]
    public async Task ConcurrentUpdatesWithSameOriginalRowVersionReturnOneConflict()
    {
        await fixture.ResetAsync();
        var (roomId, _) = await SeedRoomAsync();
        int incidentId;
        await using (var db = fixture.CreateContext())
        {
            var incident = NewIncident(roomId, OwnerId);
            db.MaintenanceIncidents.Add(incident);
            await db.SaveChangesAsync();
            incidentId = incident.Id;
        }

        var barrierFactory = new BarrierDbContextFactory(fixture.ConnectionString, new TwoIncidentReadBarrier());
        var service = new IncidentService(barrierFactory, TimeProvider.System, null!, null!);
        var caller = new PropertyCaller(OwnerId, Roles.ChuTro);
        var results = await Task.WhenAll(
            service.UpdateAsync(incidentId, new IncidentUpdateRequest
            {
                Status = IncidentStatus.InProgress,
                TechnicalNotes = "first writer",
            }, caller),
            service.UpdateAsync(incidentId, new IncidentUpdateRequest
            {
                Status = IncidentStatus.InProgress,
                AssigneeName = "second writer",
            }, caller));

        Assert.Single(results, result => result.Succeeded);
        var conflict = Assert.Single(results, result => result.ErrorCode == IncidentErrorCodes.Conflict);
        Assert.False(conflict.Succeeded);
        await using var verify = fixture.CreateContext();
        Assert.Equal(IncidentStatus.InProgress,
            await verify.MaintenanceIncidents.Where(item => item.Id == incidentId).Select(item => item.Status).SingleAsync());
        Assert.Equal(2, await verify.MaintenanceIncidentHistories.CountAsync(item => item.IncidentId == incidentId));
    }

    [SqlServerFact]
    public async Task ManagerSeesOnlyCurrentlyAssignedPropertyAndAdminHasNoBusinessAccess()
    {
        await fixture.ResetAsync();
        var (roomId, propertyId) = await SeedRoomAsync();
        const string managerId = "incident-manager";
        const string outsiderOwnerId = "incident-other-owner";
        const string adminId = "incident-admin";
        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, managerId, Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(db, outsiderOwnerId, Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(db, adminId, Roles.Admin);
            var otherProperty = new Property { OwnerId = outsiderOwnerId, Name = "Other" };
            db.Properties.Add(otherProperty);
            await db.SaveChangesAsync();
            db.PropertyManagers.AddRange(
                new PropertyManager { PropertyId = propertyId, ManagerUserId = managerId, AssignedByUserId = OwnerId },
                new PropertyManager { PropertyId = otherProperty.Id, ManagerUserId = managerId, AssignedByUserId = outsiderOwnerId, RevokedAt = DateTime.UtcNow });
            db.MaintenanceIncidents.AddRange(
                NewIncident(roomId, OwnerId),
                NewIncident(await AddRoomAsync(db, otherProperty.Id), outsiderOwnerId));
            await db.SaveChangesAsync();
        }

        var service = new IncidentService(fixture, TimeProvider.System, null!, null!);
        var managerList = await service.ListAsync(new PropertyCaller(managerId, Roles.QuanLy));
        Assert.Single(managerList);
        Assert.Equal(propertyId, managerList[0].PropertyId);
        await using var scopeDb = fixture.CreateContext();
        var scope = new IncidentFileScope(fixture, TimeProvider.System);
        var outOfScopeId = await scopeDb.MaintenanceIncidents
            .Where(item => item.RoomId != roomId).Select(item => item.Id).SingleAsync();
        Assert.Equal(IncidentFileAccess.Manage,
            await scope.ResolveAsync(managerList[0].Id, new StorageCaller(managerId, Roles.QuanLy)));
        Assert.Equal(IncidentFileAccess.None,
            await scope.ResolveAsync(outOfScopeId, new StorageCaller(managerId, Roles.QuanLy)));
        Assert.Equal(IncidentFileAccess.None,
            await scope.ResolveAsync(managerList[0].Id, new StorageCaller(adminId, Roles.Admin)));
        Assert.Empty(await service.ListAsync(new PropertyCaller(adminId, Roles.Admin)));
        var forbidden = await service.UpdateAsync(managerList[0].Id,
            new IncidentUpdateRequest { Status = IncidentStatus.InProgress }, new PropertyCaller(adminId, Roles.Admin));
        Assert.Equal(IncidentErrorCodes.Forbidden, forbidden.ErrorCode);
    }

    [SqlServerFact]
    public async Task ManagerCreateAndStatusChangesAreAppliedOnlyWhenOwnerApprovesSw12AndSw13()
    {
        await fixture.ResetAsync();
        var (roomId, propertyId) = await SeedRoomAsync();
        const string managerId = "incident-manager-sw";
        await using (var db = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(db, managerId, Roles.QuanLy);
            db.PropertyManagers.Add(new PropertyManager
            {
                PropertyId = propertyId,
                ManagerUserId = managerId,
                AssignedByUserId = OwnerId,
            });
            await db.SaveChangesAsync();
        }

        var toggles = new ApprovalRequiredTogglePolicy();
        await using var approvalDb = fixture.CreateContext();
        var approvals = new ManagerApprovalService(approvalDb, TimeProvider.System,
            [new IncidentCreateApprovalApplier(TimeProvider.System), new IncidentStatusApprovalApplier(TimeProvider.System)],
            NullLogger<ManagerApprovalService>.Instance);
        var incidents = new IncidentService(fixture, TimeProvider.System, toggles, approvals);
        var manager = new PropertyCaller(managerId, Roles.QuanLy);
        var owner = new Web.Features.Approvals.ApprovalCaller(OwnerId, Roles.ChuTro);

        var pendingCreate = await incidents.CreateAsync(ValidRequest(roomId), manager);
        Assert.Null(pendingCreate.Incident);
        Assert.NotNull(pendingCreate.ApprovalRequest);
        var createDecision = await approvals.ApproveAsync(pendingCreate.ApprovalRequest!.Id, owner);
        Assert.True(createDecision.Succeeded);

        await using var readDb = fixture.CreateContext();
        var createdIncident = await readDb.MaintenanceIncidents.SingleAsync();
        Assert.Equal(managerId, createdIncident.ReportedByUserId);
        Assert.Equal(2, await readDb.MaintenanceIncidentHistories.CountAsync());

        var longNotes = new string('x', 2000);
        var pendingUpdate = await incidents.UpdateAsync(createdIncident.Id,
            new IncidentUpdateRequest { Status = IncidentStatus.InProgress, AssigneeName = "Nhân viên", TechnicalNotes = longNotes }, manager);
        Assert.NotNull(pendingUpdate.ApprovalRequest);
        var updateDecision = await approvals.ApproveAsync(pendingUpdate.ApprovalRequest!.Id, owner);
        Assert.True(updateDecision.Succeeded);

        await using var verifyDb = fixture.CreateContext();
        var updatedIncident = await verifyDb.MaintenanceIncidents.SingleAsync();
        Assert.Equal(IncidentStatus.InProgress, updatedIncident.Status);
        Assert.Equal("Nhân viên", updatedIncident.AssigneeName);
        var history = await verifyDb.MaintenanceIncidentHistories.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(4, history.Count);
        Assert.Equal(IncidentHistoryAction.StatusChanged, history[^2].Action);
        Assert.Equal(managerId, history[^2].ActorUserId);
        Assert.Equal(longNotes, history[^2].Note);
        Assert.Equal(IncidentHistoryAction.Approved, history[^1].Action);
        Assert.Equal(OwnerId, history[^1].ActorUserId);
        Assert.Equal(longNotes, history[^1].Note);
    }

    private async Task<(int RoomId, int PropertyId)> SeedRoomAsync()
    {
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        var property = new Property { OwnerId = OwnerId, Name = "Incident scope" };
        db.Properties.Add(property);
        await db.SaveChangesAsync();
        var room = new Room { PropertyId = property.Id, Code = "101", Status = RoomStatus.Available };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return (room.Id, property.Id);
    }

    private static async Task<int> AddRoomAsync(ApplicationDbContext db, int propertyId)
    {
        var room = new Room { PropertyId = propertyId, Code = "202", Status = RoomStatus.Available };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();
        return room.Id;
    }

    private static async Task AddTenantContractAsync(
        ApplicationDbContext db, int roomId, string tenantUserId, ContractStatus status, DateTime start, DateTime end)
    {
        var tenant = new Tenant
        {
            FullName = tenantUserId,
            Phone = $"09{Math.Abs(tenantUserId.GetHashCode()) % 100000000:D8}",
            UserId = tenantUserId,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        var contract = new Contract
        {
            ContractNumber = $"HD-INC-{Guid.NewGuid():N}",
            RoomId = roomId,
            StartDate = start,
            EndDate = end,
            SnapshotPrice = 1_000_000m,
            DepositAmount = 200_000m,
            Status = status,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember { ContractId = contract.ContractId, TenantId = tenant.Id });
        await db.SaveChangesAsync();
    }

    private static IncidentCreateRequest ValidRequest(int roomId) => new()
    {
        RoomId = roomId,
        Title = "Ổ cắm bị hỏng",
        Description = "Ổ cắm trong phòng có dấu hiệu chập điện.",
        Category = IncidentCategory.Electrical,
        Urgency = IncidentUrgency.Urgent,
    };

    private static MaintenanceIncident NewIncident(int roomId, string reporterId) => new()
    {
        RoomId = roomId,
        ReportedByUserId = reporterId,
        Title = "Máy lạnh hỏng",
        Description = "Máy lạnh không hoạt động bình thường.",
        Category = IncidentCategory.Equipment,
        Urgency = IncidentUrgency.Normal,
        CreatedAt = DateTime.UtcNow,
        History = [new MaintenanceIncidentHistory
        {
            Action = IncidentHistoryAction.Created,
            ToStatus = IncidentStatus.New,
            ActorUserId = reporterId,
            CreatedAt = DateTime.UtcNow,
        }],
    };

    private sealed class ApprovalRequiredTogglePolicy : IManagerTogglePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(ManagerAction action, string role, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.RequiresApproval);

        public Task<RoomChangeDecision> EvaluateAsync(string code, string role, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.RequiresApproval);
    }

    private sealed class BarrierDbContextFactory(string connectionString, TwoIncidentReadBarrier barrier)
        : IDbContextFactory<ApplicationDbContext>
    {
        private DbContextOptions<ApplicationDbContext> Options => new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(barrier)
            .Options;

        public ApplicationDbContext CreateDbContext() => new(Options);

        public Task<ApplicationDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed class TwoIncidentReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothQueriesRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _matchingReads;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[MaintenanceIncidents]", StringComparison.Ordinal)
                && Interlocked.Increment(ref _matchingReads) <= 2)
            {
                if (Volatile.Read(ref _matchingReads) == 2) _bothQueriesRead.TrySetResult();
                await _bothQueriesRead.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }

            return result;
        }
    }
}
