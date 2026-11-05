using Microsoft.Extensions.Logging.Abstractions;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Approvals;
using Web.Features.Approvals.Services;
using Xunit;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// OP09 "Xem / xử lý hàng đợi chờ duyệt" — sheet Ma trận quyền, dòng 10 (D10 ❌ / E10 ✅ / F10 ⚠️ / G10 ❌).
/// Mỗi test một ô. Ô F10 ⚠️ (Quản lý chỉ thấy yêu cầu của mình, không tự duyệt) cần cả hai case
/// trong/ngoài phạm vi nên do <see cref="PermissionMatrixAutomatedTests"/> chứng minh.
/// </summary>
public sealed class PermissionMatrixApprovalsTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private async Task<(ApplicationDbContext Db, ManagerApprovalService Approvals)> SeedAsync()
    {
        await fixture.ResetAsync();
        var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "admin-1", Roles.Admin);
        await SqlServerFixture.SeedAccountAsync(db, "owner-a", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "manager-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        return (db, PermissionMatrixWorld.NewRealApprovals(db));
    }

    /// <summary>Ô D10 ❌: Admin là vai trò hệ thống (D1), không có hàng đợi duyệt nghiệp vụ.</summary>
    [SqlServerFact]
    public async Task Cell_D10_OP09_Admin_IsDenied()
    {
        var (db, approvals) = await SeedAsync();
        await using var _ = db;

        var inbox = await approvals.ListInboxAsync(new ApprovalCaller("admin-1", Roles.Admin));

        Assert.False(inbox.Succeeded);
        Assert.Equal(ApprovalErrorCodes.Forbidden, inbox.ErrorCode);
    }

    /// <summary>Ô E10 ✅: Chủ trọ mở được hộp thư duyệt.</summary>
    [SqlServerFact]
    public async Task Cell_E10_OP09_ChuTro_IsAllowed()
    {
        var (db, approvals) = await SeedAsync();
        await using var _ = db;

        var inbox = await approvals.ListInboxAsync(new ApprovalCaller("owner-a", Roles.ChuTro));
        Assert.True(inbox.Succeeded, inbox.ErrorMessage);
    }

    /// <summary>Ô G10 ❌: Khách thuê không xem được hàng đợi, kể cả danh sách "của tôi".</summary>
    [SqlServerFact]
    public async Task Cell_G10_OP09_KhachThue_IsDenied()
    {
        var (db, approvals) = await SeedAsync();
        await using var _ = db;

        var inbox = await approvals.ListInboxAsync(new ApprovalCaller("khach-1", Roles.KhachThue));
        var mine = await approvals.ListMineAsync(new ApprovalCaller("khach-1", Roles.KhachThue));

        Assert.False(inbox.Succeeded);
        Assert.Equal(ApprovalErrorCodes.Forbidden, inbox.ErrorCode);
        Assert.False(mine.Succeeded);
        Assert.Equal(ApprovalErrorCodes.Forbidden, mine.ErrorCode);
    }

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô F10 ⚠️: OP09 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F10_OP09_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F10");
}
