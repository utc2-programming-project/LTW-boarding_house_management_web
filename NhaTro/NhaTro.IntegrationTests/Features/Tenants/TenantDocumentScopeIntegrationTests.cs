using NhaTro.IntegrationTests.Infrastructure;
using NhaTro.IntegrationTests.TestSupport;
using Web.Features.Accounts;
using Web.Features.Tenants;
using Web.Features.Tenants.Documents;
using Web.Features.Tenants.DTOs;

namespace NhaTro.IntegrationTests.Features.Tenants;

/// <summary>
/// VHT-26: phạm vi giấy tờ P3 trên SQL Server thật — Chủ trọ, khách thuê, Quản lý (D9) và Admin.
/// </summary>
public sealed class TenantDocumentScopeIntegrationTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static readonly TenantCaller Manager = new("chu-tro-1", Roles.ChuTro);

    [SqlServerFact]
    public async Task Pham_vi_giay_to_dung_theo_vai_tro_va_ho_so()
    {
        await fixture.ResetAsync();
        await using var db = fixture.CreateContext();
        await SqlServerFixture.SeedAccountAsync(db, "chu-tro-1", Roles.ChuTro);
        await SqlServerFixture.SeedAccountAsync(db, "ql-1", Roles.QuanLy);
        await SqlServerFixture.SeedAccountAsync(db, "khach-1", Roles.KhachThue);
        await SqlServerFixture.SeedAccountAsync(db, "khach-2", Roles.KhachThue);

        var tenantService = new TenantService(
            db, TimeProvider.System, new AllowAllManagerTogglePolicy());
        var created = await tenantService.CreateAsync(new CreateTenantRequest
        {
            FullName = "An",
            PhoneNumber = "0901234567",
        }, Manager);
        Assert.True(created.Succeeded, created.ErrorMessage);
        var tenantId = created.Tenant!.Id;

        var linked = await tenantService.LinkAccountAsync(
            tenantId, "khach-1", created.Tenant.RowVersion, Manager);
        Assert.True(linked.Succeeded, linked.ErrorMessage);

        var scope = new TenantDocumentScopeService(db, new NoManagerTenantScope());

        Assert.Equal(
            TenantDocumentAccessLevel.Owner,
            await scope.ResolveAsync(tenantId, Manager));
        Assert.Equal(
            TenantDocumentAccessLevel.Own,
            await scope.ResolveAsync(tenantId, new TenantCaller("khach-1", Roles.KhachThue)));
        Assert.Equal(
            TenantDocumentAccessLevel.None,
            await scope.ResolveAsync(tenantId, new TenantCaller("khach-2", Roles.KhachThue)));
        Assert.Equal(
            TenantDocumentAccessLevel.None,
            await scope.ResolveAsync(tenantId, new TenantCaller("admin-1", Roles.Admin)));
        Assert.Equal(
            TenantDocumentAccessLevel.None,
            await scope.ResolveAsync(tenantId, new TenantCaller("khong-ton-tai", Roles.KhachThue)));

        // Chưa có bảng phân công VHT-24: Quản lý bị từ chối theo deny-by-default.
        Assert.Equal(
            TenantDocumentAccessLevel.None,
            await scope.ResolveAsync(tenantId, new TenantCaller("ql-1", Roles.QuanLy)));

        // Khi VHT-24 cung cấp phạm vi, Quản lý được giao tòa nhà thấy giấy tờ nhưng không xuất.
        var managerWithAssignment = new TenantDocumentScopeService(db, new AllowManagerScope());
        var managerAccess = await managerWithAssignment.ResolveAsync(
            tenantId, new TenantCaller("ql-1", Roles.QuanLy));
        Assert.Equal(TenantDocumentAccessLevel.Manager, managerAccess);
        Assert.True(TenantDocumentPermissionRules.CanView(managerAccess));
        Assert.False(TenantDocumentPermissionRules.CanExport(managerAccess));

        // Hồ sơ không tồn tại: không lộ quyền.
        Assert.Equal(
            TenantDocumentAccessLevel.None,
            await scope.ResolveAsync(tenantId + 1, Manager));
    }

    private sealed class AllowManagerScope : IManagerTenantScope
    {
        public Task<bool> IsTenantInScopeAsync(
            string managerUserId,
            int tenantId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
