using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Data;
using Web.Data.Migrations;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Contracts;
using Microsoft.Data.SqlClient;
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
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Contracts;

public sealed class ContractServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task ListAndGetAsync_ApplyOwnerManagerTenantAndAdminScopes()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, TenantUserId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, OtherTenantUserId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwnerId, Roles.ChuTro);

        var ownedProperty = new Property { Name = "Owner property", OwnerId = OwnerId };
        var otherProperty = new Property { Name = "Other property", OwnerId = OtherOwnerId };
        db.Properties.AddRange(ownedProperty, otherProperty);
        await db.SaveChangesAsync();
        var ownedRoom = new Room { PropertyId = ownedProperty.Id, Code = "101", StandardPrice = 1_000_000m };
        var vacantOwnedRoom = new Room { PropertyId = ownedProperty.Id, Code = "102", StandardPrice = 1_000_000m };
        var otherRoom = new Room { PropertyId = otherProperty.Id, Code = "201", StandardPrice = 1_000_000m };
        db.Rooms.AddRange(ownedRoom, vacantOwnedRoom, otherRoom);
        await db.SaveChangesAsync();
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = ownedProperty.Id,
            ManagerUserId = ManagerId,
            AssignedByUserId = OwnerId,
        });
        var tenant = new Tenant { FullName = "Owned tenant", Phone = "0900000011", UserId = TenantUserId };
        var otherTenant = new Tenant { FullName = "Other tenant", Phone = "0900000022", UserId = OtherTenantUserId };
        db.Tenants.AddRange(tenant, otherTenant);
        await db.SaveChangesAsync();
        var ownedContract = TestContract(Guid.NewGuid(), ownedRoom.Id, ContractStatus.Pending);
        var otherContract = TestContract(Guid.NewGuid(), otherRoom.Id, ContractStatus.Pending);
        db.Contracts.AddRange(ownedContract, otherContract);
        db.ContractMembers.AddRange(
            new ContractMember { ContractId = ownedContract.ContractId, TenantId = tenant.Id },
            new ContractMember { ContractId = otherContract.ContractId, TenantId = otherTenant.Id });
        await db.SaveChangesAsync();
        var service = new ContractService(db);

        Assert.Equal([ownedContract.ContractId], (await service.ListAsync(null, OwnerCaller)).Select(item => item.ContractId));
        Assert.Equal([ownedContract.ContractId], (await service.ListAsync(null, new PropertyCaller(ManagerId, Roles.QuanLy))).Select(item => item.ContractId));
        Assert.Equal([ownedContract.ContractId], (await service.ListAsync(null, new PropertyCaller(TenantUserId, Roles.KhachThue))).Select(item => item.ContractId));
        Assert.Equal([otherContract.ContractId], (await service.ListAsync(null, new PropertyCaller(OtherTenantUserId, Roles.KhachThue))).Select(item => item.ContractId));

        var detail = await service.GetAsync(ownedContract.ContractId, new PropertyCaller(TenantUserId, Roles.KhachThue));
        Assert.Equal("Owner property", detail.PropertyName);
        await Assert.ThrowsAsync<ContractNotFoundException>(() => service.GetAsync(
            otherContract.ContractId, new PropertyCaller(TenantUserId, Roles.KhachThue)));
        var adminCaller = new PropertyCaller("admin", Roles.Admin);
        await Assert.ThrowsAsync<ContractAuthorizationException>(() => service.ListAsync(null, adminCaller));
        await Assert.ThrowsAsync<ContractAuthorizationException>(() => service.GetAsync(ownedContract.ContractId, adminCaller));
        await Assert.ThrowsAsync<ContractAuthorizationException>(() => service.GetCreateOptionsAsync(adminCaller));

        await Assert.ThrowsAsync<ContractNotFoundException>(() => service.CreateContractAsync(
            Request(otherRoom.Id, [tenant.Id]), new PropertyCaller(ManagerId, Roles.QuanLy)));
        var managerCreate = await service.CreateContractAsync(
            Request(vacantOwnedRoom.Id, [tenant.Id]), new PropertyCaller(ManagerId, Roles.QuanLy));
        Assert.Equal(ContractStatus.Pending, managerCreate.Status);
        var managerOptions = await service.GetCreateOptionsAsync(new PropertyCaller(ManagerId, Roles.QuanLy));
        Assert.Equal(ownedProperty.Id, Assert.Single(managerOptions.Properties).Id);
        Assert.DoesNotContain(managerOptions.Rooms, room => room.PropertyId == otherProperty.Id);
    }

    [SqlServerFact]
    public async Task CreateContractAsync_RejectsRoomThatIsNotAvailable()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractNotFoundException>(() => service.CreateContractAsync(
            Request(roomId, tenantIds: [1]), OwnerCaller));

        Assert.Equal(ContractErrorCodes.RoomNotAvailable, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task CreateContractAsync_RejectsTenantIdThatDoesNotExist()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractValidationException>(() => service.CreateContractAsync(
            Request(roomId, tenantIds: [999]), OwnerCaller));

        Assert.Equal("TenantIds", exception.ParamName);
    }

    [SqlServerFact]
    public async Task CreateContractAsync_PersistsContractAndMembersWithPriceSnapshots()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var tenant = await AddTenantAsync(dbContext, "Tenant One", "0900000001");
        var service = new ContractService(dbContext);

        var response = await service.CreateContractAsync(Request(roomId, [tenant.Id]), OwnerCaller);

        Assert.NotEqual(Guid.Empty, response.ContractId);
        Assert.StartsWith("HD-", response.ContractNumber);
        Assert.Equal(roomId, response.RoomId);
        Assert.Equal(new DateTime(2026, 10, 22), response.StartDate);
        Assert.Equal(new DateTime(2026, 11, 22), response.EndDate);
        Assert.Equal(1_000_000m, response.SnapshotPrice);
        Assert.Equal(200_000m, response.DepositAmount);
        // D4: hợp đồng do Chủ trọ tạo có hiệu lực ngay.
        Assert.Equal(ContractStatus.Approved, response.Status);

        await using var reloadedDb = fixture.CreateContext();
        var savedContract = await reloadedDb.Contracts.SingleAsync(
            contract => contract.ContractId == response.ContractId);
        var savedMember = await reloadedDb.ContractMembers.SingleAsync(
            member => member.ContractId == response.ContractId);

        Assert.Equal(response.SnapshotPrice, savedContract.SnapshotPrice);
        Assert.Equal(response.DepositAmount, savedContract.DepositAmount);
        Assert.Equal(tenant.Id, savedMember.TenantId);
    }

    [SqlServerFact]
    public async Task CreateContractAsync_RollsBackContractWhenMemberInsertFails()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var tenant = await AddTenantAsync(dbContext, "Tenant Two", "0900000002");
        await dbContext.Database.ExecuteSqlRawAsync("""
            ALTER TABLE [ContractMembers]
            ADD CONSTRAINT [CK_Test_ContractMembers_Reject] CHECK ([TenantId] < 0);
            """);
        var service = new ContractService(dbContext);

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateContractAsync(Request(roomId, [tenant.Id]), OwnerCaller));
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE [ContractMembers] DROP CONSTRAINT [CK_Test_ContractMembers_Reject];
                """);
        }

        await using var reloadedDb = fixture.CreateContext();
        Assert.Empty(await reloadedDb.Contracts.ToListAsync());
        Assert.Empty(await reloadedDb.ContractMembers.ToListAsync());
    }

    [SqlServerFact]
    public async Task CreateContractAsync_RejectsRoomOwnedByAnotherOwner()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        await SqlServerFixture.SeedAccountAsync(dbContext, OtherOwnerId, Roles.ChuTro);
        var tenant = await AddTenantAsync(dbContext, "Tenant Other Owner", "0900000099");
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractNotFoundException>(() => service.CreateContractAsync(
            Request(roomId, [tenant.Id]),
            new PropertyCaller(OtherOwnerId, Roles.ChuTro)));

        Assert.Equal(ContractErrorCodes.RoomNotAvailable, exception.ErrorCode);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Empty(await reloadedDb.Contracts.ToListAsync());
    }

    [SqlServerFact]
    public async Task CreateContractAsync_RejectsDuplicatePendingContractForSameRoomAndPeriod()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var tenant = await AddTenantAsync(dbContext, "Tenant Duplicate", "0900000098");
        await AssignManagerAsync(dbContext, roomId);
        var manager = new PropertyCaller(ManagerId, Roles.QuanLy);
        var service = new ContractService(dbContext);
        var request = Request(roomId, [tenant.Id]);

        await service.CreateContractAsync(request, manager);
        var exception = await Assert.ThrowsAsync<ContractOperationException>(() =>
            service.CreateContractAsync(request, manager));

        Assert.Equal(ContractErrorCodes.PendingContractExists, exception.ErrorCode);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Single(await reloadedDb.Contracts.ToListAsync());
    }

    [SqlServerFact]
    public async Task CreateContractAsync_AllowsNonOverlappingPendingContracts()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var tenant = await AddTenantAsync(dbContext, "Tenant Sequential", "0900000097");
        await AssignManagerAsync(dbContext, roomId);
        var manager = new PropertyCaller(ManagerId, Roles.QuanLy);
        var service = new ContractService(dbContext, new FixedTimeProvider(
            new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero)));

        await service.CreateContractAsync(Request(roomId, [tenant.Id]), manager);
        await service.CreateContractAsync(
            new CreateContractRequest(
                roomId,
                new DateTime(2026, 11, 23),
                new DateTime(2026, 12, 23),
                [tenant.Id]),
            manager);

        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(2, await reloadedDb.Contracts.CountAsync());
    }

    [SqlServerFact]
    public async Task CreateContractAsync_ConcurrentDuplicatesOnlyPersistOne()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        var tenant = await AddTenantAsync(db, "Tenant Concurrent", "0900000096");
        await AssignManagerAsync(db, roomId);
        await db.DisposeAsync();
        var request = Request(roomId, [tenant.Id]);
        var manager = new PropertyCaller(ManagerId, Roles.QuanLy);

        async Task<Exception?> TryCreateAsync()
        {
            await using var context = fixture.CreateContext();
            var service = new ContractService(context);
            try
            {
                await service.CreateContractAsync(request, manager);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var results = await Task.WhenAll(TryCreateAsync(), TryCreateAsync());

        Assert.Single(results, result => result is null);
        var losingRequest = Assert.Single(results, result => result is not null);
        var operationException = Assert.IsType<ContractOperationException>(losingRequest);
        Assert.Equal(ContractErrorCodes.PendingContractExists, operationException.ErrorCode);

        await using var reloadedDb = fixture.CreateContext();
        Assert.Single(await reloadedDb.Contracts.ToListAsync());
        Assert.Single(await reloadedDb.ContractMembers.ToListAsync());
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_ThrowsNotFoundForMissingContract()
    {
        await ResetDatabaseAsync();
        var (db, _) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var service = new ContractService(dbContext);

        await Assert.ThrowsAsync<ContractNotFoundException>(() => service.ApproveContractAsync(
            Guid.NewGuid(),
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller));
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RejectsContractThatIsNotPending()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller));
        Assert.Equal(ContractErrorCodes.InvalidStatus, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RejectionPersistsReasonAndStatus()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var response = await service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = false, RejectReason = "  Missing signature  " },
            OwnerCaller);

        Assert.Equal(ContractStatus.Rejected, response.Status);
        Assert.Equal("Missing signature", response.RejectReason);

        await using var reloadedDb = fixture.CreateContext();
        var saved = await reloadedDb.Contracts.SingleAsync(contract => contract.ContractId == contractId);
        Assert.Equal(ContractStatus.Rejected, saved.Status);
        Assert.Equal("Missing signature", saved.RejectReason);
        Assert.Equal(RoomStatus.Available,
            (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_ApprovesContractAndRentsRoom()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var response = await service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller);

        Assert.Equal(ContractStatus.Approved, response.Status);

        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Approved,
            (await reloadedDb.Contracts.SingleAsync(contract => contract.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Rented,
            (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RejectsOverlappingApprovedContract()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var approvedId = Guid.NewGuid();
        var pendingId = Guid.NewGuid();
        dbContext.Contracts.AddRange(
            TestContract(approvedId, roomId, ContractStatus.Approved),
            TestContract(pendingId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            pendingId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller));

        Assert.Equal(ContractErrorCodes.ContractOverlap, exception.ErrorCode);
        Assert.Equal(ContractErrorMessages.ContractOverlap, exception.Message);

        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Pending,
            (await reloadedDb.Contracts.SingleAsync(contract => contract.ContractId == pendingId)).Status);
        Assert.Equal(RoomStatus.Available,
            (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RejectsContractOwnedByAnotherOwner()
    {
        await ResetDatabaseAsync();
        var (db, _) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        const string otherOwnerId = "contract-test-other-owner";
        await SqlServerFixture.SeedAccountAsync(dbContext, otherOwnerId, Roles.ChuTro);
        var otherProperty = new Property { Name = "Other property", OwnerId = otherOwnerId };
        dbContext.Properties.Add(otherProperty);
        await dbContext.SaveChangesAsync();
        var otherRoom = new Room
        {
            PropertyId = otherProperty.Id,
            Code = "201",
            StandardPrice = 1_000_000m,
            Status = RoomStatus.Available,
        };
        dbContext.Rooms.Add(otherRoom);
        await dbContext.SaveChangesAsync();
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, otherRoom.Id, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractAuthorizationException>(() => service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller));

        Assert.Equal(ContractErrorCodes.Forbidden, exception.ErrorCode);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Pending,
            (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Available,
            (await reloadedDb.Rooms.SingleAsync(item => item.Id == otherRoom.Id)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RejectsContractForSoftDeletedProperty()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var room = await dbContext.Rooms.SingleAsync(item => item.Id == roomId);
        var property = await dbContext.Properties.SingleAsync(item => item.Id == room.PropertyId);
        property.IsDeleted = true;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller));

        Assert.Equal(ContractErrorCodes.PropertyUnavailable, exception.ErrorCode);
        Assert.Equal(ContractErrorMessages.PropertyUnavailable, exception.Message);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Pending,
            (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Available,
            (await reloadedDb.Rooms.SingleAsync(item => item.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_BlocksManagerForContractTheyCreated()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractAuthorizationException>(() => service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = true },
            new PropertyCaller(OwnerId, Roles.QuanLy)));

        Assert.Equal(ContractErrorCodes.Forbidden, exception.ErrorCode);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RejectsRoomUnderMaintenanceWithoutChangingIt()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Maintenance);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller));

        Assert.Equal(ContractErrorCodes.RoomUnderMaintenance, exception.ErrorCode);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Pending,
            (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Maintenance,
            (await reloadedDb.Rooms.SingleAsync(item => item.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RejectsRentedRoomWithoutChangingIt()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var exception = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            contractId, new ApproveContractRequestDto { IsApproved = true }, OwnerCaller));

        Assert.Equal(ContractErrorCodes.RoomNotVacant, exception.ErrorCode);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Pending, (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Rented, (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RejectsFutureStartWithoutRentingRoom()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        var contract = TestContract(contractId, roomId, ContractStatus.Pending);
        contract.StartDate = new DateTime(2026, 11, 2);
        contract.EndDate = new DateTime(2027, 11, 1);
        dbContext.Contracts.Add(contract);
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext, new FixedTimeProvider(
            new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            contractId, new ApproveContractRequestDto { IsApproved = true }, OwnerCaller));

        Assert.Equal(ContractErrorCodes.FutureStartNotAllowed, exception.ErrorCode);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Pending, (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Available, (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_SnapshotsCurrentRoomPriceAndDeposit()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var room = await dbContext.Rooms.SingleAsync(item => item.Id == roomId);
        room.StandardPrice = 1_500_003m;
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        var response = await service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller);

        Assert.Equal(1_500_003m, response.SnapshotPrice);
        // Tiền cọc do người tạo nhập được giữ nguyên khi duyệt, không bị ghi đè 20%.
        Assert.Equal(200_000m, response.DepositAmount);
        await using var reloadedDb = fixture.CreateContext();
        var saved = await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId);
        Assert.Equal(response.SnapshotPrice, saved.SnapshotPrice);
        Assert.Equal(response.DepositAmount, saved.DepositAmount);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_KeepsSnapshotWhenRoomPriceChangesAfterApproval()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        await service.ApproveContractAsync(
            contractId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller);

        var room = await dbContext.Rooms.SingleAsync(item => item.Id == roomId);
        room.StandardPrice = 2_000_000m;
        await dbContext.SaveChangesAsync();

        await using var reloadedDb = fixture.CreateContext();
        var saved = await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId);
        Assert.Equal(1_000_000m, saved.SnapshotPrice);
        Assert.Equal(200_000m, saved.DepositAmount);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_DuplicateApprovalAndRejectionAreBlocked()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var approvedId = Guid.NewGuid();
        var rejectedId = Guid.NewGuid();
        dbContext.Contracts.AddRange(
            TestContract(approvedId, roomId, ContractStatus.Pending),
            TestContract(rejectedId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext);

        await service.ApproveContractAsync(approvedId, new ApproveContractRequestDto { IsApproved = true }, OwnerCaller);
        var duplicateApproval = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            approvedId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller));
        Assert.Equal(ContractErrorCodes.InvalidStatus, duplicateApproval.ErrorCode);

        await service.ApproveContractAsync(
            rejectedId,
            new ApproveContractRequestDto { IsApproved = false, RejectReason = "No longer needed" },
            OwnerCaller);
        var duplicateRejection = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            rejectedId,
            new ApproveContractRequestDto { IsApproved = false, RejectReason = "Repeated" },
            OwnerCaller));
        Assert.Equal(ContractErrorCodes.InvalidStatus, duplicateRejection.ErrorCode);
        var approvalAfterRejection = await Assert.ThrowsAsync<ContractOperationException>(() => service.ApproveContractAsync(
            rejectedId,
            new ApproveContractRequestDto { IsApproved = true },
            OwnerCaller));
        Assert.Equal(ContractErrorCodes.InvalidStatus, approvalAfterRejection.ErrorCode);
    }

    [SqlServerFact]
    public async Task ContractsTable_RejectsOverlappingApprovedContractOutsideService()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var existingId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        dbContext.Contracts.AddRange(
            TestContract(existingId, roomId, ContractStatus.Approved),
            TestContract(candidateId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(ContractOverlapTriggerSql.CreateOrAlter);

            // Ghi trực tiếp để kiểm tra trigger, không đi qua bước chặn trùng của service.
            var exception = await Assert.ThrowsAsync<SqlException>(() =>
                dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE [Contracts] SET [Status] = {(int)ContractStatus.Approved} WHERE [ContractId] = {candidateId}"));

            Assert.Equal(51030, exception.Number);
            Assert.Contains("ContractOverlap", exception.Message);
            await using var reloadedDb = fixture.CreateContext();
            Assert.Equal(ContractStatus.Pending,
                (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == candidateId)).Status);
            Assert.Single(await reloadedDb.Contracts
                .Where(item => item.RoomId == roomId && item.Status == ContractStatus.Approved)
                .ToListAsync());
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "DROP TRIGGER IF EXISTS [dbo].[TR_Contracts_PreventApprovedOverlap];");
        }
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_RollsBackContractWhenRoomUpdateFails()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        await dbContext.Database.ExecuteSqlRawAsync("""
            ALTER TABLE [Rooms]
            ADD CONSTRAINT [CK_Test_Rooms_RejectRented] CHECK ([Status] <> 1);
            """);
        var service = new ContractService(dbContext);

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => service.ApproveContractAsync(
                contractId,
                new ApproveContractRequestDto { IsApproved = true },
                OwnerCaller));
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync("""
                ALTER TABLE [Rooms] DROP CONSTRAINT [CK_Test_Rooms_RejectRented];
                """);
        }

        Assert.Equal(ContractStatus.Pending,
            (await dbContext.Contracts.SingleAsync(contract => contract.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Available,
            (await dbContext.Rooms.SingleAsync(room => room.Id == roomId)).Status);

        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Pending,
            (await reloadedDb.Contracts.SingleAsync(contract => contract.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Available,
            (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ApproveContractAsync_ConcurrentOverlappingRequestsOnlyApproveOne()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        var approvedCandidateId = Guid.NewGuid();
        var competingCandidateId = Guid.NewGuid();
        await using (db)
        {
            db.Contracts.AddRange(
                TestContract(approvedCandidateId, roomId, ContractStatus.Pending),
                TestContract(competingCandidateId, roomId, ContractStatus.Pending));
            await db.SaveChangesAsync();
        }

        async Task<Exception?> TryApproveAsync(Guid id)
        {
            await using var context = fixture.CreateContext();
            var service = new ContractService(context);
            try
            {
                await service.ApproveContractAsync(
                    id,
                    new ApproveContractRequestDto { IsApproved = true },
                    OwnerCaller);
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        var results = await Task.WhenAll(
            TryApproveAsync(approvedCandidateId),
            TryApproveAsync(competingCandidateId));

        Assert.Single(results, result => result is null);
        var losingRequest = Assert.Single(results, result => result is not null);
        var operationException = Assert.IsType<ContractOperationException>(losingRequest);
        Assert.Equal(ContractErrorCodes.ContractOverlap, operationException.ErrorCode);
        Assert.Equal(ContractErrorMessages.ContractOverlap, operationException.Message);

        await using var reloadedDb = fixture.CreateContext();
        Assert.Single(await reloadedDb.Contracts
            .Where(contract => contract.RoomId == roomId && contract.Status == ContractStatus.Approved)
            .ToListAsync());
        Assert.Equal(RoomStatus.Rented,
            (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task GetAsync_ExposesLifecycleActionsForEligibleOwnerContract()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext, LifecycleClock);

        var detail = await service.GetAsync(contractId, OwnerCaller);

        Assert.True(detail.CanRenew);
        Assert.True(detail.CanLiquidate);
    }

    [SqlServerFact]
    public async Task GetAsync_HidesLifecycleActionsForPendingContract()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Pending));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext, LifecycleClock);

        var detail = await service.GetAsync(contractId, OwnerCaller);

        Assert.False(detail.CanRenew);
        Assert.False(detail.CanLiquidate);
    }

    [SqlServerFact]
    public async Task GetAsync_HidesLifecycleActionsWhenSw07DeniesManager()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        await AssignManagerAsync(dbContext, roomId);
        var service = new ContractService(dbContext, LifecycleClock, new FixedTogglePolicy(RoomChangeDecision.Denied));

        var detail = await service.GetAsync(contractId, new PropertyCaller(ManagerId, Roles.QuanLy));

        Assert.False(detail.CanRenew);
        Assert.False(detail.CanLiquidate);
    }

    [SqlServerFact]
    public async Task RenewContractAsync_CreatesApprovedSuccessorAndKeepsHistory()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var tenant = await AddTenantAsync(dbContext, "Tenant Renew", "0900000101");
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        dbContext.ContractMembers.Add(new ContractMember { ContractId = contractId, TenantId = tenant.Id });
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext, LifecycleClock);

        var result = await service.RenewContractAsync(
            contractId,
            new RenewContractRequestDto { EndDate = new DateTime(2028, 10, 21) },
            OwnerCaller);

        Assert.Equal(ContractActionOutcome.Applied, result.Outcome);
        Assert.NotNull(result.ContractId);
        Assert.Null(result.ApprovalRequestId);

        await using var reloadedDb = fixture.CreateContext();
        var renewal = await reloadedDb.Contracts.SingleAsync(item => item.ContractId == result.ContractId);
        Assert.Equal(ContractStatus.Approved, renewal.Status);
        Assert.Equal(new DateTime(2027, 10, 22), renewal.StartDate);
        Assert.Equal(new DateTime(2028, 10, 21), renewal.EndDate);
        Assert.Equal(1_000_000m, renewal.SnapshotPrice);
        Assert.Equal(200_000m, renewal.DepositAmount);
        Assert.Equal(tenant.Id, (await reloadedDb.ContractMembers
            .SingleAsync(member => member.ContractId == renewal.ContractId)).TenantId);
        Assert.Equal(ContractStatus.Approved,
            (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Rented,
            (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task RenewContractAsync_RejectsEndDateNotAfterCurrentEndDate()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext, LifecycleClock);

        var exception = await Assert.ThrowsAsync<ContractValidationException>(() => service.RenewContractAsync(
            contractId,
            new RenewContractRequestDto { EndDate = new DateTime(2027, 10, 21) },
            OwnerCaller));

        Assert.Equal(ContractErrorCodes.InvalidRenewalEndDate, exception.ErrorCode);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Single(await reloadedDb.Contracts.ToListAsync());
    }

    [SqlServerFact]
    public async Task LiquidateContractAsync_MarksLiquidatedAndFreesRoom()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        var service = new ContractService(dbContext, LifecycleClock);

        var result = await service.LiquidateContractAsync(contractId, OwnerCaller);

        Assert.Equal(ContractActionOutcome.Applied, result.Outcome);
        Assert.Equal(contractId, result.ContractId);

        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Liquidated,
            (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId)).Status);
        Assert.Equal(RoomStatus.Available,
            (await reloadedDb.Rooms.SingleAsync(room => room.Id == roomId)).Status);
    }

    [SqlServerFact]
    public async Task ManagerRenewal_RequiresOwnerApprovalThenCreatesSuccessor()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var tenant = await AddTenantAsync(dbContext, "Tenant Manager Renew", "0900000102");
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        dbContext.ContractMembers.Add(new ContractMember { ContractId = contractId, TenantId = tenant.Id });
        await dbContext.SaveChangesAsync();
        await AssignManagerAsync(dbContext, roomId);

        var approvals = new ManagerApprovalService(
            dbContext,
            LifecycleClock,
            [new ContractApprovalApplier(LifecycleClock)],
            NullLogger<ManagerApprovalService>.Instance);
        var service = new ContractService(
            dbContext,
            LifecycleClock,
            new FixedTogglePolicy(RoomChangeDecision.RequiresApproval),
            approvals);

        var result = await service.RenewContractAsync(
            contractId,
            new RenewContractRequestDto { EndDate = new DateTime(2028, 10, 21) },
            new PropertyCaller(ManagerId, Roles.QuanLy));

        Assert.Equal(ContractActionOutcome.PendingApproval, result.Outcome);
        Assert.NotNull(result.ApprovalRequestId);
        await using (var beforeApprovalDb = fixture.CreateContext())
        {
            Assert.Single(await beforeApprovalDb.Contracts.ToListAsync());
            var pending = await beforeApprovalDb.ManagerApprovalRequests.SingleAsync();
            Assert.Equal("SW-07", pending.OperationCode);
            Assert.Equal(ApprovalTargetType.Contract, pending.TargetType);
            Assert.Equal(contractId.ToString("D"), pending.TargetKey);
        }

        var decision = await approvals.ApproveAsync(
            result.ApprovalRequestId!.Value,
            new ApprovalCaller(OwnerId, Roles.ChuTro));

        Assert.True(decision.Succeeded);
        await using var afterApprovalDb = fixture.CreateContext();
        var contracts = await afterApprovalDb.Contracts.OrderBy(item => item.StartDate).ToListAsync();
        Assert.Equal(2, contracts.Count);
        var renewal = contracts[^1];
        Assert.Equal(ContractStatus.Approved, renewal.Status);
        Assert.Equal(new DateTime(2027, 10, 22), renewal.StartDate);
        Assert.Equal(new DateTime(2028, 10, 21), renewal.EndDate);
        Assert.Equal(tenant.Id, (await afterApprovalDb.ContractMembers
            .SingleAsync(member => member.ContractId == renewal.ContractId)).TenantId);
    }

    [SqlServerFact]
    public async Task ManagerLiquidation_RequiresOwnerApprovalThenFreesRoom()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        await AssignManagerAsync(dbContext, roomId);

        var approvals = new ManagerApprovalService(
            dbContext,
            LifecycleClock,
            [new ContractApprovalApplier(LifecycleClock)],
            NullLogger<ManagerApprovalService>.Instance);
        var service = new ContractService(
            dbContext,
            LifecycleClock,
            new FixedTogglePolicy(RoomChangeDecision.RequiresApproval),
            approvals);

        var result = await service.LiquidateContractAsync(
            contractId,
            new PropertyCaller(ManagerId, Roles.QuanLy));

        Assert.Equal(ContractActionOutcome.PendingApproval, result.Outcome);
        Assert.NotNull(result.ApprovalRequestId);
        await using (var beforeApprovalDb = fixture.CreateContext())
        {
            Assert.Equal(ContractStatus.Approved,
                (await beforeApprovalDb.Contracts.SingleAsync()).Status);
            Assert.Equal(RoomStatus.Rented,
                (await beforeApprovalDb.Rooms.SingleAsync()).Status);
        }

        var decision = await approvals.ApproveAsync(
            result.ApprovalRequestId!.Value,
            new ApprovalCaller(OwnerId, Roles.ChuTro));

        Assert.True(decision.Succeeded);
        await using var afterApprovalDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Liquidated,
            (await afterApprovalDb.Contracts.SingleAsync()).Status);
        Assert.Equal(RoomStatus.Available,
            (await afterApprovalDb.Rooms.SingleAsync()).Status);
    }

    [SqlServerFact]
    public async Task ManagerRenewal_IsBlockedWhenSw07Denies()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var contractId = Guid.NewGuid();
        dbContext.Contracts.Add(TestContract(contractId, roomId, ContractStatus.Approved));
        await dbContext.SaveChangesAsync();
        await AssignManagerAsync(dbContext, roomId);
        var service = new ContractService(
            dbContext, LifecycleClock, new FixedTogglePolicy(RoomChangeDecision.Denied));

        var exception = await Assert.ThrowsAsync<ContractOperationException>(() => service.LiquidateContractAsync(
            contractId,
            new PropertyCaller(ManagerId, Roles.QuanLy)));

        Assert.Equal(ContractErrorCodes.ApprovalDenied, exception.ErrorCode);
        await using var reloadedDb = fixture.CreateContext();
        Assert.Equal(ContractStatus.Approved,
            (await reloadedDb.Contracts.SingleAsync(item => item.ContractId == contractId)).Status);
        Assert.Empty(await reloadedDb.ManagerApprovalRequests.ToListAsync());
    }

    private async Task<(ApplicationDbContext Db, int RoomId)> CreateDatabaseWithRoomAsync(RoomStatus roomStatus)
    {
        var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);

        var property = new Property { Name = "Contract test property", OwnerId = OwnerId };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var room = new Room
        {
            PropertyId = property.Id,
            Code = "101",
            StandardPrice = 1_000_000m,
            Status = roomStatus,
        };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        return (db, room.Id);
    }

    private async Task ResetDatabaseAsync()
    {
        await using (var db = fixture.CreateContext())
        {
            await db.ContractMembers.ExecuteDeleteAsync();
            await db.Contracts.ExecuteDeleteAsync();
        }

        await fixture.ResetAsync();
    }

    private static async Task<Tenant> AddTenantAsync(ApplicationDbContext db, string fullName, string phone)
    {
        var tenant = new Tenant { FullName = fullName, Phone = phone };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    private static Contract TestContract(Guid contractId, int roomId, ContractStatus status) => new()
    {
        ContractId = contractId,
        ContractNumber = $"HD-{contractId:N}",
        RoomId = roomId,
        StartDate = new DateTime(2026, 10, 22),
        EndDate = new DateTime(2027, 10, 21),
        SnapshotPrice = 1_000_000m,
        DepositAmount = 200_000m,
        Status = status,
    };

    private static CreateContractRequest Request(int roomId, List<int> tenantIds) => new(
        roomId,
        new DateTime(2026, 10, 22),
        new DateTime(2026, 11, 22),
        tenantIds);

    private static async Task AssignManagerAsync(ApplicationDbContext db, int roomId)
    {
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        var propertyId = await db.Rooms
            .Where(room => room.Id == roomId)
            .Select(room => room.PropertyId)
            .SingleAsync();
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = propertyId,
            ManagerUserId = ManagerId,
            AssignedByUserId = OwnerId,
        });
        await db.SaveChangesAsync();
    }

    private static TimeProvider LifecycleClock => new FixedTimeProvider(
        new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero));

    private sealed class FixedTogglePolicy(RoomChangeDecision decision) : IManagerTogglePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(
            ManagerAction action, string role, CancellationToken ct = default) =>
            Task.FromResult(decision);

        public Task<RoomChangeDecision> EvaluateAsync(
            string code, string role, CancellationToken ct = default) =>
            Task.FromResult(decision);
    }

    private const string OwnerId = "contract-test-owner";
    private const string ManagerId = "contract-test-manager";
    private const string TenantUserId = "contract-test-tenant-user";
    private const string OtherTenantUserId = "contract-test-other-tenant-user";
    private const string OtherOwnerId = "contract-test-other-owner";
    private static PropertyCaller OwnerCaller => new(OwnerId, Roles.ChuTro);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
