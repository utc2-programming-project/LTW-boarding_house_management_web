using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Properties.Entities;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Rooms.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;

namespace NhaTro.IntegrationTests.Features.Tenants;

/// <summary>
/// VHT-25 (2B): liên kết user -> hồ sơ -> hợp đồng, truy vấn phòng đang thuê từ hợp đồng thật
/// (không suy từ Room.Status), tách khách chưa thuê và khách có nhiều hợp đồng lịch sử.
/// Chạy trên SQL Server LocalDB thật như các integration test P3 hiện có.
/// </summary>
public sealed class TenantRentalLinkIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly TenantCaller Owner = new("rental-owner", Roles.ChuTro);
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    private TenantService NewTenantService(TimeProvider? clock = null) =>
        new(fixture, clock ?? TimeProvider.System, new AllowAllTogglePolicy());

    [SqlServerFact]
    public async Task Ho_so_chua_thue_tra_ket_qua_rong_ro_rang()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "rental-tenant", Roles.KhachThue);

        var created = await NewTenantService().CreateAsync(
            NewTenant("An", "0901111111"), Owner);
        var id = created.Tenant!.Id;

        var linked = await NewTenantService().LinkAccountAsync(
            id, "rental-tenant", created.Tenant.RowVersion, Owner);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        var asManager = await NewTenantService().GetRentalOverviewAsync(id, Owner);
        Assert.True(asManager.Succeeded, asManager.ErrorMessage);
        Assert.Empty(asManager.Rental!.Rentals);
        Assert.Null(asManager.Rental.CurrentRental);
        Assert.False(asManager.Rental.HasActiveContract);
        Assert.Equal(id, asManager.Rental.TenantId);

        var asTenant = await NewTenantService().GetOwnRentalOverviewAsync(
            new TenantCaller("rental-tenant", Roles.KhachThue));
        Assert.True(asTenant.Succeeded, asTenant.ErrorMessage);
        Assert.Empty(asTenant.Rental!.Rentals);
        Assert.Null(asTenant.Rental.CurrentRental);
    }

    [SqlServerFact]
    public async Task Chi_tra_dung_hop_dong_cua_khach_va_tach_hien_tai_voi_lich_su()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "tenant-a", Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, "tenant-b", Roles.KhachThue);

        var service = NewTenantService();
        var tenantA = (await service.CreateAsync(NewTenant("An", "0901111111"), Owner)).Tenant!;
        var tenantB = (await service.CreateAsync(NewTenant("Bình", "0902222222"), Owner)).Tenant!;
        await service.LinkAccountAsync(tenantA.Id, "tenant-a", tenantA.RowVersion, Owner);
        await service.LinkAccountAsync(tenantB.Id, "tenant-b", tenantB.RowVersion, Owner);

        var (property, rooms) = await SeedPropertyWithRoomsAsync(db, "Nhà A", "room-a", "room-a-old", "room-a-future", "room-a-pending");
        var (_, roomsB) = await SeedPropertyWithRoomsAsync(db, "Nhà B", "room-b");

        // A: một hợp đồng đang hiệu lực, một lịch sử đã kết thúc, một tương lai, một chờ duyệt.
        await AddContractAsync(db, tenantA.Id, rooms[0].Id, ContractStatus.Approved,
            Today.AddDays(-10), Today.AddDays(10), "HD-A-CURRENT");
        await AddContractAsync(db, tenantA.Id, rooms[1].Id, ContractStatus.Approved,
            Today.AddDays(-60), Today.AddDays(-1), "HD-A-OLD");
        await AddContractAsync(db, tenantA.Id, rooms[2].Id, ContractStatus.Approved,
            Today.AddDays(1), Today.AddDays(30), "HD-A-FUTURE");
        await AddContractAsync(db, tenantA.Id, rooms[3].Id, ContractStatus.Pending,
            Today.AddDays(-1), Today.AddDays(20), "HD-A-PENDING");

        // B: hợp đồng đang hiệu lực khác phòng; không được lẫn sang kết quả của A.
        await AddContractAsync(db, tenantB.Id, roomsB[0].Id, ContractStatus.Approved,
            Today.AddDays(-5), Today.AddDays(5), "HD-B-CURRENT");

        var ownA = await service.GetOwnRentalOverviewAsync(
            new TenantCaller("tenant-a", Roles.KhachThue));

        Assert.True(ownA.Succeeded, ownA.ErrorMessage);
        Assert.Equal(tenantA.Id, ownA.Rental!.TenantId);
        Assert.Equal(4, ownA.Rental.Rentals.Count);
        Assert.DoesNotContain(ownA.Rental.Rentals, rental => rental.ContractNumber == "HD-B-CURRENT");
        Assert.Equal("HD-A-CURRENT", ownA.Rental.CurrentRental!.ContractNumber);
        Assert.Equal("room-a", ownA.Rental.CurrentRental.RoomCode);
        Assert.Equal(property.Name, ownA.Rental.CurrentRental.PropertyName);
        Assert.Equal(property.Address, ownA.Rental.CurrentRental.PropertyAddress);
        Assert.Equal(23.45m, ownA.Rental.CurrentRental.AreaM2);
        Assert.Contains(
            ownA.Rental.CurrentRental.Roommates,
            roommate => roommate.TenantId == tenantA.Id && roommate.FullName == tenantA.FullName);
        Assert.Single(ownA.Rental.Rentals, rental => rental.IsCurrent);
        Assert.Contains(ownA.Rental.Rentals, rental => rental.ContractNumber == "HD-A-OLD" && !rental.IsCurrent);
        Assert.Contains(ownA.Rental.Rentals, rental => rental.ContractNumber == "HD-A-FUTURE" && !rental.IsCurrent);
        Assert.Contains(ownA.Rental.Rentals, rental => rental.ContractNumber == "HD-A-PENDING" && !rental.IsCurrent);

        var ownB = await service.GetOwnRentalOverviewAsync(
            new TenantCaller("tenant-b", Roles.KhachThue));
        Assert.True(ownB.Succeeded, ownB.ErrorMessage);
        Assert.Equal("HD-B-CURRENT", ownB.Rental!.CurrentRental!.ContractNumber);
        Assert.DoesNotContain(ownB.Rental.Rentals, rental => rental.ContractNumber.StartsWith("HD-A-"));
    }

    [SqlServerFact]
    public async Task Khong_xem_duoc_phong_cua_khach_khac()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "tenant-a", Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, "tenant-b", Roles.KhachThue);

        var service = NewTenantService();
        var tenantA = (await service.CreateAsync(NewTenant("An", "0901111111"), Owner)).Tenant!;
        var tenantB = (await service.CreateAsync(NewTenant("Bình", "0902222222"), Owner)).Tenant!;
        await service.LinkAccountAsync(tenantA.Id, "tenant-a", tenantA.RowVersion, Owner);
        await service.LinkAccountAsync(tenantB.Id, "tenant-b", tenantB.RowVersion, Owner);

        var (_, rooms) = await SeedPropertyWithRoomsAsync(db, "Nhà B", "room-b");
        await AddContractAsync(db, tenantB.Id, rooms[0].Id, ContractStatus.Approved,
            Today.AddDays(-2), Today.AddDays(2), "HD-B-CURRENT");

        var callerA = new TenantCaller("tenant-a", Roles.KhachThue);
        var others = await service.GetRentalOverviewAsync(tenantB.Id, callerA);

        Assert.False(others.Succeeded);
        Assert.Equal(TenantErrorCodes.TenantNotFound, others.ErrorCode);
        Assert.Null(others.Rental);

        // Tài khoản khách chưa gắn hồ sơ: không có dữ liệu, không rơi sang hồ sơ khác.
        await SqlServerFixture.SeedAccountAsync(db, "unlinked", Roles.KhachThue);
        var unlinked = await service.GetOwnRentalOverviewAsync(
            new TenantCaller("unlinked", Roles.KhachThue));
        Assert.Equal(TenantErrorCodes.TenantNotFound, unlinked.ErrorCode);

        // Admin không gọi nghiệp vụ khách thuê.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.GetRentalOverviewAsync(tenantB.Id, new TenantCaller("admin", Roles.Admin)));
    }

    private static async Task<(Property Property, List<Room> Rooms)> SeedPropertyWithRoomsAsync(
        ApplicationDbContext db, string propertyName, params string[] roomCodes)
    {
        var property = new Property
        {
            Name = propertyName,
            Address = $"Địa chỉ {propertyName}",
            OwnerId = Owner.UserId,
        };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var rooms = roomCodes.Select((code, index) => new Room
        {
            PropertyId = property.Id,
            Code = code,
            AreaM2 = 23.45m,
            StandardPrice = 1_000_000m + index,
            Status = RoomStatus.Available,
        }).ToList();
        db.Rooms.AddRange(rooms);
        await db.SaveChangesAsync();

        return (property, rooms);
    }

    private static async Task AddContractAsync(
        ApplicationDbContext db,
        int tenantId,
        int roomId,
        ContractStatus status,
        DateTime startDate,
        DateTime endDate,
        string contractNumber)
    {
        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = contractNumber,
            RoomId = roomId,
            StartDate = startDate,
            EndDate = endDate,
            SnapshotPrice = 1_000_000m,
            DepositAmount = 200_000m,
            Status = status,
        };
        db.Contracts.Add(contract);
        db.ContractMembers.Add(new ContractMember
        {
            ContractId = contract.ContractId,
            TenantId = tenantId,
        });
        await db.SaveChangesAsync();
    }

    private static CreateTenantRequest NewTenant(string fullName, string phone) => new()
    {
        FullName = fullName,
        PhoneNumber = phone,
    };

    private sealed class AllowAllTogglePolicy : IManagerTogglePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(
            Web.Features.Settings.Enums.ManagerAction action, string role, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.Allowed);

        public Task<RoomChangeDecision> EvaluateAsync(
            string code, string role, CancellationToken ct = default) =>
            Task.FromResult(RoomChangeDecision.Allowed);
    }
}
