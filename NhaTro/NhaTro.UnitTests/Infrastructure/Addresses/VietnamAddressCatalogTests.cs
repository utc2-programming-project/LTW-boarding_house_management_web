using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Infrastructure.Addresses;

namespace NhaTro.UnitTests.Infrastructure.Addresses;

public sealed class VietnamAddressCatalogTests : IDisposable
{
    private readonly string _webRoot;

    public VietnamAddressCatalogTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "nhatro-address-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_webRoot, "data"));
        File.WriteAllText(Path.Combine(_webRoot, "data", "vn-address.json"), """
        [
          {
            "code": 1,
            "name": "Thành phố Hà Nội",
            "wards": [
              { "code": 4, "name": "Phường Ba Đình" },
              { "code": 292, "name": "Phường Bạch Mai" }
            ]
          },
          {
            "code": 79,
            "name": "Thành phố Hồ Chí Minh",
            "wards": [
              { "code": 26734, "name": "Phường Bến Thành" }
            ]
          }
        ]
        """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_webRoot))
        {
            Directory.Delete(_webRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("Thành phố Hà Nội")]
    [InlineData("Hà Nội")]
    [InlineData("ha noi")]
    [InlineData("HA NOI")]
    [InlineData("TP. Hà Nội")]
    public void FindProvince_MatchesFullShortAndAccentInsensitiveNames(string input)
    {
        var catalog = CreateCatalog();

        var province = catalog.FindProvince(input);

        Assert.NotNull(province);
        Assert.Equal(1, province!.Code);
    }

    [Theory]
    [InlineData("Phường Ba Đình")]
    [InlineData("Ba Đình")]
    [InlineData("ba dinh")]
    public void FindWard_MatchesFullShortAndAccentInsensitiveNames(string input)
    {
        var catalog = CreateCatalog();
        var province = catalog.FindProvince("Hà Nội");

        var ward = catalog.FindWard(province!, input);

        Assert.NotNull(ward);
        Assert.Equal(4, ward!.Code);
    }

    [Theory]
    [InlineData("TP.HCM")]
    [InlineData("TP HCM")]
    [InlineData("TPHCM")]
    [InlineData("HCM")]
    [InlineData("Sài Gòn")]
    public void FindProvince_MatchesCommonAbbreviations(string input)
    {
        var catalog = CreateCatalog();

        var province = catalog.FindProvince(input);

        Assert.NotNull(province);
        Assert.Equal(79, province!.Code);
    }

    [Theory]
    [InlineData("Quận 1", true)]
    [InlineData("quan 1", true)]
    [InlineData("Q. Bình Thạnh", true)]
    [InlineData("Huyện Nhà Bè", true)]
    [InlineData("Thị xã Bến Cát", true)]
    [InlineData("TP. Thủ Đức", true)]
    [InlineData("Phường Bến Thành", false)]
    [InlineData("12 Nguyễn Trãi", false)]
    public void IsLegacyDistrictSegment_Nhan_dien_cap_huyen_cu(string input, bool expected)
    {
        Assert.Equal(expected, AddressText.IsLegacyDistrictSegment(input));
    }

    [Fact]
    public void FindProvince_ReturnsNullForUnknownOrEmptyInput()
    {
        var catalog = CreateCatalog();

        Assert.Null(catalog.FindProvince("Không tồn tại"));
        Assert.Null(catalog.FindProvince(""));
        Assert.Null(catalog.FindProvince(null));
    }

    [Fact]
    public void RepositoryDataset_HasPostMergeProvinceAndWardCounts()
    {
        var webRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Web", "wwwroot"));

        var catalog = new VietnamAddressCatalog(
            new FakeWebHostEnvironment(webRoot),
            NullLogger<VietnamAddressCatalog>.Instance);

        Assert.Equal(34, catalog.Provinces.Count);
        Assert.Equal(3321, catalog.Provinces.Sum(province => province.Wards.Count));
    }

    private VietnamAddressCatalog CreateCatalog() =>
        new(new FakeWebHostEnvironment(_webRoot), NullLogger<VietnamAddressCatalog>.Instance);

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string webRootPath)
        {
            WebRootPath = webRootPath;
        }

        public string ApplicationName { get; set; } = "NhaTro.UnitTests";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
