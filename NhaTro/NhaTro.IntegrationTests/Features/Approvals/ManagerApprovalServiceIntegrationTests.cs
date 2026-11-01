using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.DTOs;
using Web.Features.Approvals.Entities;
using Web.Features.Approvals.Enums;
using Web.Features.Approvals.Services;
using Web.Features.Properties.Entities;
using Web.Features.Tenants.Entities;

namespace NhaTro.IntegrationTests.Features.Approvals;

/// <summary>
/// VHT-96 (NEW-03) — integration test trên SQL Server thật cho hàng đợi duyệt thao tác Quản lý:
/// duyệt hai lần chỉ áp dụng một lần, dữ liệu đã đổi thì bị chặn, từ chối không đổi dữ liệu,
/// Quản lý không duyệt được, hủy đúng người, chống tạo trùng và hai luồng duyệt song song.
/// </summary>
public sealed class ManagerApprovalServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string OwnerId = "chu-tro-1";
    private const string OtherOwnerId = "chu-tro-2";
    private const string ManagerId = "quan-ly-1";
    private const string OtherManagerId = "quan-ly-2";

    private static readonly ApprovalCaller ChuTro = new(OwnerId, Roles.ChuTro);
    private static readonly ApprovalCaller OtherOwner = new(OtherOwnerId, Roles.ChuTro);
    private static readonly ApprovalCaller QuanLy = new(ManagerId, Roles.QuanLy);
    private static readonly ApprovalCaller OtherManager = new(OtherManagerId, Roles.QuanLy);

    private static ManagerApprovalService NewService(ApplicationDbContext db) =>
        new(db,
            TimeProvider.System,
            [new TenantRenameApplier()],
            NullLogger<ManagerApprovalService>.Instance);

    private async Task<int> ResetAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, OtherOwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, OtherManagerId, Roles.QuanLy);

        // Quản lý luôn được phân công ít nhất một tòa của Chủ trọ; yêu cầu chưa gắn tòa
        // (tạo hồ sơ mới) dựa vào phân công này để xác định Chủ trọ được duyệt.
        var property = new Property { Name = "Nhà A", OwnerId = OwnerId };
        db.Properties.Add(property);
        await db.SaveChangesAsync();

        db.PropertyManagers.Add(new PropertyManager
        {
            PropertyId = property.Id,
            ManagerUserId = ManagerId,
            AssignedByUserId = OwnerId,
        });
        await db.SaveChangesAsync();

        return property.Id;
    }

    private async Task<(int TenantId, byte[] RowVersion)> SeedTenantAsync(string fullName = "An")
    {
        await using var db = fixture.CreateContext();
        var tenant = new Tenant { FullName = fullName, Phone = "0901234567" };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return (tenant.Id, tenant.RowVersion);
    }

    /// <summary>Quản lý gửi yêu cầu đổi tên hồ sơ (SW-05) với token hiện tại của hồ sơ.</summary>
    private async Task<ApprovalResult> CreateRenameAsync(
        int tenantId, byte[]? expectedToken, int? propertyId = null, string targetKey = null!, string newName = "Bình")
    {
        await using var db = fixture.CreateContext();
        return await NewService(db).CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = "SW-05",
                TargetType = ApprovalTargetType.Tenant,
                TargetKey = targetKey ?? tenantId.ToString(),
                PropertyId = propertyId,
                ExpectedToken = expectedToken,
                PayloadJson = JsonSerializer.Serialize(new RenamePayload(tenantId, newName)),
            },
            QuanLy);
    }

    private async Task<ApprovalResult> ApproveAsync(Guid id, ApprovalCaller caller)
    {
        await using var db = fixture.CreateContext();
        return await NewService(db).ApproveAsync(id, caller);
    }

    private async Task<string> TenantNameAsync(int tenantId)
    {
        await using var db = fixture.CreateContext();
        return (await db.Tenants.SingleAsync(tenant => tenant.Id == tenantId)).FullName;
    }

    // ---------- Tiêu chí 1: duyệt hai lần chỉ áp dụng một lần ----------

    [SqlServerFact]
    public async Task Duyet_hai_lan_chi_ap_dung_mot_lan()
    {
        await ResetAsync();
        var (tenantId, rowVersion) = await SeedTenantAsync("An");

        var created = await CreateRenameAsync(tenantId, rowVersion, newName: "Bình");
        Assert.True(created.Succeeded, created.ErrorMessage);

        var first = await ApproveAsync(created.Request!.Id, ChuTro);
        Assert.True(first.Succeeded, first.ErrorMessage);
        Assert.Equal(ApprovalRequestStatus.Approved, first.Request!.Status);
        Assert.Equal("Bình", await TenantNameAsync(tenantId));

        var second = await ApproveAsync(created.Request.Id, ChuTro);
        Assert.False(second.Succeeded);
        Assert.Equal(ApprovalErrorCodes.AlreadyDecided, second.ErrorCode);
        Assert.Equal("Bình", await TenantNameAsync(tenantId));
    }

    // ---------- Tiêu chí 2: dữ liệu đã đổi thì bị chặn, có thông báo ----------

    [SqlServerFact]
    public async Task Duyet_khi_du_lieu_da_doi_bi_chan()
    {
        await ResetAsync();
        var (tenantId, rowVersion) = await SeedTenantAsync("An");
        var created = await CreateRenameAsync(tenantId, rowVersion, newName: "Bình");

        // Người khác sửa hồ sơ trong lúc yêu cầu còn Chờ duyệt.
        await using (var db = fixture.CreateContext())
        {
            var tenant = await db.Tenants.SingleAsync(item => item.Id == tenantId);
            tenant.FullName = "Cường";
            await db.SaveChangesAsync();
        }

        var approved = await ApproveAsync(created.Request!.Id, ChuTro);

        Assert.False(approved.Succeeded);
        Assert.Equal(ApprovalErrorCodes.Conflict, approved.ErrorCode);
        Assert.Contains("đã thay đổi", approved.ErrorMessage);
        Assert.Equal("Cường", await TenantNameAsync(tenantId));
    }

    // ---------- Tiêu chí 3: từ chối không đổi dữ liệu ----------

    [SqlServerFact]
    public async Task Tu_choi_khong_doi_du_lieu_va_bat_buoc_ly_do()
    {
        await ResetAsync();
        var (tenantId, rowVersion) = await SeedTenantAsync("An");
        var created = await CreateRenameAsync(tenantId, rowVersion, newName: "Bình");

        var missingReason = await RejectAsync(created.Request!.Id, ChuTro, "   ");
        Assert.Equal(ApprovalErrorCodes.RejectReasonRequired, missingReason.ErrorCode);

        var tooLong = await RejectAsync(created.Request.Id, ChuTro, new string('x', 501));
        Assert.Equal(ApprovalErrorCodes.RejectReasonTooLong, tooLong.ErrorCode);

        var rejected = await RejectAsync(created.Request.Id, ChuTro, "Không đồng ý");
        Assert.True(rejected.Succeeded, rejected.ErrorMessage);
        Assert.Equal(ApprovalRequestStatus.Rejected, rejected.Request!.Status);
        Assert.Equal("Không đồng ý", rejected.Request.DecisionReason);
        Assert.Equal("An", await TenantNameAsync(tenantId));

        var approveAfterReject = await ApproveAsync(created.Request.Id, ChuTro);
        Assert.Equal(ApprovalErrorCodes.AlreadyDecided, approveAfterReject.ErrorCode);
        Assert.Equal("An", await TenantNameAsync(tenantId));
    }

    // ---------- Tiêu chí 4: Quản lý không duyệt được yêu cầu của mình ----------

    [SqlServerFact]
    public async Task Quan_ly_khong_duyet_duoc_yeu_cau_cua_minh()
    {
        await ResetAsync();
        var (tenantId, rowVersion) = await SeedTenantAsync("An");
        var created = await CreateRenameAsync(tenantId, rowVersion, newName: "Bình");

        var approved = await ApproveAsync(created.Request!.Id, QuanLy);
        Assert.False(approved.Succeeded);
        Assert.Equal(ApprovalErrorCodes.Forbidden, approved.ErrorCode);

        var rejected = await RejectAsync(created.Request.Id, QuanLy, "tự duyệt");
        Assert.Equal(ApprovalErrorCodes.Forbidden, rejected.ErrorCode);

        Assert.Equal("An", await TenantNameAsync(tenantId));
    }

    // ---------- Hủy: đúng người, đúng trạng thái ----------

    [SqlServerFact]
    public async Task Huy_chi_nguoi_tao_va_chi_khi_con_cho_duyet()
    {
        await ResetAsync();
        var (tenantId, rowVersion) = await SeedTenantAsync("An");
        var created = await CreateRenameAsync(tenantId, rowVersion, newName: "Bình");
        var id = created.Request!.Id;

        await using (var db = fixture.CreateContext())
        {
            var service = NewService(db);
            var other = await service.CancelAsync(id, OtherManager);
            Assert.Equal(ApprovalErrorCodes.Forbidden, other.ErrorCode);

            var owner = await service.CancelAsync(id, ChuTro);
            Assert.Equal(ApprovalErrorCodes.Forbidden, owner.ErrorCode);

            var cancelled = await service.CancelAsync(id, QuanLy);
            Assert.True(cancelled.Succeeded, cancelled.ErrorMessage);
            Assert.Equal(ApprovalRequestStatus.Cancelled, cancelled.Request!.Status);
        }

        var approveAfterCancel = await ApproveAsync(id, ChuTro);
        Assert.Equal(ApprovalErrorCodes.AlreadyDecided, approveAfterCancel.ErrorCode);
        Assert.Equal("An", await TenantNameAsync(tenantId));
    }

    // ---------- Chống tạo trùng yêu cầu Chờ duyệt ----------

    [SqlServerFact]
    public async Task Tao_trung_yeu_cau_cho_cung_doi_tuong_bi_chan()
    {
        await ResetAsync();
        var (tenantId, rowVersion) = await SeedTenantAsync("An");

        var first = await CreateRenameAsync(tenantId, rowVersion, newName: "Bình");
        Assert.True(first.Succeeded, first.ErrorMessage);

        var second = await CreateRenameAsync(tenantId, rowVersion, newName: "Cường");
        Assert.False(second.Succeeded);
        Assert.Equal(ApprovalErrorCodes.DuplicatePendingRequest, second.ErrorCode);

        // Đối tượng khác thì vẫn tạo được.
        var (otherTenantId, otherRowVersion) = await SeedTenantAsync("Dũng");
        var otherTarget = await CreateRenameAsync(otherTenantId, otherRowVersion, newName: "Em");
        Assert.True(otherTarget.Succeeded, otherTarget.ErrorMessage);
    }

    // ---------- Hai luồng duyệt song song chỉ một thắng ----------

    [SqlServerFact]
    public async Task Hai_luong_duyet_song_song_chi_ap_dung_mot_lan()
    {
        await ResetAsync();
        var (tenantId, rowVersion) = await SeedTenantAsync("An");
        var created = await CreateRenameAsync(tenantId, rowVersion, newName: "Bình");
        var id = created.Request!.Id;

        await using var db1 = fixture.CreateContext();
        await using var db2 = fixture.CreateContext();

        var results = await Task.WhenAll(
            NewService(db1).ApproveAsync(id, ChuTro),
            NewService(db2).ApproveAsync(id, ChuTro));

        Assert.Equal(1, results.Count(result => result.Succeeded));
        Assert.All(
            results.Where(result => !result.Succeeded),
            result => Assert.Equal(ApprovalErrorCodes.AlreadyDecided, result.ErrorCode));
        Assert.Equal("Bình", await TenantNameAsync(tenantId));
    }

    // ---------- Phạm vi: Quản lý chỉ thấy của mình, Chủ trọ chỉ thấy tòa mình ----------

    [SqlServerFact]
    public async Task Danh_sach_chi_tra_trong_pham_vi()
    {
        var propertyId = await ResetAsync();

        var (tenantId, rowVersion) = await SeedTenantAsync("An");
        var created = await CreateRenameAsync(tenantId, rowVersion, propertyId);

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var mine = await service.ListMineAsync(QuanLy);
        Assert.True(mine.Succeeded);
        Assert.Single(mine.Items);

        var otherManagerList = await service.ListMineAsync(OtherManager);
        Assert.Empty(otherManagerList.Items);

        var inbox = await service.ListInboxAsync(ChuTro);
        Assert.True(inbox.Succeeded);
        Assert.Single(inbox.Items);

        var otherOwnerInbox = await service.ListInboxAsync(OtherOwner);
        Assert.Empty(otherOwnerInbox.Items);

        Assert.Equal(created.Request!.Id, inbox.Items[0].Id);
    }

    // ---------- Kiểm dữ liệu đầu vào ----------

    [SqlServerFact]
    public async Task Ma_SW_la_va_payload_khong_hop_le_bi_chan()
    {
        await ResetAsync();

        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var unknownCode = await service.CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = "SW-99",
                TargetType = ApprovalTargetType.Tenant,
                TargetKey = "1",
                PayloadJson = "{}",
            },
            QuanLy);
        Assert.Equal(ApprovalErrorCodes.UnknownOperationCode, unknownCode.ErrorCode);

        var badPayload = await service.CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = "SW-05",
                TargetType = ApprovalTargetType.Tenant,
                TargetKey = "1",
                PayloadJson = "khong-phai-json",
            },
            QuanLy);
        Assert.Equal(ApprovalErrorCodes.InvalidRequest, badPayload.ErrorCode);

        var chuTroCreate = await service.CreateAsync(
            new CreateApprovalRequest
            {
                OperationCode = "SW-05",
                TargetType = ApprovalTargetType.Tenant,
                TargetKey = "1",
                PayloadJson = "{}",
            },
            ChuTro);
        Assert.Equal(ApprovalErrorCodes.Forbidden, chuTroCreate.ErrorCode);
    }

    private async Task<ApprovalResult> RejectAsync(Guid id, ApprovalCaller caller, string reason)
    {
        await using var db = fixture.CreateContext();
        return await NewService(db).RejectAsync(id, new RejectApprovalRequest { Reason = reason }, caller);
    }

    /// <summary>Payload của applier test: đổi tên hồ sơ khách thuê.</summary>
    public sealed record RenamePayload(int TenantId, string FullName);

    /// <summary>
    /// Applier test cho SW-05 (sửa hồ sơ khách thuê) — production chưa đăng ký applier nào,
    /// nên test tự cắm một applier thật trên bảng Tenants (đã có RowVersion).
    /// </summary>
    private sealed class TenantRenameApplier : IApprovalApplier
    {
        public string OperationCode => "SW-05";

        public async Task ApplyAsync(
            ManagerApprovalRequest request, ApplicationDbContext db, CancellationToken ct = default)
        {
            var payload = JsonSerializer.Deserialize<RenamePayload>(request.PayloadJson)
                ?? throw new ApprovalApplyException(ApprovalErrorCodes.InvalidRequest, "Payload rỗng.");

            var tenant = await db.Tenants
                .IgnoreQueryFilters()
                .SingleOrDefaultAsync(item => item.Id == payload.TenantId, ct)
                ?? throw new ApprovalApplyException(
                    ApprovalErrorCodes.OperationFailed, "Không tìm thấy hồ sơ khách thuê.");

            if (request.ExpectedToken is { Length: > 0 })
            {
                db.Entry(tenant).Property(nameof(Tenant.RowVersion)).OriginalValue = request.ExpectedToken;
            }

            tenant.FullName = payload.FullName;
            tenant.UpdatedAt = DateTime.UtcNow;
        }
    }
}
