using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Web.Configuration.Options;

namespace NhaTro.IntegrationTests.Features.Security;

// Kiem startup that nhung dung tai Build, truoc khi seed hoac ket noi SQL.
public sealed class DeploymentConfigurationTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(
        Path.GetTempPath(), "nhatro-deployment-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Dpapi_bat_nhung_thieu_duong_dan_keys_bi_chan_truoc_khi_build(string? keysPath)
    {
        using var factory = CreateFactory(keysPath, "true");

        var error = Record.Exception(() => _ = factory.Services);

        Assert.NotNull(error);
        var cause = Assert.IsType<InvalidOperationException>(error.GetBaseException());
        Assert.Equal("Security:ProtectKeysWithDpapi requires Security:DataProtectionKeysPath.", cause.Message);
    }

    [Fact]
    public void Dpapi_tat_cho_phep_duong_dan_keys_trong()
    {
        using var factory = CreateFactory(string.Empty, "false");

        AssertReachedBuild(factory);
    }

    [Fact]
    public void Dpapi_bat_va_co_duong_dan_keys_kiem_tra_dung_nen_tang()
    {
        using var factory = CreateFactory(Path.Combine(_testRoot, "keys"), "true");

        if (!OperatingSystem.IsWindows())
        {
            var error = Record.Exception(() => _ = factory.Services);
            Assert.NotNull(error);
            Assert.IsType<PlatformNotSupportedException>(error.GetBaseException());
            return;
        }

        AssertReachedBuild(factory);
    }

    [Fact]
    public void Logging_directory_bo_khoang_trang_o_bien()
    {
        var logsPath = Path.Combine(_testRoot, "trimmed-logs");
        using var factory = CreateFactory(string.Empty, "false", "  " + logsPath + "  ");

        AssertReachedBuild(factory);

        Assert.True(Directory.Exists(logsPath));
    }

    [Fact]
    public void Logging_directory_khong_hop_le_bao_ten_cau_hinh()
    {
        using var factory = CreateFactory(string.Empty, "false", Path.Combine(_testRoot, "invalid\0private-path"));

        var error = Record.Exception(() => _ = factory.Services);

        Assert.NotNull(error);
        var cause = Assert.IsType<InvalidOperationException>(error.GetBaseException());
        Assert.Equal("Logging:Directory must be a valid directory path.", cause.Message);
        Assert.DoesNotContain("private-path", cause.Message);
    }

    [Fact]
    public void Security_options_bind_dung_bool_va_duong_dan()
    {
        var keysPath = Path.Combine(_testRoot, "bound-keys");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Security:ProtectKeysWithDpapi"] = "true",
                ["Security:DataProtectionKeysPath"] = keysPath,
            }).Build();

        var options = configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>();

        Assert.NotNull(options);
        Assert.True(options.ProtectKeysWithDpapi);
        Assert.Equal(keysPath, options.DataProtectionKeysPath);
    }

    private WebApplicationFactory<Program> CreateFactory(string? keysPath, string dpapi, string? logsPath = null)
    {
        return new StartupFactory(new Dictionary<string, string?>
        {
            ["Logging:Directory"] = logsPath ?? Path.Combine(_testRoot, "logs"),
            ["Security:ProtectKeysWithDpapi"] = dpapi,
            ["Security:DataProtectionKeysPath"] = keysPath,
        });
    }

    private sealed class StartupFactory(Dictionary<string, string?> configuration) : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // DeferredHostBuilder chuyen host config thanh args truoc khi Program doc cau hinh.
            // ConfigureAppConfiguration se qua muon cho code chay truoc Build.
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(configuration));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            // Moc dung rieng de kiem guard startup, khong phu thuoc LocalDB.
            builder.ConfigureServices(_ => throw new StartupReachedBuildException());
        }
    }

    private static void AssertReachedBuild(WebApplicationFactory<Program> factory)
    {
        var error = Record.Exception(() => _ = factory.Services);

        Assert.NotNull(error);
        Assert.IsType<StartupReachedBuildException>(error.GetBaseException());
    }

    private sealed class StartupReachedBuildException : Exception { }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
