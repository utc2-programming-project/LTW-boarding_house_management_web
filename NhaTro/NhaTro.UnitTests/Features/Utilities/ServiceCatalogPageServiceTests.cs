using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Features.Accounts;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Services;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class ServiceCatalogPageServiceTests
{
    [Theory]
    [InlineData("fixed-monthly", ServiceRateType.Flat, ServiceTypeCategory.FixedMonthly)]
    [InlineData("tiered", ServiceRateType.Tiered, ServiceTypeCategory.Meter)]
    [InlineData("meter", ServiceRateType.Flat, ServiceTypeCategory.Meter)]
    [InlineData("custom", ServiceRateType.Flat, ServiceTypeCategory.Custom)]
    [InlineData("unknown-method", ServiceRateType.Flat, ServiceTypeCategory.Custom)]
    public void MapCalculationMethod_MapsCorrectly(
        string method, ServiceRateType expectedRateType, ServiceTypeCategory expectedCategory)
    {
        var (rateType, category) = ServiceCatalogPageService.MapCalculationMethod(method);

        Assert.Equal(expectedRateType, rateType);
        Assert.Equal(expectedCategory, category);
    }

    [Theory]
    [InlineData("phòng", ServiceUnitCategory.Room)]
    [InlineData("room", ServiceUnitCategory.Room)]
    [InlineData("người", ServiceUnitCategory.Person)]
    [InlineData("person", ServiceUnitCategory.Person)]
    [InlineData("xe", ServiceUnitCategory.Vehicle)]
    [InlineData("vehicle", ServiceUnitCategory.Vehicle)]
    [InlineData("lượt", ServiceUnitCategory.Visit)]
    [InlineData("visit", ServiceUnitCategory.Visit)]
    [InlineData("kwh", ServiceUnitCategory.KilowattHour)]
    [InlineData("m³", ServiceUnitCategory.CubicMeter)]
    [InlineData("m3", ServiceUnitCategory.CubicMeter)]
    [InlineData("random", ServiceUnitCategory.Room)]
    public void MapUnit_MapsCorrectly(string unit, ServiceUnitCategory expected)
    {
        var result = ServiceCatalogPageService.MapUnit(unit);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CheckWriteSupport_ReturnsAllowedForSprint2C()
    {
        var service = CreateServiceWithRole(Roles.ChuTro);

        var (canWrite, reason) = service.CheckWriteSupport();

        Assert.True(canWrite);
        Assert.Null(reason);
    }

    [Fact]
    public async Task AddServiceAsync_RejectsNonChuTro()
    {
        var service = CreateServiceWithRole(Roles.QuanLy);
        var request = new CreateServiceRequest
        {
            Name = "Dịch vụ giặt sấy",
            Type = ServiceTypeCategory.Custom,
            Unit = ServiceUnitCategory.Visit,
            Rate = 20000m,
            EffectiveFrom = new DateOnly(2026, 11, 1),
            BuildingId = "1",
        };

        var (success, error, item) = await service.AddServiceAsync(request);

        Assert.False(success);
        Assert.Null(item);
        Assert.NotNull(error);
        Assert.Equal("ErrorOwnerOnlyService", error);
    }

    [Fact]
    public async Task AddServiceAsync_RejectsMissingProperty()
    {
        var service = CreateServiceWithRole(Roles.ChuTro);
        var request = new CreateServiceRequest
        {
            Name = "Dịch vụ giặt sấy",
            Type = ServiceTypeCategory.Custom,
            Unit = ServiceUnitCategory.Visit,
            Rate = 20000m,
            EffectiveFrom = new DateOnly(2026, 11, 1),
            BuildingId = "all", // "all" is rejected because global catalog is not allowed
        };

        var (success, error, item) = await service.AddServiceAsync(request);

        Assert.False(success);
        Assert.Null(item);
        Assert.NotNull(error);
        Assert.Equal("ErrorBuildingRequired", error);
    }

    [Fact]
    public async Task AddServiceAsync_RejectsEmptyName()
    {
        var service = CreateServiceWithRole(Roles.ChuTro);
        var request = new CreateServiceRequest
        {
            Name = "   ",
            Type = ServiceTypeCategory.Custom,
            Unit = ServiceUnitCategory.Visit,
            Rate = 20000m,
            EffectiveFrom = new DateOnly(2026, 11, 1),
            BuildingId = "1",
        };

        var (success, error, item) = await service.AddServiceAsync(request);

        Assert.False(success);
        Assert.Null(item);
        Assert.NotNull(error);
        Assert.Equal("ErrorNameIsRequired", error);
    }

    [Fact]
    public async Task UpdateServicePriceAsync_RejectsNonChuTro()
    {
        var service = CreateServiceWithRole(Roles.QuanLy);
        var request = new UpdatePriceRequest
        {
            ServiceId = "1",
            Unit = ServiceUnitCategory.KilowattHour,
            NewRate = 3500m,
            EffectiveFrom = new DateOnly(2026, 12, 1),
            BuildingScope = "1",
        };

        var (success, error, version) = await service.UpdateServicePriceAsync(request);

        Assert.False(success);
        Assert.Null(version);
        Assert.NotNull(error);
        Assert.Equal("ErrorOwnerOnlyService", error);
    }

    [Fact]
    public async Task UpdateServicePriceAsync_RejectsInvalidServiceId()
    {
        var service = CreateServiceWithRole(Roles.ChuTro);
        var request = new UpdatePriceRequest
        {
            ServiceId = "invalid-id",
            Unit = ServiceUnitCategory.KilowattHour,
            NewRate = 3500m,
            EffectiveFrom = new DateOnly(2026, 12, 1),
            BuildingScope = "1",
        };

        var (success, error, version) = await service.UpdateServicePriceAsync(request);

        Assert.False(success);
        Assert.Null(version);
        Assert.NotNull(error);
        Assert.Equal("ErrorInvalidServiceId", error);
    }

    [Fact]
    public void IsSampleData_ReturnsFalseForPersistedService()
    {
        var service = CreateServiceWithRole(Roles.ChuTro);

        Assert.False(service.IsSampleData);
    }

    [Fact]
    public void MapToServiceItemDto_MapsFieldsAccurately()
    {
        var today = new DateOnly(2026, 11, 2);
        var catalog = new ServiceCatalogDto(
            5,
            12,
            "Điện sinh hoạt",
            "kWh",
            "tiered",
            3500m,
            new DateOnly(2026, 10, 1),
            true,
            new List<ServicePriceVersionDto>());

        var dto = ServiceCatalogPageService.MapToServiceItemDto(catalog, "Nhà trọ Hòa An", today);

        Assert.Equal("5", dto.Id);
        Assert.Equal("SRV05", dto.Code);
        Assert.Equal("Điện sinh hoạt", dto.Name);
        Assert.Equal(ServiceTypeCategory.Meter, dto.Type);
        Assert.Equal(ServiceUnitCategory.KilowattHour, dto.Unit);
        Assert.Equal(ServiceRateType.Tiered, dto.RateType);
        Assert.Equal(3500m, dto.CurrentRate);
        Assert.Equal(new DateOnly(2026, 10, 1), dto.EffectiveFrom);
        Assert.Null(dto.EffectiveTo);
        Assert.Equal(PriceVersionStatus.Active, dto.PriceStatus);
        Assert.True(dto.IsReadOnly);
        Assert.False(dto.IsSessionCreated);
        Assert.Equal("12", dto.BuildingId);
        Assert.Equal("Nhà trọ Hòa An", dto.BuildingName);
    }

    [Fact]
    public void MapToServiceItemDto_MarksUpcomingWhenEffectiveDateInFuture()
    {
        var today = new DateOnly(2026, 11, 2);
        var catalog = new ServiceCatalogDto(
            7,
            null,
            "Nước sinh hoạt",
            "m3",
            "meter",
            15000m,
            new DateOnly(2026, 12, 1), // in future
            false,
            new List<ServicePriceVersionDto>());

        var dto = ServiceCatalogPageService.MapToServiceItemDto(catalog, "Tất cả nhà trọ", today);

        Assert.Equal(PriceVersionStatus.Upcoming, dto.PriceStatus);
        Assert.Equal("all", dto.BuildingId);
    }

    [Fact]
    public void MapToPriceVersionDto_MapsFieldsAccurately()
    {
        var today = new DateOnly(2026, 11, 2);
        var catalog = new ServiceCatalogDto(
            3,
            8,
            "Internet cáp quang",
            "phòng",
            "fixed-monthly",
            100000m,
            new DateOnly(2026, 10, 1),
            false,
            new List<ServicePriceVersionDto>());

        var version = new ServicePriceVersionDto(
            19,
            "Internet cáp quang",
            "phòng",
            "fixed-monthly",
            120000m,
            new DateOnly(2026, 11, 1));

        var dto = ServiceCatalogPageService.MapToPriceVersionDto(
            catalog, version, "8", "Nhà Trọ Ánh Dương", today);

        Assert.Equal("pv-3-19", dto.Id);
        Assert.Equal("3", dto.ServiceId);
        Assert.Equal("Internet cáp quang", dto.ServiceName);
        Assert.Equal(120000m, dto.Rate);
        Assert.Equal(ServiceUnitCategory.Room, dto.Unit);
        Assert.Equal(ServiceRateType.Flat, dto.RateType);
        Assert.Equal(new DateOnly(2026, 11, 1), dto.EffectiveFrom);
        Assert.Null(dto.EffectiveTo);
        Assert.Equal(PriceVersionStatus.Active, dto.Status);
        Assert.Equal("8", dto.BuildingScope);
        Assert.Equal("Nhà Trọ Ánh Dương", dto.BuildingName);
        Assert.False(dto.IsSessionCreated);
    }

    [Fact]
    public async Task GetCurrentRoleAsync_ThrowsForAdminCaller()
    {
        var service = CreateServiceWithRole(Roles.Admin);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetCurrentRoleAsync());
    }

    [Theory]
    [InlineData(Roles.ChuTro)]
    [InlineData(Roles.QuanLy)]
    [InlineData(Roles.KhachThue)]
    public async Task GetCurrentRoleAsync_AcceptsAllowedRoles(string role)
    {
        var service = CreateServiceWithRole(role);

        var resolved = await service.GetCurrentRoleAsync();

        Assert.Equal(role, resolved);
    }

    private static ServiceCatalogPageService CreateServiceWithRole(string role)
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user-guid-123"));
        identity.AddClaim(new Claim(ClaimTypes.Name, "testuser"));
        identity.AddClaim(new Claim(ClaimTypes.Role, role));
        var user = new ClaimsPrincipal(identity);

        var authProvider = new TestAuthenticationStateProvider(user);
        var services = new ServiceCollection();
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        return new ServiceCatalogPageService(
            authProvider,
            scopeFactory,
            TimeProvider.System,
            NullLogger<ServiceCatalogPageService>.Instance,
            new NhaTro.UnitTests.TestSupport.UtilityKeyLocalizer());
    }

    private sealed class TestAuthenticationStateProvider(ClaimsPrincipal user) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(user));
    }
}
