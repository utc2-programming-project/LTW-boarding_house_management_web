using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.DTOs;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Services;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Tenants.Entities;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Contracts;

public sealed class ContractServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    [SqlServerFact]
    public async Task CreateContractAsync_RejectsRoomThatIsNotAvailable()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Rented);
        await using var dbContext = db;
        var service = new ContractService(dbContext, null!);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateContractAsync(
            Request(roomId, tenantIds: [1])));

        Assert.Equal("RoomId", exception.ParamName);
    }

    [SqlServerFact]
    public async Task CreateContractAsync_RejectsTenantIdThatDoesNotExist()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var service = new ContractService(dbContext, null!);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateContractAsync(
            Request(roomId, tenantIds: [999])));

        Assert.Equal("TenantIds", exception.ParamName);
    }

    [SqlServerFact]
    public async Task CreateContractAsync_PersistsContractAndMembersWithPriceSnapshots()
    {
        await ResetDatabaseAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var tenant = await AddTenantAsync(dbContext, "Tenant One", "0900000001");
        var service = new ContractService(dbContext, null!);

        var response = await service.CreateContractAsync(Request(roomId, [tenant.Id]));

        Assert.NotEqual(Guid.Empty, response.ContractId);
        Assert.StartsWith("HD-", response.ContractNumber);
        Assert.Equal(roomId, response.RoomId);
        Assert.Equal(new DateTime(2026, 10, 22), response.StartDate);
        Assert.Equal(new DateTime(2026, 11, 22), response.EndDate);
        Assert.Equal(1_000_000m, response.SnapshotPrice);
        Assert.Equal(200_000m, response.DepositAmount);
        Assert.Equal(ContractStatus.Pending, response.Status);

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
        var service = new ContractService(dbContext, null!);

        try
        {
            await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateContractAsync(Request(roomId, [tenant.Id])));
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

    private async Task<(ApplicationDbContext Db, int RoomId)> CreateDatabaseWithRoomAsync(RoomStatus roomStatus)
    {
        var db = fixture.CreateContext();
        const string ownerId = "contract-test-owner";
        await SqlServerFixture.SeedAccountAsync(db, ownerId, Roles.ChuTro);

        var property = new Property { Name = "Contract test property", OwnerId = ownerId };
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

    private static CreateContractRequest Request(int roomId, List<int> tenantIds) => new(
        roomId,
        new DateTime(2026, 10, 22),
        new DateTime(2026, 11, 22),
        tenantIds);
}
