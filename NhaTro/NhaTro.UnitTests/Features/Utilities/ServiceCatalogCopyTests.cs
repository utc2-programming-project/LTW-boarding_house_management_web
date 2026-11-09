using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Web.Data;
using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Properties.Entities;
using Web.Features.Utilities.Entities;
using Web.Features.Utilities.Services;

namespace NhaTro.UnitTests.Features.Utilities;

/// <summary>
/// PLAN-012: sao chép danh mục/giá từ nhà trọ nguồn sang nhà trọ đích.
/// Chỉ lấy danh mục đang dùng, chỉ mang bản giá đang hiệu lực, bỏ qua trùng tên.
/// </summary>
public sealed class ServiceCatalogCopyTests
{
    private static readonly FakeTimeProvider Clock =
        new(new DateTimeOffset(2026, 11, 9, 2, 0, 0, TimeSpan.Zero)); // 09:00 giờ Việt Nam

    private const string OwnerId = "owner-1";
    private const int SourcePropertyId = 1;
    private const int TargetPropertyId = 2;

    private static readonly PropertyCaller Owner = new(OwnerId, Roles.ChuTro);

    [Fact]
    public async Task Copy_chi_mang_ban_gia_hieu_luc_va_bo_qua_trung_ten()
    {
        await using var db = CreateDbContext();
        Seed(db);
        await db.SaveChangesAsync();

        var service = new ServiceCatalogService(db, Clock);
        var result = await service.CopyPricesFromPropertyAsync(SourcePropertyId, TargetPropertyId, Owner);

        Assert.Equal(1, result.Copied); // chỉ "Vệ sinh" đủ điều kiện
        Assert.Equal(1, result.Skipped); // "internet" trùng tên (không phân biệt hoa/thường)

        db.ChangeTracker.Clear();
        var targetCatalogs = await db.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
            .Where(catalog => catalog.PropertyId == TargetPropertyId)
            .ToListAsync();

        Assert.Equal(2, targetCatalogs.Count); // bản gốc của đích + bản sao chép
        var copied = targetCatalogs.Single(catalog =>
            catalog.PriceHistory.Any(version => version.Name == "Vệ sinh"));
        Assert.True(copied.IsActive);
        Assert.False(copied.IsReadOnly);
        var version = Assert.Single(copied.PriceHistory);
        Assert.Equal(50_000m, version.UnitPrice);
        Assert.Equal(new DateOnly(2026, 11, 9), version.EffectiveDate);
        Assert.Null(version.EffectiveTo);
        Assert.Contains("Sao chép", version.Note ?? string.Empty);
    }

    [Fact]
    public async Task Copy_khong_mang_theo_lich_su_gia_cu()
    {
        await using var db = CreateDbContext();
        Seed(db);
        await db.SaveChangesAsync();

        var summary = await db.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
            .SingleAsync(catalog => catalog.PropertyId == SourcePropertyId
                && catalog.PriceHistory.Any(version => version.Name == "Vệ sinh"));
        Assert.Equal(2, summary.PriceHistory.Count); // nguồn có 2 bản giá

        var service = new ServiceCatalogService(db, Clock);
        await service.CopyPricesFromPropertyAsync(SourcePropertyId, TargetPropertyId, Owner);

        db.ChangeTracker.Clear();
        var targetCatalogs = await db.ServiceCatalogs
            .AsNoTracking()
            .Include(catalog => catalog.PriceHistory)
            .Where(catalog => catalog.PropertyId == TargetPropertyId)
            .ToListAsync();
        var copied = targetCatalogs.Single(catalog => catalog.PriceHistory.Any(version => version.Name == "Vệ sinh"));
        Assert.Single(copied.PriceHistory);
    }

    [Fact]
    public async Task Copy_tu_chinh_minh_bi_tu_choi()
    {
        await using var db = CreateDbContext();
        Seed(db);
        await db.SaveChangesAsync();
        var service = new ServiceCatalogService(db, Clock);

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CopyPricesFromPropertyAsync(SourcePropertyId, SourcePropertyId, Owner));

        Assert.Equal("ErrorCopySameProperty", error.Data[ServiceCatalogErrorKeys.ResourceKey]);
    }

    [Fact]
    public async Task Copy_tu_nha_tro_khong_thuoc_quyen_bi_tu_choi()
    {
        await using var db = CreateDbContext();
        Seed(db);

        // Nhà trọ nguồn thuộc chủ khác.
        db.Properties.Add(new Property { Id = 3, Name = "Trọ người khác", OwnerId = "owner-2" });
        await db.SaveChangesAsync();
        var service = new ServiceCatalogService(db, Clock);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CopyPricesFromPropertyAsync(3, TargetPropertyId, Owner));

        Assert.Equal("ErrorCopySourceForbidden", error.Data[ServiceCatalogErrorKeys.ResourceKey]);
    }

    [Theory]
    [InlineData(Roles.QuanLy)]
    [InlineData(Roles.KhachThue)]
    public async Task Copy_chi_chu_tro_moi_duoc_phep(string role)
    {
        var service = new ServiceCatalogService(null!, Clock);

        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CopyPricesFromPropertyAsync(
                SourcePropertyId, TargetPropertyId, new PropertyCaller(OwnerId, role)));

        Assert.Equal("ErrorOwnerOnlyService", error.Data[ServiceCatalogErrorKeys.ResourceKey]);
    }

    private static void Seed(ApplicationDbContext db)
    {
        db.Properties.AddRange(
            new Property { Id = SourcePropertyId, Name = "Trọ nguồn", OwnerId = OwnerId },
            new Property { Id = TargetPropertyId, Name = "Trọ đích", OwnerId = OwnerId });

        // Nguồn: 1 danh mục đủ điều kiện (Vệ sinh) + 1 trùng tên với đích (Internet) + 1 có 2 bản giá.
        var internet = Catalog(SourcePropertyId, "Internet", 120_000m, new DateOnly(2026, 1, 1));
        var cleaning = Catalog(SourcePropertyId, "Vệ sinh", 50_000m, new DateOnly(2026, 1, 1));
        cleaning.PriceHistory.Add(Version("Vệ sinh", 40_000m, new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1)));

        // Các trường hợp phải bị bỏ qua.
        var upcoming = Catalog(SourcePropertyId, "Giữ xe", 80_000m, new DateOnly(2027, 1, 1));
        var expired = Catalog(SourcePropertyId, "Điện", 3_500m, new DateOnly(2025, 1, 1), new DateOnly(2026, 1, 1));
        var readOnly = Catalog(SourcePropertyId, "Biểu giá hệ thống", 3_000m, new DateOnly(2025, 6, 1));
        readOnly.IsReadOnly = true;
        var inactive = Catalog(SourcePropertyId, "Nước", 20_000m, new DateOnly(2025, 6, 1));
        inactive.IsActive = false;
        var deleted = Catalog(SourcePropertyId, "Rác", 15_000m, new DateOnly(2025, 6, 1));
        deleted.IsDeleted = true;

        // Đích đã có "internet" → bản sao trùng tên bị bỏ qua.
        var existing = Catalog(TargetPropertyId, "internet", 130_000m, new DateOnly(2026, 1, 1));

        db.ServiceCatalogs.AddRange(internet, cleaning, upcoming, expired, readOnly, inactive, deleted, existing);
    }

    private static ServiceCatalog Catalog(int propertyId, string name, decimal price, DateOnly from, DateOnly? to = null) =>
        new()
        {
            PropertyId = propertyId,
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            PriceHistory = { Version(name, price, from, to) },
        };

    private static ServicePriceHistory Version(string name, decimal price, DateOnly from, DateOnly? to = null) =>
        new()
        {
            Name = name,
            Unit = "Tháng",
            CalculationMethod = "Cố định",
            UnitPrice = price,
            EffectiveDate = from,
            EffectiveTo = to,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedByUserId = OwnerId,
        };

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
