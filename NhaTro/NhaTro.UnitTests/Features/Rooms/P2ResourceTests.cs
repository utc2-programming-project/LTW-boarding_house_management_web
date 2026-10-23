using System.Xml.Linq;

namespace NhaTro.UnitTests.Features.Rooms;

// Hồi quy nhánh 16 (B16-06, B16-07 và nhãn empty state B16-02) trên file .resx, không cần DB hay trình duyệt.
public class P2ResourceTests
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
    public void B16_06_loi_nhac_cho_duyet_khong_lo_chi_tiet_noi_bo(string culture)
    {
        var text = Load(culture)["P2ApprovalPendingHint"];

        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain("SQL", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("service", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cac_key_P2_co_du_o_ca_hai_ngon_ngu()
    {
        var vi = Load("vi-VN").Keys.Where(k => k.StartsWith("P2", StringComparison.Ordinal)).ToHashSet();
        var en = Load("en-US").Keys.Where(k => k.StartsWith("P2", StringComparison.Ordinal)).ToHashSet();

        Assert.Empty(vi.Except(en));
        Assert.Empty(en.Except(vi));
    }

    [Theory]
    [InlineData("TableNoResults")]
    [InlineData("TableNoResultsDescription")]
    [InlineData("TableClearFilters")]
    public void B16_02_nhan_empty_state_khi_loc_co_o_ca_hai_ngon_ngu(string key)
    {
        foreach (var culture in Cultures)
        {
            Assert.False(string.IsNullOrWhiteSpace(Load(culture)[key]), $"{culture}:{key}");
        }
    }

    [Fact]
    public void B16_07_don_vi_dien_tich_co_o_ca_hai_ngon_ngu()
    {
        foreach (var culture in Cultures)
        {
            Assert.Equal("m²", Load(culture)["P2SquareMeter"]);
        }
    }
}
