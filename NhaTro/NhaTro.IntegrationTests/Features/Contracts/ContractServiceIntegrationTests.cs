using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.DTOs;
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
        await fixture.ResetAsync();
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
        await fixture.ResetAsync();
        var (db, roomId) = await CreateDatabaseWithRoomAsync(RoomStatus.Available);
        await using var dbContext = db;
        var service = new ContractService(dbContext, null!);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.CreateContractAsync(
            Request(roomId, tenantIds: [999])));

        Assert.Equal("TenantIds", exception.ParamName);
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

    private static CreateContractRequest Request(int roomId, List<int> tenantIds) => new(
        roomId,
        new DateTime(2026, 10, 22),
        new DateTime(2026, 11, 22),
        tenantIds);
}
