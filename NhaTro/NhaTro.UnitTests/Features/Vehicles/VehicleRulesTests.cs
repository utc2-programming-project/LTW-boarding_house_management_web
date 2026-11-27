using Web.Features.Vehicles.Entities;
using Web.Features.Vehicles.Enums;
using Web.Features.Vehicles.Rules;

namespace NhaTro.UnitTests.Features.Vehicles;

public sealed class VehicleRulesTests
{
    [Theory]
    [InlineData("59-X1 123.45", "59X112345")]
    [InlineData(" 29a - 012.34 ", "29A01234")]
    [InlineData("59-MĐ1 111.22", "59MD111122")]
    [InlineData("43K1/555.66", "43K155566")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void NormalizePlateNumberStripsSeparatorsAndUppercases(string input, string expected)
    {
        var actual = VehicleRules.NormalizePlateNumber(input);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("59X112345", true)]
    [InlineData("29MĐ112345", false)] // Non-ASCII letter not allowed after strict ASCII check
    [InlineData("29MD112345", true)]
    [InlineData("1234", false)] // Too short (< 5)
    [InlineData("ABCDEFG", false)] // No digits
    [InlineData("59X11234567890123", false)] // Too long (> 15)
    public void IsValidPlateNumberValidatesLengthAndCharacters(string normalized, bool expected)
    {
        Assert.Equal(expected, VehicleRules.IsValidPlateNumber(normalized));
    }

    [Fact]
    public void GenerateQrCodeTokenCreatesPrefixedNonPiiToken()
    {
        var token1 = VehicleRules.GenerateQrCodeToken();
        var token2 = VehicleRules.GenerateQrCodeToken();

        Assert.StartsWith("VH-", token1);
        Assert.Equal(19, token1.Length);
        Assert.NotEqual(token1, token2);
    }

    [Fact]
    public void DefaultMonthlyFeesMatchSpecDvh03()
    {
        var setting = new VehiclePropertySetting();

        Assert.Equal(50_000m, VehicleRules.CalculateMonthlyFee(VehicleType.ElectricMotorbike, setting));
        Assert.Equal(80_000m, VehicleRules.CalculateMonthlyFee(VehicleType.ManualMotorbike, setting));
        Assert.Equal(100_000m, VehicleRules.CalculateMonthlyFee(VehicleType.Scooter, setting));
    }

    [Fact]
    public void CalculateMonthlyFeeRespectsZoneFreeAndOverrideWhenZonesEnabled()
    {
        var setting = new VehiclePropertySetting
        {
            EnableParkingZones = true,
            ElectricMotorbikeMonthlyFee = 50_000m,
            ManualMotorbikeMonthlyFee = 80_000m,
            ScooterMonthlyFee = 100_000m,
        };

        var freeZone = new ParkingZone { IsActive = true, IsFree = true };
        var overrideZone = new ParkingZone { IsActive = true, IsFree = false, OverrideMonthlyFee = 120_000m };
        var normalZone = new ParkingZone { IsActive = true, IsFree = false, OverrideMonthlyFee = null };

        Assert.Equal(0m, VehicleRules.CalculateMonthlyFee(VehicleType.Scooter, setting, freeZone));
        Assert.Equal(120_000m, VehicleRules.CalculateMonthlyFee(VehicleType.ManualMotorbike, setting, overrideZone));
        Assert.Equal(80_000m, VehicleRules.CalculateMonthlyFee(VehicleType.ManualMotorbike, setting, normalZone));

        // Khi tắt EnableParkingZones thì luôn tính theo đơn giá loại xe
        setting.EnableParkingZones = false;
        Assert.Equal(100_000m, VehicleRules.CalculateMonthlyFee(VehicleType.Scooter, setting, freeZone));
    }

    [Fact]
    public void IsVehicleTypeAllowedEnforcesZoneRestrictions()
    {
        var indoorNoElectric = AllowedVehicleTypeFlags.ManualMotorbike | AllowedVehicleTypeFlags.Scooter;

        Assert.False(VehicleRules.IsVehicleTypeAllowed(VehicleType.ElectricMotorbike, indoorNoElectric));
        Assert.True(VehicleRules.IsVehicleTypeAllowed(VehicleType.ManualMotorbike, indoorNoElectric));
        Assert.True(VehicleRules.IsVehicleTypeAllowed(VehicleType.Scooter, indoorNoElectric));
    }

    [Fact]
    public void BuildInvoiceLineDescriptionSummarizesCountsByType()
    {
        var types = new[]
        {
            VehicleType.ElectricMotorbike,
            VehicleType.ManualMotorbike,
            VehicleType.ManualMotorbike,
            VehicleType.Scooter,
        };

        var description = VehicleRules.BuildInvoiceLineDescription(types);

        Assert.Equal("Phí gửi xe (1 xe máy điện, 2 xe máy số, 1 xe tay ga)", description);
        Assert.True(VehicleRules.IsParkingFeeLine(description));
    }

    [Fact]
    public void IsParkingFeeLineRecognizesBothVietnameseAndEnglishPrefixes()
    {
        Assert.True(VehicleRules.IsParkingFeeLine("Phí gửi xe (2 xe máy số)"));
        Assert.True(VehicleRules.IsParkingFeeLine("  phí GỬI xe (1 xe tay ga)"));
        Assert.True(VehicleRules.IsParkingFeeLine("Parking fee (2 motorbikes)"));
        Assert.True(VehicleRules.IsParkingFeeLine("Parking fee (80,000đ)"));
        Assert.False(VehicleRules.IsParkingFeeLine("Tiền điện tháng 11"));
        Assert.False(VehicleRules.IsParkingFeeLine(null));
    }

    [Fact]
    public void BuildInvoiceLineDescriptionAcceptsLocalizedPrefix()
    {
        var typeLabel = (VehicleType _) => "scooter";
        var description = VehicleRules.BuildInvoiceLineDescription(
            new[] { (VehicleType.Scooter, 100_000m) },
            typeLabel,
            VehicleRules.ParkingInvoiceLinePrefixEn);

        Assert.StartsWith("Parking fee (", description);
        Assert.True(VehicleRules.IsParkingFeeLine(description));

        // Không truyền tiền tố → giữ nhãn tiếng Việt mặc định để nhận diện ổn định.
        var fallback = VehicleRules.BuildInvoiceLineDescription(
            new[] { (VehicleType.Scooter, 100_000m) }, typeLabel);
        Assert.StartsWith(VehicleRules.ParkingInvoiceLinePrefix, fallback);
    }
}
