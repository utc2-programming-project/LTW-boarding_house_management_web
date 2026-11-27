using System.Collections;
using System.Globalization;
using System.Resources;
using System.Text.RegularExpressions;
using Web.Features.Vehicles;

namespace NhaTro.UnitTests.Features.Vehicles;

/// <summary>
/// Khóa song ngữ của module xe: vi-VN và en-US phải cùng tập khóa, cùng thứ tự tham số,
/// và các nhãn/thông báo chính không được để trống ở cả hai ngôn ngữ.
/// </summary>
public sealed class VehicleLocalizationTests
{
    [Fact]
    public void VehicleResources_ContainSameKeysInVietnameseAndEnglish()
    {
        var resources = CreateResourceManager();
        using var vi = resources.GetResourceSet(CultureInfo.GetCultureInfo("vi-VN"), true, false)!;
        using var en = resources.GetResourceSet(CultureInfo.GetCultureInfo("en-US"), true, false)!;

        var viKeys = ReadKeys(vi);
        var enKeys = ReadKeys(en);

        Assert.Empty(viKeys.Except(enKeys));
        Assert.Empty(enKeys.Except(viKeys));
    }

    [Fact]
    public void VehicleResources_PreservePlaceholderIndexesAcrossCultures()
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

    [Theory]
    [InlineData("TabVehicles")]
    [InlineData("TabZones")]
    [InlineData("TabGuests")]
    [InlineData("TabAudit")]
    [InlineData("TabSettings")]
    [InlineData("PageTitleVehicles")]
    [InlineData("PageTitleAudit")]
    [InlineData("BlockingAlertNotifTitle")]
    [InlineData("BlockingAlertNotifBody")]
    [InlineData("GuestNoticeNotifTitle")]
    [InlineData("GuestNoticeNotifBody")]
    [InlineData("NotifVehicleDeactivatedTitle")]
    [InlineData("NotifVehicleDeactivatedBody")]
    public void VehicleResources_HaveCopyInBothCultures(string key)
    {
        var resources = CreateResourceManager();

        Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, CultureInfo.GetCultureInfo("vi-VN"))),
            $"Missing vi-VN resource: {key}");
        Assert.False(string.IsNullOrWhiteSpace(resources.GetString(key, CultureInfo.GetCultureInfo("en-US"))),
            $"Missing en-US resource: {key}");
    }

    private static ResourceManager CreateResourceManager() => new(
        "Web.Resources.Features.Vehicles.VehicleResource",
        typeof(VehicleResource).Assembly);

    private static HashSet<string> ReadKeys(ResourceSet resources) => resources
        .Cast<DictionaryEntry>()
        .Select(entry => (string)entry.Key)
        .ToHashSet(StringComparer.Ordinal);

    private static int[] PlaceholderIndexes(string value) => Regex.Matches(value, @"\{(\d+)")
        .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
        .Order()
        .ToArray();
}