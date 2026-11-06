using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
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
using Web.Features.Contracts.Services;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Rooms;
using Web.Features.Rooms.Entities;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;
using Web.Features.Tenants;
using Web.Features.Tenants.Entities;
using NhaTro.IntegrationTests.Infrastructure;

namespace NhaTro.IntegrationTests.Features.Contracts;

/// <summary>
/// VHT-125 (2E): BQL duyệt yêu cầu gia hạn. Chấp thuận delegates việc gia hạn cho P4 nên
/// Quản lý đi qua công tắc SW-07; từ chối bắt buộc lý do và không được đụng tới hợp đồng.
/// </summary>
public sealed class ContractRenewalDecisionIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string OwnerId = "renewal-decide-owner";
    private const string OtherOwnerId = "renewal-decide-other-owner";
    private const string ManagerId = "renewal-decide-manager";
    private const string TenantUserId = "renewal-decide-tenant";
    private const string OtherTenantUserId = "renewal-decide-outsider";
    private static readonly DateTimeOffset FrozenNow = new(2026, 11, 4, 3, 30, 0, TimeSpan.Zero);
    private static readonly PropertyCaller OwnerCaller = new(OwnerId, Roles.ChuTro);
    private static readonly PropertyCaller ManagerCaller = new(ManagerId, Roles.QuanLy);

    private static TimeProvider Clock { get; } = new FixedTimeProvider(FrozenNow);

    [SqlServerFact]
    public async Task Chap_thuan_gia_han_dung_so_thang_khach_xin_va_chot_yeu_cau()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var (contract, requestId) = await SeedPendingRequestAsync(db, "TEST-RENEWDEC-A", 6);

        var result = await NewService(db)
            .DecideAsync(requestId, new RenewalDecisionRequest { IsApproved = true }, OwnerCaller);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(RenewalRequestStatus.Approved, result.Request!.Status);
        Assert.False(result.AwaitingOwnerApproval);
        Assert.Equal(FrozenNow.UtcDateTime, result.Request.DecidedAt);

        await using var check = fixture.CreateContext();
        var stored = await check.ContractRenewalRequests.SingleAsync();
        Assert.Equal(RenewalRequestStatus.Approved, stored.Status);
        Assert.Equal(OwnerId, stored.DecidedByUserId);
        Assert.Null(stored.RejectReason);

        var contracts = await check.Contracts.OrderBy(item => item.StartDate).ToListAsync();
        Assert.Equal(2, contracts.Count);
        Assert.Equal(contract.ContractId, contracts[0].ContractId);
        Assert.Equal(new DateTime(2027, 10, 22), contracts[1].StartDate);
        Assert.Equal(new DateTime(2028, 4, 21), contracts[1].EndDate);
        Assert.Equal(ContractStatus.Approved, contracts[1].Status);
    }

    [SqlServerFact]
    public async Task Tu_choi_can_ly_do_va_khong_dong_vao_hop_dong()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var (contract, requestId) = await SeedPendingRequestAsync(db, "TEST-RENEWDEC-B", 3);
        var service = NewService(db);

        var missing = await service.DecideAsync(
            requestId, new RenewalDecisionRequest { IsApproved = false, Reason = "   " }, OwnerCaller);
        Assert.Equal(ContractRenewalErrorCodes.RejectReasonRequired, missing.ErrorCode);
        Assert.Equal(RenewalRequestStatus.Pending, (await db.ContractRenewalRequests.SingleAsync()).Status);

        var accepted = await service.DecideAsync(
            requestId,
            new RenewalDecisionRequest { IsApproved = false, Reason = "  Phong nay se sua thang toi  " },
            OwnerCaller);

        Assert.True(accepted.Succeeded, accepted.ErrorMessage);
        Assert.Equal(RenewalRequestStatus.Rejected, accepted.Request!.Status);
        Assert.Equal("Phong nay se sua thang toi", accepted.Request.RejectReason);

        await using var check = fixture.CreateContext();
        var stored = await check.ContractRenewalRequests.SingleAsync();
        Assert.Equal(RenewalRequestStatus.Rejected, stored.Status);
        Assert.Equal("Phong nay se sua thang toi", stored.RejectReason);
        Assert.Equal(OwnerId, stored.DecidedByUserId);
        Assert.Equal(1, await check.Contracts.CountAsync(item => item.ContractId == contract.ContractId));
        Assert.Equal(new DateTime(2027, 10, 21), (await check.Contracts.SingleAsync()).EndDate);

        // Tab "Kết quả & lịch sử" phía khách thuê: phải thấy trạng thái + lý do bị từ chối của yêu cầu mới nhất.
        var asTenant = await NewService(check)
            .ListForContractAsync(new TenantCaller(TenantUserId, Roles.KhachThue), contract.ContractId);
        Assert.True(asTenant.Succeeded, asTenant.ErrorMessage);
        var historyRow = Assert.Single(asTenant.Items);
        Assert.Equal(RenewalRequestStatus.Rejected, historyRow.Status);
        Assert.Equal("Phong nay se sua thang toi", historyRow.RejectReason);
        Assert.Equal(FrozenNow.UtcDateTime, historyRow.DecidedAt);
    }

    [SqlServerFact]
    public async Task Chan_nguoi_ngoai_pham_vi_va_khong_duoc_duyet_lan_hai()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var (_, requestId) = await SeedPendingRequestAsync(db, "TEST-RENEWDEC-C", 6);
        var service = NewService(db);

        var otherOwner = await service.DecideAsync(
            requestId,
            new RenewalDecisionRequest { IsApproved = true },
            new PropertyCaller(OtherOwnerId, Roles.ChuTro));
        Assert.Equal(ContractRenewalErrorCodes.Forbidden, otherOwner.ErrorCode);

        var unassignedManager = await service.DecideAsync(
            requestId,
            new RenewalDecisionRequest { IsApproved = true },
            ManagerCaller);
        Assert.Equal(ContractRenewalErrorCodes.Forbidden, unassignedManager.ErrorCode);

        var asTenant = await service.DecideAsync(
            requestId,
            new RenewalDecisionRequest { IsApproved = true },
            new PropertyCaller(TenantUserId, Roles.KhachThue));
        Assert.Equal(ContractRenewalErrorCodes.Forbidden, asTenant.ErrorCode);

        var unknown = await service.DecideAsync(
            Guid.NewGuid(),
            new RenewalDecisionRequest { IsApproved = false, Reason = "Khong ton tai" },
            OwnerCaller);
        Assert.Equal(ContractRenewalErrorCodes.RequestNotFound, unknown.ErrorCode);

        Assert.Equal(RenewalRequestStatus.Pending, (await db.ContractRenewalRequests.SingleAsync()).Status);
        Assert.Empty(await db.Contracts.Where(item => item.StartDate > new DateTime(2027, 1, 1)).ToListAsync());

        // Chủ trọ duyệt xong thì yêu cầu không còn mở để duyệt lần hai.
        Assert.True((await service.DecideAsync(
            requestId, new RenewalDecisionRequest { IsApproved = true }, OwnerCaller)).Succeeded);

        var twice = await service.DecideAsync(
            requestId,
            new RenewalDecisionRequest { IsApproved = false, Reason = "Duyet lai" },
            OwnerCaller);
        Assert.Equal(ContractRenewalErrorCodes.AlreadyDecided, twice.ErrorCode);
    }

    [SqlServerFact]
    public async Task Quan_ly_duy_khi_SW07_mo_va_yeu_cau_van_con_cho_Ch_u_tro()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var (contract, requestId) = await SeedPendingRequestAsync(db, "TEST-RENEWDEC-D", 6);
        await AssignManagerAsync(db, contract.RoomId);

        var approvals = NewApprovals(db);
        var service = NewService(db, new ContractService(
            db, Clock, new FixedTogglePolicy(RoomChangeDecision.Allowed), approvals));

        var result = await service.DecideAsync(
            requestId, new RenewalDecisionRequest { IsApproved = true }, ManagerCaller);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.False(result.AwaitingOwnerApproval);

        await using var check = fixture.CreateContext();
        Assert.Equal(RenewalRequestStatus.Approved, (await check.ContractRenewalRequests.SingleAsync()).Status);
        Assert.Equal(ManagerId, (await check.ContractRenewalRequests.SingleAsync()).DecidedByUserId);
        Assert.Equal(2, await check.Contracts.CountAsync());
        Assert.Empty(await check.ManagerApprovalRequests.ToListAsync());
    }

    [SqlServerFact]
    public async Task Quan_ly_duy_khi_SW07_bat_dong_cho_Ch_u_tro_roi_Ch_u_tro_duyet_moi_chot()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var (contract, requestId) = await SeedPendingRequestAsync(db, "TEST-RENEWDEC-E", 6);
        await AssignManagerAsync(db, contract.RoomId);

        var approvals = NewApprovals(db);
        var service = NewService(db, new ContractService(
            db, Clock, new FixedTogglePolicy(RoomChangeDecision.RequiresApproval), approvals));

        var result = await service.DecideAsync(
            requestId, new RenewalDecisionRequest { IsApproved = true }, ManagerCaller);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.AwaitingOwnerApproval);

        await using (var waitingDb = fixture.CreateContext())
        {
            // Yêu cầu của khách vẫn Chờ xử lý và chưa có hợp đồng mới.
            var waiting = await waitingDb.ContractRenewalRequests.SingleAsync();
            Assert.Equal(RenewalRequestStatus.Pending, waiting.Status);
            Assert.Null(waiting.DecidedByUserId);
            Assert.Equal(1, await waitingDb.Contracts.CountAsync());

            var queued = await waitingDb.ManagerApprovalRequests.SingleAsync();
            Assert.Equal("SW-07", queued.OperationCode);
            Assert.Equal(ApprovalTargetType.Contract, queued.TargetType);
            Assert.Equal(contract.ContractId.ToString("D"), queued.TargetKey);

            var decision = await approvals.ApproveAsync(
                queued.Id, new ApprovalCaller(OwnerId, Roles.ChuTro));
            Assert.True(decision.Succeeded, decision.ErrorMessage);
        }

        await using var doneDb = fixture.CreateContext();
        var settled = await doneDb.ContractRenewalRequests.SingleAsync();
        Assert.Equal(RenewalRequestStatus.Approved, settled.Status);
        Assert.Equal(OwnerId, settled.DecidedByUserId);
        Assert.Equal(FrozenNow.UtcDateTime, settled.DecidedAt);
        Assert.Equal(2, await doneDb.Contracts.CountAsync());
    }

    [SqlServerFact]
    public async Task Quan_ly_bị_chan_khi_SW07_tat_va_hop_dong_khong_doi_trang_thai()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var (contract, requestId) = await SeedPendingRequestAsync(db, "TEST-RENEWDEC-F", 6);
        await AssignManagerAsync(db, contract.RoomId);

        var service = NewService(db, new ContractService(
            db, Clock, new FixedTogglePolicy(RoomChangeDecision.Denied), NewApprovals(db)));

        var result = await service.DecideAsync(
            requestId, new RenewalDecisionRequest { IsApproved = true }, ManagerCaller);

        Assert.False(result.Succeeded);
        Assert.Equal(ContractRenewalErrorCodes.ApprovalDenied, result.ErrorCode);

        await using var check = fixture.CreateContext();
        Assert.Equal(RenewalRequestStatus.Pending, (await check.ContractRenewalRequests.SingleAsync()).Status);
        Assert.Equal(1, await check.Contracts.CountAsync());
        Assert.Empty(await check.ManagerApprovalRequests.ToListAsync());
    }

    [SqlServerFact]
    public async Task Danh_sach_cho_chi_thay_yeu_cau_trong_pham_vi()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var (mine, mineId) = await SeedPendingRequestAsync(db, "TEST-RENEWDEC-G", 6, note: "Gia han nhe");
        var (theirs, _) = await SeedPendingRequestAsyncAsyncOtherOwner(db, "TEST-RENEWDEC-H");
        await AssignManagerAsync(db, mine.RoomId);

        var ownerView = await NewService(db).ListPendingAsync(OwnerCaller);
        Assert.True(ownerView.Succeeded);
        var row = Assert.Single(ownerView.Items);
        Assert.Equal(mineId, row.Id);
        Assert.Equal("TEST-RENEWDEC-G", row.RoomCode);
        Assert.Equal("Nhà TEST_RENEWDEC", row.PropertyName);
        Assert.Equal($"Khach {TenantUserId}", row.RequesterName);
        Assert.Equal("Gia han nhe", row.Note);
        Assert.Equal(6, row.RequestedMonths);
        Assert.Equal(new DateTime(2027, 10, 21), row.CurrentEndDate);
        Assert.Equal(new DateTime(2028, 4, 21), row.ProposedEndDate);

        var managerView = await NewService(db).ListPendingAsync(ManagerCaller);
        Assert.Equal([mineId], managerView.Items.Select(item => item.Id).ToList());

        var otherOwnerView = await NewService(db)
            .ListPendingAsync(new PropertyCaller(OtherOwnerId, Roles.ChuTro));
        Assert.Equal([theirs.ContractId], otherOwnerView.Items.Select(item => item.ContractId).ToList());

        var tenantView = await NewService(db)
            .ListPendingAsync(new PropertyCaller(TenantUserId, Roles.KhachThue));
        Assert.False(tenantView.Succeeded);
        Assert.Equal(ContractRenewalErrorCodes.Forbidden, tenantView.ErrorCode);
    }

    [SqlServerFact]
    public async Task Lich_su_yeu_cau_cua_hop_dong_moi_nhat_truoc_va_ke_ca_ly_do_tu_choi()
    {
        await ResetDatabaseAsync();
        await using var db = fixture.CreateContext();
        await SeedAccountsAsync(db);
        var (contract, olderId) = await SeedPendingRequestAsync(db, "TEST-RENEWDEC-H", 4);
        var service = NewService(db);
        var tenantId = (await db.ContractRenewalRequests.SingleAsync(item => item.Id == olderId)).RequestedByTenantId;

        await service.DecideAsync(
            olderId,
            new RenewalDecisionRequest { IsApproved = false, Reason = "Phong se sua chua" },
            OwnerCaller);

        db.ContractRenewalRequests.Add(new ContractRenewalRequest
        {
            ContractId = contract.ContractId,
            RequestedByTenantId = tenantId,
            RequestedMonths = 9,
            Status = RenewalRequestStatus.Pending,
            RequestedAt = FrozenNow.UtcDateTime.AddDays(1),
        });
        await db.SaveChangesAsync();

        var history = await service.ListForContractAsync(
            new TenantCaller(TenantUserId, Roles.KhachThue), contract.ContractId);

        Assert.True(history.Succeeded, history.ErrorMessage);
        Assert.Equal(
            [RenewalRequestStatus.Pending, RenewalRequestStatus.Rejected],
            history.Items.Select(item => item.Status).ToList());
        Assert.Equal(9, history.Items[0].RequestedMonths);
        Assert.Equal("Phong se sua chua", history.Items[1].RejectReason);
        Assert.Equal(FrozenNow.UtcDateTime, history.Items[1].DecidedAt);

        var outsider = await service.ListForContractAsync(
            new TenantCaller(OtherTenantUserId, Roles.KhachThue), contract.ContractId);
        Assert.False(outsider.Succeeded);
        Assert.Equal(ContractRenewalErrorCodes.Forbidden, outsider.ErrorCode);
    }

    private static ManagerApprovalService NewApprovals(ApplicationDbContext db) =>
        new(db, Clock, [new ContractApprovalApplier(Clock)], NullLogger<ManagerApprovalService>.Instance);

    private static ContractRenewalService NewService(ApplicationDbContext db) =>
        new(db, Clock, new ContractService(db, Clock));

    private static ContractRenewalService NewService(ApplicationDbContext db, ContractService contracts) =>
        new(db, Clock, contracts);

    private async Task ResetDatabaseAsync()
    {
        await using var db = fixture.CreateContext();
        await db.ContractRenewalRequests.ExecuteDeleteAsync();

        await fixture.ResetAsync();
    }

    private static async Task SeedAccountsAsync(ApplicationDbContext db)
    {
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, TenantUserId, Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, OtherTenantUserId, Roles.KhachThue);
    }

    private static async Task<(Contract Contract, Guid RequestId)> SeedPendingRequestAsync(
        ApplicationDbContext db,
        string roomCode,
        int months,
        string? note = null,
        string ownerId = OwnerId)
    {
        var property = new Property { Name = "Nhà TEST_RENEWDEC", OwnerId = ownerId };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        var room = new Room { PropertyId = property.Id, Code = roomCode, StandardPrice = 1_000_000m };
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

        var tenant = await TenantOfAsync(db, TenantUserId);
        db.ContractMembers.Add(new ContractMember { ContractId = contract.ContractId, TenantId = tenant.Id });

        var request = new ContractRenewalRequest
        {
            ContractId = contract.ContractId,
            RequestedByTenantId = tenant.Id,
            RequestedMonths = months,
            Note = note,
            Status = RenewalRequestStatus.Pending,
            RequestedAt = FrozenNow.UtcDateTime,
        };
        db.ContractRenewalRequests.Add(request);
        await db.SaveChangesAsync();

        return (contract, request.Id);
    }

    private static Task<(Contract Contract, Guid RequestId)> SeedPendingRequestAsyncAsyncOtherOwner(
        ApplicationDbContext db, string roomCode) =>
        SeedPendingRequestAsync(db, roomCode, 12, ownerId: OtherOwnerId);

    private static async Task AssignManagerAsync(ApplicationDbContext db, int roomId)
    {
        var room = await db.Rooms.SingleAsync(item => item.Id == roomId);
        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = room.PropertyId,
            ManagerUserId = ManagerId,
            AssignedByUserId = OwnerId,
        });
        await db.SaveChangesAsync();
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

    private static string PhoneOf(string userId) => userId switch
    {
        TenantUserId => "0900000201",
        OtherTenantUserId => "0900000202",
        _ => throw new InvalidOperationException($"Chưa chuẩn bị số diện thoại cho {userId}."),
    };

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class FixedTogglePolicy(RoomChangeDecision decision) : IManagerTogglePolicy
    {
        public Task<RoomChangeDecision> EvaluateAsync(
            ManagerAction action, string role, CancellationToken ct = default) =>
            Task.FromResult(decision);

        public Task<RoomChangeDecision> EvaluateAsync(
            string code, string role, CancellationToken ct = default) =>
            Task.FromResult(decision);
    }
}
