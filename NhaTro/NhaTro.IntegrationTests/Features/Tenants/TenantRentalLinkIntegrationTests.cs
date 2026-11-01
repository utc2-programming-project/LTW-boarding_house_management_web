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

    // 2C (VHT-120): danh sách/picker lấy đúng hợp đồng đang hiệu lực từ liên kết thật,
    // khách chỉ có hợp đồng lịch sử không bị coi là đang thuê dù Room.Status là Occupied.
    [SqlServerFact]
    public async Task Danh_sach_va_picker_dung_hop_dong_dang_hieu_luc_va_loc_theo_tinh_trang_thue()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner.UserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "manager-account", Roles.QuanLy);

        var service = NewTenantService();
        var tenantA = (await service.CreateAsync(NewTenant("An", "0901111111"), Owner)).Tenant!;
        var tenantB = (await service.CreateAsync(NewTenant("Bình", "0902222222"), Owner)).Tenant!;
        var tenantC = (await service.CreateAsync(NewTenant("Cường", "0903333333"), Owner)).Tenant!;

        var (_, roomsA) = await SeedPropertyWithRoomsAsync(db, "Nhà A", "room-a-current", "room-a-old");
        var (_, roomsB) = await SeedPropertyWithRoomsAsync(db, "Nhà B", "room-b-old-1", "room-b-old-2");

        // Phòng của B đang Rented nhưng hợp đồng đã hết hạn: không được suy ra "đang thuê" từ Room.Status.
        roomsB[0].Status = RoomStatus.Rented;
        roomsB[1].Status = RoomStatus.Rented;
        await db.SaveChangesAsync();

        await AddContractAsync(db, tenantA.Id, roomsA[0].Id, ContractStatus.Approved,
            Today.AddDays(-10), Today.AddDays(10), "HD-A-CURRENT");
        await AddContractAsync(db, tenantA.Id, roomsA[1].Id, ContractStatus.Approved,
            Today.AddDays(-60), Today.AddDays(-1), "HD-A-OLD");
        await AddContractAsync(db, tenantB.Id, roomsB[0].Id, ContractStatus.Approved,
            Today.AddDays(-90), Today.AddDays(-30), "HD-B-OLD-1");
        await AddContractAsync(db, tenantB.Id, roomsB[1].Id, ContractStatus.Approved,
            Today.AddDays(-20), Today.AddDays(-1), "HD-B-OLD-2");

        var ownerList = await service.ListAsync(new TenantFilterRequest { PageSize = 100 }, Owner);
        var listA = ownerList.Items.Single(t => t.Id == tenantA.Id);
        var listB = ownerList.Items.Single(t => t.Id == tenantB.Id);
        var listC = ownerList.Items.Single(t => t.Id == tenantC.Id);
        Assert.True(listA.HasActiveContract);
        Assert.Equal("room-a-current", listA.RoomName);
        Assert.False(listB.HasActiveContract);
        Assert.Null(listB.RoomName);
        Assert.False(listC.HasActiveContract);
        Assert.Null(listC.RoomName);

        // Lọc trước phân trang/tính tổng: chỉ A đang thuê, B và C chưa thuê.
        var renting = await service.ListAsync(
            new TenantFilterRequest { HasActiveContract = true, PageSize = 100 }, Owner);
        Assert.Equal(1, renting.TotalCount);
        Assert.Equal(tenantA.Id, renting.Items.Single().Id);

        var notRenting = await service.ListAsync(
            new TenantFilterRequest { HasActiveContract = false, PageSize = 100 }, Owner);
        Assert.Equal(2, notRenting.TotalCount);
        Assert.DoesNotContain(notRenting.Items, tenant => tenant.Id == tenantA.Id);

        var picker = await service.SearchForPickerAsync(new TenantPickerRequest { Limit = 50 }, Owner);
        Assert.True(picker.Single(t => t.Id == tenantA.Id).HasActiveContract);
        Assert.False(picker.Single(t => t.Id == tenantB.Id).HasActiveContract);
        Assert.False(picker.Single(t => t.Id == tenantC.Id).HasActiveContract);

        // Quản lý vẫn thấy phòng/trạng thái thuê nhưng không thấy trạng thái tài khoản (D9).
        var managerList = await service.ListAsync(
            new TenantFilterRequest { PageSize = 100 }, new TenantCaller("manager-account", Roles.QuanLy));
        var managerA = managerList.Items.Single(t => t.Id == tenantA.Id);
        Assert.True(managerA.HasActiveContract);
        Assert.Equal("room-a-current", managerA.RoomName);
        Assert.Null(managerA.HasAccount);
    }

    // 2C (VHT-120): nhiều hợp đồng cùng hiệu lực chọn xác định theo StartDate mới nhất rồi ContractId.
    [SqlServerFact]
    public async Task Nhieu_hop_dong_cung_hieu_luc_chon_hop_dong_moi_nhat()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, Owner.UserId, Roles.ChuTro);

        var service = NewTenantService();
        var tenant = (await service.CreateAsync(NewTenant("An", "0901111111"), Owner)).Tenant!;
        var (_, rooms) = await SeedPropertyWithRoomsAsync(db, "Nhà A", "room-older", "room-newer");

        await AddContractAsync(db, tenant.Id, rooms[0].Id, ContractStatus.Approved,
            Today.AddDays(-30), Today.AddDays(30), "HD-OLDER");
        await AddContractAsync(db, tenant.Id, rooms[1].Id, ContractStatus.Approved,
            Today.AddDays(-5), Today.AddDays(25), "HD-NEWER");

        var list = await service.ListAsync(new TenantFilterRequest { PageSize = 100 }, Owner);
        var row = list.Items.Single(t => t.Id == tenant.Id);
        Assert.True(row.HasActiveContract);
        Assert.Equal("room-newer", row.RoomName);
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
