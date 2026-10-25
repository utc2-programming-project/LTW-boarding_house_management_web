using System.Security.Claims;
using Web.Features.Accounts;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Rules;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityValidationRulesTests
{
    [Fact]
    public void ValidateCreateService_RejectsEmptyNameAndNonPositiveRate()
    {
        var request = ValidCreateRequest();
        request.Name = "  ";
        request.Rate = 0m;

        var issues = UtilityDraftRules.ValidateCreateService(request);

        Assert.Contains(issues, issue => issue.Error == UtilityErrorCode.NameIsRequired);
        Assert.Contains(issues, issue => issue.Error == UtilityErrorCode.PriceMustBePositive);
    }

    [Fact]
    public void ValidateCreateService_RequiresEffectiveDate()
    {
        var request = ValidCreateRequest();
        request.EffectiveFrom = default;

        Assert.Contains(
            UtilityDraftRules.ValidateCreateService(request),
            issue => issue.Error == UtilityErrorCode.EffectiveFromIsRequired);
    }

    [Fact]
    public void ValidateCreateService_RejectsUnknownUnit()
    {
        var request = ValidCreateRequest();
        request.Unit = (ServiceUnitCategory)999;

        Assert.Contains(
            UtilityDraftRules.ValidateCreateService(request),
            issue => issue.Error == UtilityErrorCode.InvalidServiceUnit);
    }

    [Fact]
    public void ValidateCreateService_AcceptsValidInput()
    {
        Assert.Empty(UtilityDraftRules.ValidateCreateService(ValidCreateRequest()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5000)]
    public void ValidateUpdatePrice_RejectsNonPositiveRate(decimal rate)
    {
        var request = ValidPriceRequest();
        request.NewRate = rate;

        Assert.Contains(
            UtilityDraftRules.ValidateUpdatePrice(request),
            issue => issue.Error == UtilityErrorCode.PriceMustBePositive);
    }

    [Fact]
    public void ValidateUpdatePrice_RequiresStartDate()
    {
        var request = ValidPriceRequest();
        request.EffectiveFrom = default;

        Assert.Contains(
            UtilityDraftRules.ValidateUpdatePrice(request),
            issue => issue.Error == UtilityErrorCode.EffectiveFromIsRequired);
    }

    [Fact]
    public void ValidateUpdatePrice_RejectsUnknownUnit()
    {
        var request = ValidPriceRequest();
        request.Unit = (ServiceUnitCategory)999;

        Assert.Contains(
            UtilityDraftRules.ValidateUpdatePrice(request),
            issue => issue.Error == UtilityErrorCode.InvalidServiceUnit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateUpdatePrice_RequiresEndDateStrictlyAfterStart(int daysAfterStart)
    {
        var request = ValidPriceRequest();
        request.EffectiveTo = request.EffectiveFrom.AddDays(daysAfterStart);

        Assert.Contains(
            UtilityDraftRules.ValidateUpdatePrice(request),
            issue => issue.Error == UtilityErrorCode.EffectiveToMustBeAfterEffectiveFrom);
    }

    [Fact]
    public void ValidateUpdatePrice_AcceptsEndDateAfterStart()
    {
        var request = ValidPriceRequest();
        request.EffectiveTo = request.EffectiveFrom.AddDays(1);

        Assert.Empty(UtilityDraftRules.ValidateUpdatePrice(request));
    }

    [Fact]
    public void ValidateBillingPeriod_RejectsEndBeforeStart()
    {
        var issues = UtilityDraftRules.ValidateBillingPeriod(
            new DateOnly(2026, 10, 2),
            new DateOnly(2026, 10, 1));

        Assert.Contains(issues, issue => issue.Error == UtilityErrorCode.BillingPeriodEndMustBeOnOrAfterStart);
    }

    [Fact]
    public void ValidateBillingPeriod_AcceptsSameDayOrLaterEnd()
    {
        Assert.Empty(UtilityDraftRules.ValidateBillingPeriod(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)));
        Assert.Empty(UtilityDraftRules.ValidateBillingPeriod(
            new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)));
    }

    [Fact]
    public void ValidateMeterReading_RejectsEndReadingLowerThanStart()
    {
        Assert.Contains(
            UtilityDraftRules.ValidateMeterReading(1450m, 1400m),
            issue => issue.Error == UtilityErrorCode.MeterReadingMustBeGreaterOrEqualStart);
    }

    [Fact]
    public void ValidateMeterReading_RejectsNegativeEndReading()
    {
        Assert.Contains(
            UtilityDraftRules.ValidateMeterReading(0m, -10m),
            issue => issue.Error == UtilityErrorCode.MeterReadingCannotBeNegative);
    }

    [Fact]
    public void ValidateMeterReading_RequiresReasonWhenReadingChanged()
    {
        Assert.Contains(
            UtilityDraftRules.ValidateMeterReading(1450m, 1580m, true, " "),
            issue => issue.Error == UtilityErrorCode.AdjustmentReasonIsRequired);
    }

    [Theory]
    [InlineData(Roles.ChuTro, true)]
    [InlineData("chutro", true)]
    [InlineData(Roles.QuanLy, false)]
    [InlineData(Roles.KhachThue, false)]
    [InlineData(Roles.Admin, false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void CanManageServicePrices_FollowsD7(string? role, bool expected) =>
        Assert.Equal(expected, UtilityDraftRules.CanManageServicePrices(role));

    [Theory]
    [InlineData(Roles.ChuTro, true)]
    [InlineData(Roles.QuanLy, true)]
    [InlineData(Roles.KhachThue, false)]
    [InlineData(Roles.Admin, false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void CanRecordMeterReadings_FollowsMatrix(string? role, bool expected) =>
        Assert.Equal(expected, UtilityDraftRules.CanRecordMeterReadings(role));

    [Theory]
    [InlineData(Roles.ChuTro, true)]
    [InlineData(Roles.QuanLy, true)]
    [InlineData(Roles.KhachThue, true)]
    [InlineData(Roles.Admin, false)]
    [InlineData("Unknown", false)]
    [InlineData(null, false)]
    public void CanViewUtilityData_ExcludesAdminFromP5(string? role, bool expected) =>
        Assert.Equal(expected, UtilityDraftRules.CanViewUtilityData(role));

    [Theory]
    [InlineData("ELEC", true)]
    [InlineData("WATER", true)]
    [InlineData("NET", false)]
    [InlineData("CLEAN", false)]
    [InlineData("PARK", false)]
    public void ElectricityAndWaterTariffsAreReadOnlyPerD7(string code, bool expected) =>
        Assert.Equal(expected, UtilityDraftRules.IsTariffReadOnly(code));

    [Fact]
    public void ResolveRole_UsesRestrictivePrecedenceWhenClaimsContainMultipleRoles()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.Name, "preview-user"),
            new Claim(ClaimTypes.Role, Roles.ChuTro),
            new Claim(ClaimTypes.Role, Roles.Admin),
        ], "test");

        Assert.Equal(Roles.Admin, UtilityDraftRules.ResolveRole(new ClaimsPrincipal(identity)));
    }

    private static CreateServiceRequest ValidCreateRequest() => new()
    {
        Name = "Laundry",
        Rate = 50_000m,
        Unit = ServiceUnitCategory.Visit,
        BuildingId = "b1",
        EffectiveFrom = new DateOnly(2026, 10, 25),
    };

    private static UpdatePriceRequest ValidPriceRequest() => new()
    {
        ServiceId = "srv-3",
        NewRate = 120_000m,
        Unit = ServiceUnitCategory.Room,
        BuildingScope = "b1",
        EffectiveFrom = new DateOnly(2026, 10, 25),
    };
}
