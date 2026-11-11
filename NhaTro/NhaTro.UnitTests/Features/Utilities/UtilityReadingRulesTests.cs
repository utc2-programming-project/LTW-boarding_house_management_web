using Web.Features.Utilities;
using Web.Features.Utilities.Rules;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityReadingRulesTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 160, 60)]
    [InlineData(160, 160, 0)]
    [InlineData(100, 100.12345, 0.1235)]
    public void CalculateUsage_ReturnsNormalizedDifference(decimal start, decimal end, decimal expected)
    {
        var usage = UtilityReadingRules.CalculateUsage(start, end);
        Assert.Equal(expected, UtilityTariffRules.NormalizeQuantity(usage));
    }

    [Fact]
    public void CalculateUsage_ReturnsZeroWhenEndIsLowerThanStart()
    {
        // Service đã chặn trường hợp này; rule vẫn phòng thủ để không sinh lượng âm.
        Assert.Equal(0m, UtilityReadingRules.CalculateUsage(160m, 100m));
    }

    [Fact]
    public void ValidatePeriod_RejectsEndBeforeStart()
    {
        var issues = UtilityReadingRules.ValidatePeriod(
            new DateOnly(2026, 10, 31),
            new DateOnly(2026, 10, 1));

        Assert.Equal(UtilityReadingErrorCode.InvalidPeriod, Assert.Single(issues).Error);
    }

    [Fact]
    public void ValidatePeriod_AcceptsSameDay()
    {
        Assert.Empty(UtilityReadingRules.ValidatePeriod(
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void ValidateReadings_AcceptsReadingsEqualOrGreaterThanOpening()
    {
        Assert.Empty(UtilityReadingRules.ValidateReadings(100m, 160m, 10m, 16m));
        Assert.Empty(UtilityReadingRules.ValidateReadings(100m, 100m, 10m, 10m));
    }

    [Fact]
    public void ValidateReadings_RejectsNegativeReading()
    {
        var issues = UtilityReadingRules.ValidateReadings(
            electricStart: 100m,
            electricEnd: -1m,
            waterStart: 0m,
            waterEnd: 0m);

        Assert.Contains(issues, issue => issue.Error == UtilityReadingErrorCode.ReadingCannotBeNegative);
    }

    [Fact]
    public void ValidateReadings_RejectsReadingLowerThanOpening()
    {
        var issues = UtilityReadingRules.ValidateReadings(
            electricStart: 100m,
            electricEnd: 99m,
            waterStart: 10m,
            waterEnd: 10m);

        Assert.Contains(issues, issue => issue.Error == UtilityReadingErrorCode.ReadingLessThanPrevious);
    }

    [Fact]
    public void ValidateReadings_ReportsBothElectricAndWaterIssues()
    {
        var issues = UtilityReadingRules.ValidateReadings(
            electricStart: 100m,
            electricEnd: 90m,
            waterStart: 10m,
            waterEnd: 5m);

        Assert.Equal(2, issues.Count);
    }
}
