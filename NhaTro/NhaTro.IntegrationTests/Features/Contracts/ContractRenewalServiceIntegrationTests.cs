using Microsoft.EntityFrameworkCore;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Contracts;
using Web.Features.Contracts.Entities;
using Web.Features.Contracts.Enums;
using Web.Features.Contracts.Rules;
using Web.Features.Contracts.Services;
using Web.Features.Properties.Entities;
using Web.Features.Rooms.Entities;
using Web.Features.Tenants;
using Web.Features.Tenants.Entities;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Contracts;

/// <summary>
/// VHT-125 (2D): luồng "Gửi yêu cầu gia hạn phòng" của khách thuê — ghi yêu cầu thật ở trạng thái
/// Chờ xử lý, kiểm phạm vi trong service và chặn hai yêu cầu cùng chờ trên một hợp đồng.
/// </summary>
public sealed class ContractRenewalServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string OwnerUserId = "renewal-test-owner";
    private const string TenantUserId = "renewal-test-tenant";
    private const string RoommateUserId = "renewal-test-roommate";
    private const string OtherTenantUserId = "renewal-test-outsider";
    private const string ManagerUserId = "renewal-test-manager";
    private static readonly DateTimeOffset FrozenNow = new(2026, 11, 4, 3, 30, 0, TimeSpan.Zero);

    private static TenantCaller TenantCallerOf(string userId) => new(userId, Roles.KhachThue);

    [SqlServerFact]
    public async Task Gui_yeu_cau_luu_dong_cho_xu_ly_va_doc_lai_dung_dinh_dang()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);

        var contract = await SeedApprovedContractAsync(db, TenantUserId, RoommateUserId);
        var service = NewService(db);

        var submitted = await service.SubmitAsync(
            TenantCallerOf(TenantUserId), contract.ContractId, 6, "  Gia giu phong nay cho toi  ");

        Assert.True(submitted.Succeeded, submitted.ErrorMessage);
        Assert.Equal(6, submitted.Request!.RequestedMonths);
        Assert.Equal(RenewalRequestStatus.Pending, submitted.Request.Status);
        Assert.Equal("Gia giu phong nay cho toi", submitted.Request.Note);
        Assert.Equal(FrozenNow.UtcDateTime, submitted.Request.RequestedAt);

        var stored = await db.ContractRenewalRequests.SingleAsync();
        Assert.Equal(contract.ContractId, stored.ContractId);
        Assert.Equal((await TenantOfAsync(db, TenantUserId)).Id, stored.RequestedByTenantId);
        Assert.Equal(RenewalRequestStatus.Pending, stored.Status);

        var pending = await service.GetPendingAsync(TenantCallerOf(TenantUserId), contract.ContractId);
        Assert.True(pending.Succeeded, pending.ErrorMessage);
        Assert.Equal(submitted.Request.Id, pending.Request!.Id);

        // Cùng hợp đồng nên khách ở ghép thấy đúng một yêu cầu đang chờ, không thấy bản riêng.
        var asRoommate = await service.GetPendingAsync(TenantCallerOf(RoommateUserId), contract.ContractId);
        Assert.Equal(submitted.Request.Id, asRoommate.Request!.Id);
    }

    [SqlServerFact]
    public async Task Tu_cho_so_thang_ngoai_khoang_1_den_120()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var contract = await SeedApprovedContractAsync(db, TenantUserId);
        var service = NewService(db);

        foreach (var months in new[] { 0, -3, ContractRenewalRules.MaxRequestedMonths + 1 })
        {
            var result = await service.SubmitAsync(
                TenantCallerOf(TenantUserId), contract.ContractId, months, null);

            Assert.False(result.Succeeded);
            Assert.Equal(ContractRenewalErrorCodes.InvalidMonths, result.ErrorCode);
        }

        Assert.Empty(await db.ContractRenewalRequests.ToListAsync());
    }

    [SqlServerFact]
    public async Task Chan_khach_khong_phai_thanh_vien_va_khong_phai_khach_thue()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var contract = await SeedApprovedContractAsync(db, TenantUserId);
        var service = NewService(db);

        var outsider = await service.SubmitAsync(TenantCallerOf(OtherTenantUserId), contract.ContractId, 6, null);
        Assert.Equal(ContractRenewalErrorCodes.Forbidden, outsider.ErrorCode);

        var manager = await service.SubmitAsync(
            new TenantCaller(ManagerUserId, Roles.QuanLy), contract.ContractId, 6, null);
        Assert.Equal(ContractRenewalErrorCodes.Forbidden, manager.ErrorCode);

        var unknownContract = await service.SubmitAsync(
            TenantCallerOf(TenantUserId), Guid.NewGuid(), 6, null);
        Assert.Equal(ContractRenewalErrorCodes.Forbidden, unknownContract.ErrorCode);

        Assert.False((await service.GetPendingAsync(TenantCallerOf(OtherTenantUserId), contract.ContractId)).Succeeded);
        Assert.Empty(await db.ContractRenewalRequests.ToListAsync());
    }

    [SqlServerFact]
    public async Task Tu_cho_yeu_cau_cua_hop_dong_chua_duyet()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);

        var contract = await SeedApprovedContractAsync(db, TenantUserId);
        contract.Status = ContractStatus.Pending;
        await db.SaveChangesAsync();

        var result = await NewService(db).SubmitAsync(
            TenantCallerOf(TenantUserId), contract.ContractId, 6, null);

        Assert.Equal(ContractRenewalErrorCodes.InvalidStatus, result.ErrorCode);
        Assert.Empty(await db.ContractRenewalRequests.ToListAsync());
    }

    [SqlServerFact]
    public async Task Mot_hop_dong_chi_giu_mot_yeu_cau_dang_cho()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var contract = await SeedApprovedContractAsync(db, TenantUserId, RoommateUserId);
        var service = NewService(db);

        Assert.True((await service.SubmitAsync(
            TenantCallerOf(TenantUserId), contract.ContractId, 6, null)).Succeeded);

        // Khách ở ghép thử gửi song song: yêu cầu thứ hai bị chặn, không có dòng thứ hai.
        var second = await service.SubmitAsync(
            TenantCallerOf(RoommateUserId), contract.ContractId, 12, "Gia han muon nhe");
        Assert.Equal(ContractRenewalErrorCodes.DuplicatePending, second.ErrorCode);

        Assert.Equal(1, await db.ContractRenewalRequests.CountAsync());

        // Sau khi yêu cầu cũ không còn chờ thì được gửi yêu cầu mới.
        var stored = await db.ContractRenewalRequests.SingleAsync();
        stored.Status = RenewalRequestStatus.Rejected;
        await db.SaveChangesAsync();

        var afterDecision = await service.SubmitAsync(
            TenantCallerOf(RoommateUserId), contract.ContractId, 12, null);
        Assert.True(afterDecision.Succeeded, afterDecision.ErrorMessage);
    }

    [SqlServerFact]
    public async Task Chua_gui_thi_khong_co_yeu_cho()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var contract = await SeedApprovedContractAsync(db, TenantUserId);

        var result = await NewService(db).GetPendingAsync(TenantCallerOf(TenantUserId), contract.ContractId);

        Assert.True(result.Succeeded);
        Assert.Null(result.Request);
    }

    private ContractRenewalService NewService(ApplicationDbContext db) =>
        new(db, new FixedTimeProvider(FrozenNow));

    private async Task ResetDatabaseAsync()
    {
        await using var db = fixture.CreateContext();
        await db.ContractRenewalRequests.ExecuteDeleteAsync();
        await db.ContractMembers.ExecuteDeleteAsync();
        await db.Contracts.ExecuteDeleteAsync();

        await fixture.ResetAsync();
    }

    private static async Task SeedAccountsAsync(ApplicationDbContext db)
    {
        await SqlServerFixture.SeedAccountAsync(db, OwnerUserId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, TenantUserId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, RoommateUserId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, OtherTenantUserId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, ManagerUserId, Roles.QuanLy);
    }

    private static async Task<Contract> SeedApprovedContractAsync(
        ApplicationDbContext db, params string[] memberUserIds)
    {
        var property = new Property { Name = "Nhà TEST_RENEWAL", OwnerId = OwnerUserId };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var room = new Room { PropertyId = property.Id, Code = "TEST-RENEWAL-101", StandardPrice = 1_000_000m };
        db.Rooms.Add(room);
        await db.SaveChangesAsync();

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            ContractNumber = $"HD-{Guid.NewGuid():N}",
            RoomId = room.Id,
            StartDate = new DateTime(2026, 10, 22),
            EndDate = new DateTime(2027, 10, 21),
            SnapshotPrice = 1_000_000m,
            DepositAmount = 200_000m,
            Status = ContractStatus.Approved,
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();

        foreach (var userId in memberUserIds)
        {
            var tenant = await TenantOfAsync(db, userId);
            db.ContractMembers.Add(new ContractMember { ContractId = contract.ContractId, TenantId = tenant.Id });
        }

        await db.SaveChangesAsync();
        return contract;
    }

    private static async Task<Tenant> TenantOfAsync(ApplicationDbContext db, string userId)
    {
        var existing = await db.Tenants.FirstOrDefaultAsync(tenant => tenant.UserId == userId);
        if (existing is not null)
        {
            return existing;
        }

        var tenant = new Tenant
        {
            FullName = $"Khach {userId}",
            Phone = PhoneOf(userId),
            UserId = userId,
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    // Số điện thoại cố định theo user để không trùng và không phụ thuộc hash.
    private static string PhoneOf(string userId) => userId switch
    {
        TenantUserId => "0900000101",
        RoommateUserId => "0900000102",
        OtherTenantUserId => "0900000103",
        _ => throw new InvalidOperationException($"Chưa chuẩn bị số diện thoại cho {userId}."),
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
