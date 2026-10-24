using System.Globalization;
using System.Resources;
using Web.Common.Utils;
using Web.Features.Utilities;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityLocalizationTests
{
    [Fact]
    public void UtilityResources_ResolveVietnameseAndEnglishWithoutChangingVndFormatting()
    {
        var resources = new ResourceManager(
            "Web.Resources.Features.Utilities.UtilityResources",
            typeof(UtilityResources).Assembly);

        var viHeading = resources.GetString("CatalogHeading", CultureInfo.GetCultureInfo("vi-VN"));
        var enHeading = resources.GetString("CatalogHeading", CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Danh mục dịch vụ & Đơn giá", viHeading);
        Assert.Equal("Service Catalog & Tariffs", enHeading);

        // VND formatting must remain invariant regardless of UI culture
        Assert.Equal("1.500.000 ₫", MoneyFormatter.Format(1_500_000m));
        Assert.Equal("250.000 ₫", MoneyFormatter.Format(250_000m));
    }

    [Theory]
    [InlineData("CatalogPageTitle")]
    [InlineData("CatalogBreadcrumb")]
    [InlineData("DemoTopline")]
    [InlineData("PricesHeading")]
    [InlineData("ReadingsHeading")]
    [InlineData("TotalCostCardTitle")]
    [InlineData("LockTariffNotice")]
    public void UtilityResources_KeyExistsInBothCultures(string key)
    {
        var resources = new ResourceManager(
            "Web.Resources.Features.Utilities.UtilityResources",
            typeof(UtilityResources).Assembly);

        var vi = resources.GetString(key, CultureInfo.GetCultureInfo("vi-VN"));
        var en = resources.GetString(key, CultureInfo.GetCultureInfo("en-US"));

        Assert.False(string.IsNullOrWhiteSpace(vi), $"Missing vi-VN value for key: {key}");
        Assert.False(string.IsNullOrWhiteSpace(en), $"Missing en-US value for key: {key}");
    }
}

