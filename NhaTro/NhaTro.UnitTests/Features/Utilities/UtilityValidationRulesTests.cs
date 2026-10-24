using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Rules;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityValidationRulesTests
{
    [Fact]
    public void ValidateCreateService_RejectsEmptyNameAndZeroRate()
    {
        var req = new CreateServiceRequest
        {
            Name = "  ",
            Rate = 0m,
        };

        var issues = UtilityDraftRules.ValidateCreateService(req);

        Assert.Contains(issues, i => i.Error == UtilityValidationError.NameIsRequired);
        Assert.Contains(issues, i => i.Error == UtilityValidationError.PriceMustBePositive);
    }

    [Fact]
    public void ValidateCreateService_AcceptsValidInput()
    {
        var req = new CreateServiceRequest
        {
            Name = "Giặt sấy tự động",
            Rate = 50_000m,
            Unit = "Lần",
        };

        var issues = UtilityDraftRules.ValidateCreateService(req);
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public void ValidateUpdatePrice_RejectsNonPositiveRate(decimal rate)
    {
        var req = new UpdatePriceRequest
        {
            ServiceId = "srv-3",
            NewRate = rate,
            EffectiveFrom = new DateOnly(2026, 12, 1),
        };

        var issues = UtilityDraftRules.ValidateUpdatePrice(req);
        Assert.Contains(issues, i => i.Error == UtilityValidationError.PriceMustBePositive);
    }

    [Fact]
    public void ValidateUpdatePrice_RejectsEffectiveToBeforeEffectiveFrom()
    {
        var req = new UpdatePriceRequest
        {
            ServiceId = "srv-3",
            NewRate = 120_000m,
            EffectiveFrom = new DateOnly(2026, 12, 1),
            EffectiveTo = new DateOnly(2026, 11, 30),
        };

        var issues = UtilityDraftRules.ValidateUpdatePrice(req);
        Assert.Contains(issues, i => i.Error == UtilityValidationError.EffectiveToMustBeAfterOrOnEffectiveFrom);
    }

    [Fact]
    public void ValidateMeterReading_RejectsEndReadingLowerThanStart()
    {
        var issues = UtilityDraftRules.ValidateMeterReading(
            startReading: 1_450m,
            endReading: 1_400m);

        Assert.Contains(issues, i => i.Error == UtilityValidationError.MeterReadingMustBeGreaterOrEqualStart);
    }

    [Fact]
    public void ValidateMeterReading_RejectsNegativeEndReading()
    {
        var issues = UtilityDraftRules.ValidateMeterReading(
            startReading: 0m,
            endReading: -10m);

        Assert.Contains(issues, i => i.Error == UtilityValidationError.MeterReadingCannotBeNegative);
    }

    [Fact]
    public void ValidateMeterReading_RequiresReasonWhenReadingAdjusted()
    {
        var issues = UtilityDraftRules.ValidateMeterReading(
            startReading: 1_450m,
            endReading: 1_580m,
            hasChangedFromInitial: true,
            adjustmentReason: "");

        Assert.Contains(issues, i => i.Error == UtilityValidationError.AdjustmentReasonIsRequired);
    }

    [Fact]
    public void ValidateMeterReading_AcceptsValidReadingWithReason()
    {
        var issues = UtilityDraftRules.ValidateMeterReading(
            startReading: 1_450m,
            endReading: 1_580m,
            hasChangedFromInitial: true,
            adjustmentReason: "Đồng hồ kiểm tra lại thực tế");

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("ChuTro", true)]
    [InlineData("chutro", true)]
    [InlineData("QuanLy", false)]
    [InlineData("KhachThue", false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void RolePermissions_CanManageServicePrices_FollowsRules(string? role, bool expected)
    {
        Assert.Equal(expected, UtilityDraftRules.CanManageServicePrices(role));
    }

    [Theory]
    [InlineData("ChuTro", true)]
    [InlineData("QuanLy", true)]
    [InlineData("KhachThue", false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void RolePermissions_CanRecordMeterReadings_FollowsRules(string? role, bool expected)
    {
        Assert.Equal(expected, UtilityDraftRules.CanRecordMeterReadings(role));
    }

    [Theory]
    [InlineData("ELEC", true)]
    [InlineData("WATER", true)]
    [InlineData("NET", false)]
    [InlineData("CLEAN", false)]
    [InlineData("PARK", false)]
    public void RolePermissions_ElectricityAndWaterTariffsAreReadOnly_PerD7(string code, bool expected)
    {
        Assert.Equal(expected, UtilityDraftRules.IsTariffReadOnly(code));
    }
}

