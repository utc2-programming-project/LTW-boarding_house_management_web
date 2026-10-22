using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Rooms;
using Web.Features.Settings;
using Web.Features.Settings.Entities;
using Web.Features.Settings.Enums;
using Web.Features.Settings.Services;

namespace NhaTro.IntegrationTests.Features.Settings;

/// <summary>
/// VHT-94 (đợt 1D) — integration test trên SQL Server thật, chứng minh các tiêu chí nghiệm thu:
/// 21 công tắc được seed đúng, đổi rồi tải lại đúng, SW-15 chặn Duyệt,
/// Quản lý/Admin không đọc/ghi được, policy đọc đúng chế độ trong DB.
/// </summary>
public sealed class ManagerToggleServiceIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private const string OwnerId = "chu-tro-1";
    private const string ManagerId = "quan-ly-1";

    private static readonly ManagerToggleCaller ChuTro = new(OwnerId, Roles.ChuTro);
    private static readonly ManagerToggleCaller QuanLy = new(ManagerId, Roles.QuanLy);

    private static ManagerToggleService NewService(ApplicationDbContext db) =>
        new(db, TimeProvider.System, NullLogger<ManagerToggleService>.Instance);

    private static ManagerTogglePolicy NewPolicy(ApplicationDbContext db) =>
        new(NewService(db), NullLogger<ManagerTogglePolicy>.Instance);

    /// <summary>Dọn DB, tạo tài khoản Chủ trọ (FK người đổi) và trả 21 công tắc về mặc định.</summary>
    private async Task ResetAsync()
    {
        await fixture.ResetAsync();

        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, OwnerId, Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, ManagerId, Roles.QuanLy);
        await RestoreDefaultsAsync(db);
    }

    private static async Task RestoreDefaultsAsync(ApplicationDbContext db)
    {
        await db.ManagerToggles.ExecuteDeleteAsync();

        foreach (var definition in ManagerToggleCatalog.All)
        {
            db.ManagerToggles.Add(new ManagerToggle
            {
                Code = definition.Code,
                Mode = definition.DefaultMode,
                UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            });
        }

        await db.SaveChangesAsync();
    }

    // ---------- Tiêu chí: đủ 16 + 5 công tắc, mặc định theo ma trận ----------

    [SqlServerFact]
    public async Task Seed_du_21_cong_tac_dung_mac_dinh()
    {
        await ResetAsync();
        await using var db = fixture.CreateContext();

        var result = await NewService(db).GetAllAsync(ChuTro);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(21, result.Items.Count);
        Assert.Equal(16, result.Items.Count(i => i.Code.Length <= 5));
        Assert.Equal(5, result.Items.Count(i => i.Code.Length > 5));
        Assert.Equal(ManagerToggleMode.Approve, ModeOf(result, "SW-02"));
        Assert.Equal(ManagerToggleMode.Approve, ModeOf(result, "SW-16"));
        Assert.Equal(ManagerToggleMode.Allow, ModeOf(result, "SW-15"));
        Assert.Equal(ManagerToggleMode.Allow, ModeOf(result, "SW-15e"));
    }

    // ---------- Tiêu chí: đổi rồi tải lại đúng (SQL thật) ----------

    [SqlServerFact]
    public async Task Chu_tro_doi_cong_tac_luu_vao_sql()
    {
        await ResetAsync();

        await using (var db = fixture.CreateContext())
        {
            var changed = await NewService(db).SetAsync("SW-02", ManagerToggleMode.Allow, ChuTro);
            Assert.True(changed.Succeeded, changed.ErrorMessage);
            Assert.Equal(ManagerToggleMode.Allow, changed.Toggle!.Mode);
            Assert.Equal(OwnerId, changed.Toggle.UpdatedByUserId);
        }

        // Context mới: chứng minh đã ghi xuống SQL, không chỉ cache trong bộ nhớ.
        await using var reloadDb = fixture.CreateContext();
        var reloaded = await NewService(reloadDb).GetAllAsync(ChuTro);
        Assert.Equal(ManagerToggleMode.Allow, ModeOf(reloaded, "SW-02"));
    }

    [SqlServerFact]
    public async Task Chu_tro_doi_SW15_sang_Khong_the()
    {
        await ResetAsync();
        await using var db = fixture.CreateContext();

        var changed = await NewService(db).SetAsync("SW-15", ManagerToggleMode.Deny, ChuTro);

        Assert.True(changed.Succeeded, changed.ErrorMessage);
        Assert.Equal(ManagerToggleMode.Deny, changed.Toggle!.Mode);
    }

    // ---------- Tiêu chí: SW-15 chỉ Cho phép / Không thể ----------

    [SqlServerFact]
    public async Task SW15_khong_nhan_che_do_Duyet()
    {
        await ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var rejected = await service.SetAsync("SW-15", ManagerToggleMode.Approve, ChuTro);
        var rejectedSub = await service.SetAsync("SW-15a", ManagerToggleMode.Approve, ChuTro);

        Assert.False(rejected.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.ModeNotAllowed, rejected.ErrorCode);
        Assert.False(rejectedSub.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.ModeNotAllowed, rejectedSub.ErrorCode);

        var all = await service.GetAllAsync(ChuTro);
        Assert.Equal(ManagerToggleMode.Allow, ModeOf(all, "SW-15"));
        Assert.Equal(ManagerToggleMode.Allow, ModeOf(all, "SW-15a"));
    }

    // ---------- Tiêu chí: Quản lý không đổi được công tắc của mình ----------

    [SqlServerFact]
    public async Task Quan_ly_khong_doi_duoc_cong_tac()
    {
        await ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);

        var change = await service.SetAsync("SW-02", ManagerToggleMode.Allow, QuanLy);
        var read = await service.GetAllAsync(QuanLy);

        Assert.False(change.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.Forbidden, change.ErrorCode);
        Assert.False(read.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.Forbidden, read.ErrorCode);

        var stillApproval = await service.GetAllAsync(ChuTro);
        Assert.Equal(ManagerToggleMode.Approve, ModeOf(stillApproval, "SW-02"));
    }

    // ---------- Tiêu chí: Admin không đọc/ghi công tắc nghiệp vụ ----------

    [SqlServerFact]
    public async Task Admin_khong_doc_ghi_cong_tac_nghiep_vu()
    {
        await ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var admin = new ManagerToggleCaller("admin-1", Roles.Admin);

        var read = await service.GetAllAsync(admin);
        var write = await service.SetAsync("SW-02", ManagerToggleMode.Allow, admin);

        Assert.False(read.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.Forbidden, read.ErrorCode);
        Assert.False(write.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.Forbidden, write.ErrorCode);
    }

    [SqlServerFact]
    public async Task Ma_cong_tac_la_tra_UnknownToggle()
    {
        await ResetAsync();
        await using var db = fixture.CreateContext();

        var result = await NewService(db).SetAsync("SW-99", ManagerToggleMode.Allow, ChuTro);

        Assert.False(result.Succeeded);
        Assert.Equal(ManagerToggleErrorCodes.UnknownToggle, result.ErrorCode);
    }

    // ---------- Tiêu chí: policy đọc công tắc trong DB, Chủ trọ luôn được phép ----------

    [SqlServerFact]
    public async Task Policy_doc_cong_tac_tu_sql()
    {
        await ResetAsync();
        await using var db = fixture.CreateContext();
        var service = NewService(db);
        var policy = NewPolicy(db);

        await service.SetAsync("SW-02", ManagerToggleMode.Deny, ChuTro);
        await service.SetAsync("SW-03", ManagerToggleMode.Allow, ChuTro);
        await service.SetAsync("SW-01", ManagerToggleMode.Approve, ChuTro);

        Assert.Equal(
            RoomChangeDecision.Denied,
            await policy.EvaluateAsync(ManagerAction.SW02, Roles.QuanLy));
        Assert.Equal(
            RoomChangeDecision.Allowed,
            await policy.EvaluateAsync(ManagerAction.SW03, Roles.QuanLy));
        Assert.Equal(
            RoomChangeDecision.RequiresApproval,
            await policy.EvaluateAsync(ManagerAction.SW01, Roles.QuanLy));

        // D4: Chủ trọ không phụ thuộc công tắc, kể cả khi công tắc đang Không thể.
        Assert.Equal(
            RoomChangeDecision.Allowed,
            await policy.EvaluateAsync(ManagerAction.SW02, Roles.ChuTro));
    }

    [SqlServerFact]
    public async Task Sua_phong_cua_quan_ly_di_qua_cong_tac_SW02()
    {
        await ResetAsync();
        await using var db = fixture.CreateContext();
        await NewService(db).SetAsync("SW-02", ManagerToggleMode.Deny, ChuTro);

        var decision = await NewPolicy(db).EvaluateAsync(
            new RoomChangeRequest(ManagerId, Roles.QuanLy, 1, RoomChangeKind.ChangePrice));

        Assert.Equal(RoomChangeDecision.Denied, decision);
    }

    private static ManagerToggleMode ModeOf(
        Web.Features.Settings.DTOs.ManagerToggleListResult result, string code)
    {
        var item = Assert.Single(result.Items, i => i.Code == code);
        return item.Mode;
    }
}
