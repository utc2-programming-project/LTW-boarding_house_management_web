using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Common.Utils;
using Web.Features.Accounts;
using Web.Features.Utilities;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.MockData;
using Web.Features.Utilities.Services;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class MeterReadingPageServiceTests
{
    private const string OwnerEmail = "chutro@nhatro.local";
    private const string ManagerEmail = "quanly@nhatro.local";
    private const string TenantEmail = "khachthue@nhatro.local";
    private const string AdminEmail = "admin@nhatro.local";

    [Fact]
    public async Task GetCurrentRoleAsync_ChuTro_ReturnsChuTro()
    {
        var service = CreateService(Roles.ChuTro, OwnerEmail);
        var role = await service.GetCurrentRoleAsync();
        Assert.Equal(Roles.ChuTro, role);
    }

    [Fact]
    public async Task GetCurrentRoleAsync_Admin_Throws()
    {
        var service = CreateService(Roles.Admin, AdminEmail);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetCurrentRoleAsync());
    }

    [Fact]
    public async Task CalculateCost_P101_120kWh_6m3_Returns3431900()
    {
        // ARRANGE:
        // P101: Tiền phòng = 3.000.000 ₫
        // Điện tiêu thụ: 1.570 - 1.450 = 120 kWh
        //   - Bậc 1 (50 kWh * 1.984 ₫) = 99.200 ₫
        //   - Bậc 2 (50 kWh * 2.050 ₫) = 102.500 ₫
        //   - Bậc 3 (20 kWh * 2.380 ₫) = 47.600 ₫
        //   => Tổng điện = 249.300 ₫
        // Nước tiêu thụ (Bậc thang HCM theo UtilityTariffSeedData): 130 - 124 = 6 m³
        //   - Bậc 1 (4 m³ * 6.700 ₫) = 26.800 ₫
        //   - Bậc 2 (2 m³ * 12.900 ₫) = 25.800 ₫
        //   => Tổng nước = 52.600 ₫
        // Dịch vụ cố định: Internet (100.000 ₫) + Vệ sinh (30.000 ₫) = 130.000 ₫
        // TỔNG KỲ P101 = 3.000.000 + 249.300 + 52.600 + 130.000 = 3.431.900 ₫
        var service = CreateService(Roles.ChuTro, OwnerEmail);
        var room = UtilitySampleData.CreateDefaultRoomReadings().First(r => r.Id == "room-101");

        // ACT:
        var breakdown = await service.CalculateCostAsync(room, electricEnd: 1570m, waterEnd: 130m);

        // ASSERT:
        Assert.Equal(3_000_000m, breakdown.RoomRent);
        Assert.Equal(120m, breakdown.ElectricityUsage);
        Assert.Equal(249_300m, breakdown.ElectricityCost);
        Assert.Equal(6m, breakdown.WaterUsage);
        Assert.Equal(52_600m, breakdown.WaterCost);
        Assert.Equal(100_000m, breakdown.InternetCost);
        Assert.Equal(30_000m, breakdown.CleaningCost);
        Assert.Equal(130_000m, breakdown.ServiceCost);
        Assert.Equal(3_431_900m, breakdown.TotalCost);
        Assert.Equal("3.431.900 ₫", MoneyFormatter.Format(breakdown.TotalCost));
    }

    [Fact]
    public async Task CalculateCost_MissingReadings_ReturnsOnlyRentAndFixed()
    {
        var service = CreateService(Roles.ChuTro, OwnerEmail);
        var room = UtilitySampleData.CreateDefaultRoomReadings().First(r => r.Id == "room-101");

        // Khi chỉ số bằng start -> tiêu thụ = 0 -> không phát sinh tiền điện nước
        var breakdown = await service.CalculateCostAsync(room, electricEnd: room.ElectricStart, waterEnd: room.WaterStart);

        Assert.Equal(0m, breakdown.ElectricityUsage);
        Assert.Equal(0m, breakdown.ElectricityCost);
        Assert.Equal(0m, breakdown.WaterUsage);
        Assert.Equal(0m, breakdown.WaterCost);
        Assert.Equal(3_130_000m, breakdown.TotalCost);
    }

    [Fact]
    public async Task CalculateCost_ElectricEndLessThanStart_ReturnsError()
    {
        var service = CreateService(Roles.ChuTro, OwnerEmail);

        // ElectricStart của P101 là 1.450 -> nhập 1.400 < 1.450
        var result = await service.SaveReadingAsync(
            "room-101",
            electricEnd: 1400m,
            waterEnd: 130m,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            reason: null);

        Assert.False(result.Success);
        Assert.Equal(MeterReadingErrorCodes.ElectricEndLessThanStart, result.ErrorCode);
        Assert.Contains("lớn hơn hoặc bằng chỉ số cũ", result.ErrorMessage);
    }

    [Fact]
    public async Task CalculateCost_NegativeReadings_ReturnsError()
    {
        var service = CreateService(Roles.ChuTro, OwnerEmail);

        var result = await service.SaveReadingAsync(
            "room-101",
            electricEnd: -10m,
            waterEnd: 130m,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            reason: null);

        Assert.False(result.Success);
        Assert.Equal(MeterReadingErrorCodes.NegativeReading, result.ErrorCode);
        Assert.Contains("không được là số âm", result.ErrorMessage);
    }

    [Fact]
    public async Task SaveReadingAsync_WhenRoleIsKhachThue_ThrowsUnauthorizedAccessException()
    {
        var service = CreateService(Roles.KhachThue, TenantEmail);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.SaveReadingAsync(
                "room-101",
                electricEnd: 1570m,
                waterEnd: 130m,
                new DateOnly(2026, 10, 1),
                new DateOnly(2026, 10, 31),
                reason: null));
    }

    [Fact]
    public async Task SaveReadingAsync_WhenLockPeriod_SetsStatusToLocked()
    {
        var service = CreateService(Roles.ChuTro, OwnerEmail);

        var result = await service.SaveReadingAsync(
            "room-101",
            electricEnd: 1570m,
            waterEnd: 130m,
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            reason: "Chốt kỳ tháng 10",
            isLock: true);

        Assert.True(result.Success);
        Assert.NotNull(result.Room);
        Assert.Equal("locked", result.Room.Status);
    }

    [Fact]
    public async Task CalculateCost_AbnormalUsageOver300Percent_SetsAbnormalFlags()
    {
        var service = CreateService(Roles.ChuTro, OwnerEmail);
        var rooms = UtilitySampleData.CreateDefaultRoomReadings();
        var p101 = rooms.First(r => r.Id == "room-101"); // PreviousElectricUsage = 100, PreviousWaterUsage = 5
        var p102 = rooms.First(r => r.Id == "room-102"); // PreviousElectricUsage = 25, PreviousWaterUsage = 5

        // P101: 120 kWh (120% <= 300%), 6 m3 (120% <= 300%) -> normal
        var normalBreakdown = await service.CalculateCostAsync(p101, electricEnd: 1570m, waterEnd: 130m);
        Assert.False(normalBreakdown.HasAbnormalElectricUsage);
        Assert.False(normalBreakdown.HasAbnormalWaterUsage);

        // P102: 1065 - 980 = 85 kWh vs prev 25 kWh (340% > 300%) -> abnormal electric
        // Water: 105 - 85 = 20 m3 vs prev 5 m3 (400% > 300%) -> abnormal water
        var abnormalBreakdown = await service.CalculateCostAsync(p102, electricEnd: 1065m, waterEnd: 105m);
        Assert.True(abnormalBreakdown.HasAbnormalElectricUsage);
        Assert.True(abnormalBreakdown.HasAbnormalWaterUsage);
    }

    private static MeterReadingPageService CreateService(string role, string email)
    {
        var authStateProvider = new TestAuthStateProvider(role, email);
        var sampleState = new UtilitySampleStateService(new FixedTimeProvider(
            new DateTimeOffset(2026, 10, 24, 17, 0, 0, TimeSpan.Zero)));
        var serviceCollection = new ServiceCollection();
        var scopeFactory = serviceCollection.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();

        return new MeterReadingPageService(
            authStateProvider,
            sampleState,
            new DummyUtilityTariffService(),
            scopeFactory,
            NullLogger<MeterReadingPageService>.Instance);
    }

    private sealed class TestAuthStateProvider(string role, string email) : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, email),
                new(ClaimTypes.Email, email),
            };
            if (!string.IsNullOrEmpty(role))
            {
                claims.Add(new(ClaimTypes.Role, role));
            }
            var identity = new ClaimsIdentity(claims, "TestAuth");
            return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identity)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class DummyUtilityTariffService : IUtilityTariffService
    {
        public Task<TariffDto> CalculateForPeriodAsync(
            TariffType type,
            string scopeCode,
            DateOnly periodStart,
            DateOnly periodEnd,
            decimal quantity,
            int residentCount = 1,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Using fallback rules in tests.");

        public Task<TariffDto> SnapshotForPeriodAsync(
            int billingPeriodId,
            TariffType type,
            string scopeCode,
            DateOnly periodStart,
            DateOnly periodEnd,
            decimal quantity,
            int residentCount = 1,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TariffDto?> GetPeriodSnapshotAsync(
            int snapshotId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<TariffDto?>(null);
    }
}
