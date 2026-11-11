using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Web.Components.Shared.Forms;
using Web.Infrastructure.Addresses;
using SharedResource = Web.Shared;

namespace NhaTro.UnitTests.Components;

/// <summary>Địa chỉ 3 cấp cũ được quy về mô hình 2 cấp (tỉnh/thành phố + phường/xã).</summary>
public sealed class AddressInputTests : BunitContext
{
    private readonly string _webRoot;

    public AddressInputTests()
    {
        _webRoot = Path.Combine(Path.GetTempPath(), "nhatro-address-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_webRoot, "data"));
        File.WriteAllText(Path.Combine(_webRoot, "data", "vn-address.json"), """
        [
          {
            "code": 79,
            "name": "Thành phố Hồ Chí Minh",
            "wards": [
              { "code": 26734, "name": "Phường Bến Thành" },
              { "code": 26737, "name": "Phường Sài Gòn" }
            ]
          },
          {
            "code": 1,
            "name": "Thành phố Hà Nội",
            "wards": [
              { "code": 4, "name": "Phường Ba Đình" }
            ]
          }
        ]
        """);

        var environment = new FakeWebHostEnvironment(_webRoot);
        Services.AddSingleton<IWebHostEnvironment>(environment);
        Services.AddSingleton(sp => new VietnamAddressCatalog(
            sp.GetRequiredService<IWebHostEnvironment>(),
            NullLogger<VietnamAddressCatalog>.Instance));
        Services.AddSingleton<IStringLocalizer<SharedResource>>(new KeyLocalizer());
    }

    [Fact]
    public void Dia_chi_ba_cap_cu_hien_thi_hai_cap_va_khong_tu_ghi_de_gia_tri_da_luu()
    {
        string? emitted = null;

        var cut = Render<AddressInput>(parameters => parameters
            .Add(p => p.Id, "addr")
            .Add(p => p.Value, "12 Nguyễn Trãi, Phường Bến Thành, Quận 1, TP.HCM")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => emitted = value)));

        Assert.Equal("12 Nguyễn Trãi", cut.Find("#addr-street").GetAttribute("value"));
        Assert.Equal("Phường Bến Thành", cut.Find("#addr-ward").GetAttribute("value"));
        Assert.Equal("Thành phố Hồ Chí Minh", cut.Find("#addr-province").GetAttribute("value"));

        // Chỉ xem hồ sơ cũ thì không được ghi lại địa chỉ, tránh làm mất cấp quận/huyện.
        Assert.Null(emitted);
    }

    [Fact]
    public void Sua_dia_chi_cu_thi_moi_chuan_hoa_ve_hai_cap()
    {
        string? emitted = null;

        var cut = Render<AddressInput>(parameters => parameters
            .Add(p => p.Id, "addr")
            .Add(p => p.Value, "12 Nguyễn Trãi, Phường Bến Thành, Quận 1, TP.HCM")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => emitted = value)));

        cut.Find("#addr-street").Input("12 Nguyễn Trãi");

        cut.WaitForAssertion(() =>
            Assert.Equal("12 Nguyễn Trãi, Phường Bến Thành, Thành phố Hồ Chí Minh", emitted));
    }

    [Fact]
    public void Dia_chi_hai_cap_dung_san_khong_bi_ghi_lai()
    {
        string? emitted = null;

        Render<AddressInput>(parameters => parameters
            .Add(p => p.Id, "addr")
            .Add(p => p.Value, "Số 1 Lê Lợi, Phường Sài Gòn, Thành phố Hồ Chí Minh")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => emitted = value)));

        Assert.Null(emitted);
    }

    [Fact]
    public void Dia_chi_cu_khong_co_tinh_hien_thi_phan_con_lai_va_khong_tu_ghi_de()
    {
        string? emitted = null;

        var cut = Render<AddressInput>(parameters => parameters
            .Add(p => p.Id, "addr")
            .Add(p => p.Value, "12 Nguyễn Trãi, Quận 5")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => emitted = value)));

        Assert.Equal("12 Nguyễn Trãi", cut.Find("#addr-street").GetAttribute("value"));
        Assert.Null(emitted);
    }

    [Fact]
    public void Chon_tinh_thi_mo_khoa_o_chon_phuong_xa()
    {
        string? emitted = null;

        var cut = Render<AddressInput>(parameters => parameters
            .Add(p => p.Id, "addr")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => emitted = value)));

        Assert.NotNull(cut.Find("#addr-ward").GetAttribute("disabled"));
        Assert.Equal(2, cut.FindAll("#addr-ward option").Count);

        cut.Find("#addr-province").Change("Thành phố Hà Nội");
        cut.Find("#addr-ward").Change("Phường Ba Đình");

        Assert.Null(cut.Find("#addr-ward").GetAttribute("disabled"));
        Assert.Contains(cut.FindAll("#addr-ward option"), option => option.GetAttribute("value") == "Phường Ba Đình");
        cut.WaitForAssertion(() => Assert.Equal("Phường Ba Đình, Thành phố Hà Nội", emitted));
    }

    [Fact]
    public void Chon_khac_thi_hien_o_nhap_tay_cho_tinh_va_phuong()
    {
        string? emitted = null;

        var cut = Render<AddressInput>(parameters => parameters
            .Add(p => p.Id, "addr")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => emitted = value)));

        cut.Find("#addr-province").Change("__manual__");

        Assert.Empty(cut.FindAll("#addr-province"));
        Assert.Empty(cut.FindAll("#addr-ward"));

        cut.Find("#addr-street").Input("12 Nguyễn Trãi");
        cut.Find("#addr-province-manual").Input("Tỉnh Hà Nam");
        cut.Find("#addr-ward-manual").Input("Phường Bến Thành");

        cut.WaitForAssertion(() =>
            Assert.Equal("12 Nguyễn Trãi, Phường Bến Thành, Tỉnh Hà Nam", emitted));
    }

    [Fact]
    public void Dia_chi_ngoai_danh_muc_vao_che_do_nhap_tay()
    {
        string? emitted = null;

        var cut = Render<AddressInput>(parameters => parameters
            .Add(p => p.Id, "addr")
            .Add(p => p.Value, "12 Nguyễn Trãi, Phường Bến Thành, Tỉnh Hà Nam")
            .Add(p => p.ValueChanged, EventCallback.Factory.Create<string?>(this, value => emitted = value)));

        Assert.Equal("12 Nguyễn Trãi", cut.Find("#addr-street").GetAttribute("value"));
        Assert.Equal("Phường Bến Thành", cut.Find("#addr-ward-manual").GetAttribute("value"));
        Assert.Equal("Tỉnh Hà Nam", cut.Find("#addr-province-manual").GetAttribute("value"));
        Assert.Null(emitted);
    }

    [Fact]
    public void Nhap_tay_trung_tinh_trong_danh_muc_thi_quay_lai_che_do_chon()
    {
        var cut = Render<AddressInput>(parameters => parameters
            .Add(p => p.Id, "addr"));

        cut.Find("#addr-province").Change("__manual__");
        cut.Find("#addr-province-manual").Input("Hồ Chí Minh");
        cut.Find("#addr-province-manual").Blur();

        Assert.Equal("Thành phố Hồ Chí Minh", cut.Find("#addr-province").GetAttribute("value"));
        Assert.Contains(cut.FindAll("#addr-province option"), option => option.GetAttribute("value") == "Thành phố Hồ Chí Minh");
    }

    private sealed class KeyLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

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
