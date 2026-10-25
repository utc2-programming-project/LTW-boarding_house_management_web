using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Web.Common.Utils;
using Web.Features.Utilities;
using Web.Features.Utilities.DTOs;
using Web.Features.Utilities.Rules;
using Xunit;

namespace NhaTro.UnitTests.Features.Utilities;

public sealed class UtilityLocalizationTests
{
    [Fact]
    public void UtilityResources_ContainSameKeysInVietnameseAndEnglish()
    {
        var resources = CreateResourceManager();
        using var vi = resources.GetResourceSet(CultureInfo.GetCultureInfo("vi-VN"), true, false)!;
        using var en = resources.GetResourceSet(CultureInfo.GetCultureInfo("en-US"), true, false)!;

        var viKeys = ReadKeys(vi);
        var enKeys = ReadKeys(en);

        Assert.Empty(viKeys.Except(enKeys));
        Assert.Empty(enKeys.Except(viKeys));
    }

    [Theory]
    [InlineData("SampleDataBadge")]
    [InlineData("SampleDataNotice")]
    [InlineData("ErrorNameIsRequired")]
    [InlineData("ErrorPriceMustBePositive")]
    [InlineData("ErrorEffectiveToMustBeAfterEffectiveFrom")]
    [InlineData("ErrorInvalidBuildingScope")]
    [InlineData("PricesEmptyDescription")]
    [InlineData("ReadingsEmptyTitle")]
    public void UtilityResources_HaveSampleAndValidationCopyInBothCultures(string key)
    {
        var resources = CreateResourceManager();

        Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, CultureInfo.GetCultureInfo("vi-VN"))));
        Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, CultureInfo.GetCultureInfo("en-US"))));
    }

    [Fact]
    public void UtilityResources_PreservePlaceholderIndexesAcrossCultures()
    {
        var resources = CreateResourceManager();
        using var vi = resources.GetResourceSet(CultureInfo.GetCultureInfo("vi-VN"), true, false)!;
        using var en = resources.GetResourceSet(CultureInfo.GetCultureInfo("en-US"), true, false)!;

        foreach (var key in ReadKeys(vi))
        {
            var viValue = vi.GetString(key) ?? string.Empty;
            var enValue = en.GetString(key) ?? string.Empty;
            Assert.Equal(PlaceholderIndexes(viValue), PlaceholderIndexes(enValue));
        }
    }

    [Fact]
    public void UtilityResources_HaveLabelsForEveryBusinessErrorUnitAndPriceStatus()
    {
        var resources = CreateResourceManager();
        var keys = Enum.GetValues<UtilityErrorCode>()
            .Select(error => $"Error{error}")
            .Concat(
            [
                "UnitRoom", "UnitPerson", "UnitVehicle", "UnitVisit", "UnitKwh", "UnitM3",
                "StatusActive", "StatusUpcoming", "StatusExpired",
                "MeterType", "FixedMonthlyType", "CustomType",
            ]);

        foreach (var key in keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, CultureInfo.GetCultureInfo("vi-VN"))),
                $"Missing vi-VN resource: {key}");
            Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, CultureInfo.GetCultureInfo("en-US"))),
                $"Missing en-US resource: {key}");
        }
    }

    [Fact]
    public void MoneyFormatter_FormatsVndIndependentlyOfUiCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
            var viResult = MoneyFormatter.Format(1_500_000m);

            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            var enResult = MoneyFormatter.Format(1_500_000m);

            Assert.Equal(viResult, enResult);
            Assert.Contains("1.500.000", viResult);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static ResourceManager CreateResourceManager() => new(
        "Web.Resources.Features.Utilities.UtilityResources",
        typeof(UtilityResources).Assembly);

    private static HashSet<string> ReadKeys(ResourceSet resources) => resources
        .Cast<DictionaryEntry>()
        .Select(entry => (string)entry.Key)
        .ToHashSet(StringComparer.Ordinal);

    private static int[] PlaceholderIndexes(string value) => Regex.Matches(value, @"\{(\d+)")
        .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
        .Order()
        .ToArray();
}
