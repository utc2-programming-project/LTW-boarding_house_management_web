using Microsoft.EntityFrameworkCore;
using NhaTro.IntegrationTests.Infrastructure;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.DTOs;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Services;

namespace NhaTro.IntegrationTests.Features.PermissionMatrix;

/// <summary>
/// P5 danh mục dịch vụ — sheet Ma trận quyền, dòng 34 (OP33 thiết lập danh mục dịch vụ khác
/// và đơn giá, không phải điện/nước).
/// Nền test tạo NHÀ TRỌ THẬT qua PropertyService và dùng id thật: bản cũ gửi <c>PropertyId = 1</c>
/// trên DB vừa reset nên ô ❌ có thể "đậu" vì nhà trọ không tồn tại chứ không vì vai trò.
/// Quản lý được GIAO nhà A thật (PropertyManagers) nên ô F34 ❌ chứng minh correctly:
/// có phạm vi nhà nhưng vẫn không được quyền thiết lập danh mục — quyền này chỉ Chủ trọ.
/// </summary>
public sealed class PermissionMatrixUtilitiesTests(SqlServerFixture fixture)
    : IClassFixture<SqlServerFixture>
{
    private static PropertyCaller OwnerA => new("owner-a", Roles.ChuTro);
    private static PropertyCaller Manager => new("manager-1", Roles.QuanLy);
    private static PropertyCaller TenantA => new("khach-1", Roles.KhachThue);
    private static PropertyCaller Admin => new("admin-1", Roles.Admin);

    /// <summary>Ô D34 ❌: Admin thiết lập danh mục dịch vụ bị chặn, DB không thêm danh mục.</summary>
    [SqlServerFact]
    public async Task Cell_D34_OP33_Admin_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await CountCatalogsAsync();

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewCatalog(db).CreateOrUpdateServicePriceAsync(NewRequest(data.PropertyAId), Admin));

        Assert.Contains("Chủ trọ", ex.Message);
        Assert.Equal(before, await CountCatalogsAsync());
    }

    /// <summary>
    /// Ô F34 ❌: Quản lý DÙ đã được giao nhà A vẫn bị chặn — quyền thiết lập danh mục chỉ dành
    /// cho Chủ trọ; số danh mục trong DB không đổi.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_F34_OP33_QuanLy_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await CountCatalogsAsync();

        // Chứng minh quản lý có phạm vi nhà A thật (nhìn thấy danh mục nhà A) trước khi bị chặn ghi.
        Assert.NotEmpty((await NewCatalog(db).GetServiceCatalogsAsync(Manager)));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewCatalog(db).CreateOrUpdateServicePriceAsync(NewRequest(data.PropertyAId), Manager));
        Assert.Equal(before, await CountCatalogsAsync());
    }

    /// <summary>Ô G34 ❌: Khách thuê tự thiết lập đơn giá dịch vụ bị chặn, DB không đổi.</summary>
    [SqlServerFact]
    public async Task Cell_G34_OP33_KhachThue_IsDenied()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var before = await CountCatalogsAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => NewCatalog(db).CreateOrUpdateServicePriceAsync(NewRequest(data.PropertyAId), TenantA));
        Assert.Equal(before, await CountCatalogsAsync());
    }

    /// <summary>
    /// Ô E34 ✅: Chủ trọ tạo danh mục + đơn giá cho nhà mình, phiên bản giá ghi thật vào DB
    /// và xuất hiện lại trong danh sách của chính Chủ trọ.
    /// </summary>
    [SqlServerFact]
    public async Task Cell_E34_OP33_ChuTro_IsAllowed()
    {
        var (db, data) = await SeedAsync();
        await using var _ = db;
        var catalog = NewCatalog(db);
        var before = await CountCatalogsAsync();

        var created = await catalog.CreateOrUpdateServicePriceAsync(
            NewRequest(data.PropertyAId, "Giữ xe", 100_000m), OwnerA);

        Assert.Equal(data.PropertyAId, created.PropertyId);
        Assert.Equal("Giữ xe", created.Name);
        Assert.Equal(100_000m, created.UnitPrice);
        Assert.Equal(before + 1, await CountCatalogsAsync());
        Assert.Contains(
            await catalog.GetServiceCatalogsAsync(OwnerA),
            item => item.Id == created.Id && item.Name == "Giữ xe");
    }

    // === helpers ===

    private static ServiceCatalogService NewCatalog(ApplicationDbContext db) => new(db, TimeProvider.System);

    private static CreateServiceCatalogDto NewRequest(
        int propertyId,
        string name = "Điện thoại lễ tân",
        decimal unitPrice = 50_000m) => new()
    {
        Name = name,
        Unit = "lần",
        CalculationMethod = "Fixed",
        UnitPrice = unitPrice,
        EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow.Date),
        PropertyId = propertyId,
    };

    private async Task<int> CountCatalogsAsync()
    {
        await using var read = fixture.CreateContext();
        return await read.ServiceCatalogs.CountAsync();
    }

    // === nền test ===

    private async Task<(ApplicationDbContext Db, SeedData Data)> SeedAsync()
    {
        await fixture.ResetAsync();
        await using (var setup = fixture.CreateContext())
        {
            await SqlServerFixture.SeedAccountAsync(setup, "admin-1", Roles.Admin);
            await SqlServerFixture.SeedAccountAsync(setup, "owner-a", Roles.ChuTro);
            await SqlServerFixture.SeedAccountAsync(setup, "manager-1", Roles.QuanLy);
            await SqlServerFixture.SeedAccountAsync(setup, "khach-1", Roles.KhachThue);
        }

        var db = fixture.CreateContext();
        var properties = new PropertyService(fixture, TimeProvider.System);
        var propertyA = await properties.CreateAsync(new PropertySave { Name = "Nhà A" }, OwnerA);
        Assert.True(propertyA.Succeeded, propertyA.ErrorMessage);

        await new Web.Features.Properties.Services.PropertyManagerService(fixture, TimeProvider.System)
            .AssignAsync(propertyA.Property!.Id, "manager-1", OwnerA);

        // Quản lý cần một danh mục có sẵn của nhà A để ô F34 chứng minh được "có phạm vi đọc
        // nhưng không có quyền ghi" — danh mục do chính Chủ trọ tạo qua service thật.
        await new ServiceCatalogService(db, TimeProvider.System).CreateOrUpdateServicePriceAsync(
            NewRequest(propertyA.Property.Id), OwnerA);

        return (db, new SeedData(propertyA.Property.Id));
    }

    private sealed record SeedData(int PropertyAId);

    // === VHT-101: phủ đủ mọi ô còn thiếu bằng probe hành vi thật ===

    /// <summary>Ô D29 ❌: OP28 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D29_OP28_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D29");

    /// <summary>Ô E29 ✅: OP28 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E29_OP28_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E29");

    /// <summary>Ô F29 ✅: OP28 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F29_OP28_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F29");

    /// <summary>Ô G29 ⚠️: OP28 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G29_OP28_KhachThue_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G29");

    /// <summary>Ô D30 ❌: OP29 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D30_OP29_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D30");

    /// <summary>Ô E30 ✅: OP29 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E30_OP29_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E30");

    /// <summary>Ô F30 ✅: OP29 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F30_OP29_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F30");

    /// <summary>Ô G30 ❌: OP29 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G30_OP29_KhachThue_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G30");

    /// <summary>Ô D32 ❌: OP31 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D32_OP31_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D32");

    /// <summary>Ô E32 ✅: OP31 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E32_OP31_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E32");

    /// <summary>Ô F32 ⚙️: OP31 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F32_OP31_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F32");

    /// <summary>Ô G32 ❌: OP31 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G32_OP31_KhachThue_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G32");

    /// <summary>Ô D33 ❌: OP32 - Admin (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_D33_OP32_Admin_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "D33");

    /// <summary>Ô E33 ✅: OP32 - ChuTro (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_E33_OP32_ChuTro_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "E33");

    /// <summary>Ô F33 ⚠️: OP32 - QuanLy (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_F33_OP32_QuanLy_IsAllowed() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "F33");

    /// <summary>Ô G33 ❌: OP32 - KhachThue (probe hành vi thật).</summary>
    [SqlServerFact]
    public async Task Cell_G33_OP32_KhachThue_IsDenied() =>
        await PermissionMatrixModuleProbe.AssertCellAsync(fixture, "G33");
}
