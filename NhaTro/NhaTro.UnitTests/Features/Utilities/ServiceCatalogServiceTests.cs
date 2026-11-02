using Web.Features.Accounts;
using Web.Features.Properties;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Services;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class ServiceCatalogServiceTests
{
    [Theory]
    [InlineData("Manager")]
    [InlineData("Tenant")]
    [InlineData("Admin")]
    [InlineData("Unknown")]
    public async Task CreateOrUpdateServicePriceAsync_RejectsNonLandlord(string role)
    {
        var service = new ServiceCatalogService(null!);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateOrUpdateServicePriceAsync(
            ValidCreateRequest(),
            new PropertyCaller("user-1", role)));
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Tenant")]
    [InlineData("Admin")]
    public async Task CreateOrUpdateServicePriceAsync_UpdateRejectsNonLandlord(string role)
    {
        var service = new ServiceCatalogService(null!);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateOrUpdateServicePriceAsync(
            1,
            ValidUpdateRequest(),
            new PropertyCaller("user-1", role)));
    }

    [Fact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsInvalidCreateRequest()
    {
        var service = new ServiceCatalogService(null!);
        var request = ValidCreateRequest();
        request.UnitPrice = -1m;
        request.Unit = " ";

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOrUpdateServicePriceAsync(
            request,
            new PropertyCaller("owner-1", Roles.ChuTro)));
    }

    [Fact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsMissingPropertyId()
    {
        var service = new ServiceCatalogService(null!);
        var request = ValidCreateRequest();
        request.PropertyId = null;

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOrUpdateServicePriceAsync(
            request,
            new PropertyCaller("owner-1", Roles.ChuTro)));
    }

    [Fact]
    public async Task CreateOrUpdateServicePriceAsync_RejectsInvalidUpdateRequest()
    {
        var service = new ServiceCatalogService(null!);
        var request = ValidUpdateRequest();
        request.Name = "";

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateOrUpdateServicePriceAsync(
            1,
            request,
            new PropertyCaller("owner-1", Roles.ChuTro)));
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Unknown")]
    public async Task GetServiceCatalogsAsync_RejectsRolesOutsideP5(string role)
    {
        var service = new ServiceCatalogService(null!);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetServiceCatalogsAsync(
            new PropertyCaller("user-1", role)));
    }

    private static CreateServiceCatalogDto ValidCreateRequest() => new()
    {
        PropertyId = 1,
        Name = "Internet",
        Unit = "phòng",
        CalculationMethod = "fixed-monthly",
        UnitPrice = 100_000m,
        EffectiveDate = new DateOnly(2026, 11, 1),
    };

    private static UpdateServicePriceDto ValidUpdateRequest() => new()
    {
        Name = "Internet",
        Unit = "phòng",
        CalculationMethod = "fixed-monthly",
        UnitPrice = 120_000m,
        EffectiveDate = new DateOnly(2026, 12, 1),
    };
}
