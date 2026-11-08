using System.Xml.Linq;
using Web.Features.Settings;

namespace NhaTro.UnitTests.Features.Settings;

// Chặn thiếu hoặc lệch key VI/EN khi trang Công tắc Quản lý đổi tên/mô tả công tắc.
public class ManagerSwitchResourceTests
{
    private static readonly string[] Cultures = ["vi-VN", "en-US"];

    private static Dictionary<string, string> Load(string culture)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, "Web", "Resources", $"Shared.{culture}.resx")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "Web", "Resources", $"Shared.{culture}.resx");
        return XDocument.Load(path).Root!.Elements("data").ToDictionary(
            e => (string)e.Attribute("name")!,
            e => (string?)e.Element("value") ?? string.Empty);
    }

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public void Moi_cong_tac_co_ten_va_mo_ta(string culture)
    {
        var texts = Load(culture);
        var missing = new List<string>();

        foreach (var definition in ManagerToggleCatalog.All)
        {
            if (!texts.TryGetValue(definition.Code, out var name) || string.IsNullOrWhiteSpace(name))
            {
                missing.Add(definition.Code);
            }

            var descKey = $"SwDesc-{definition.Code}";
            if (!texts.TryGetValue(descKey, out var desc) || string.IsNullOrWhiteSpace(desc))
            {
                missing.Add(descKey);
            }

            var detailKey = $"SwDetail-{definition.Code}";
            if (!texts.TryGetValue(detailKey, out var detail) || string.IsNullOrWhiteSpace(detail))
            {
                missing.Add(detailKey);
            }
        }

        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public void Du_tieu_de_nhom_va_nhan_che_do(string culture)
    {
        var texts = Load(culture);

        foreach (var group in ManagerToggleCatalog.All.Select(d => d.Group).Distinct())
        {
            Assert.True(texts.TryGetValue($"SwGroup{group}", out var title) && !string.IsNullOrWhiteSpace(title),
                $"Thiếu tiêu đề nhóm {group}");
        }

        foreach (var key in new[] { "SwModeAllow", "SwModeApprove", "SwModeDeny", "SwModeLabel", "SwModesTitle" })
        {
            Assert.True(texts.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value), $"Thiếu key {key}");
        }
    }
}
