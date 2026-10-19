using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NhaTro.UnitTests.TestSupport;
using Web.Configuration.Options;
using Web.Infrastructure.Storage;

namespace NhaTro.UnitTests.Features.Storage;

// VHT-13: ghi/doc/xoa tep vat ly ngoai wwwroot; ten do server sinh; chan path traversal.
public sealed class LocalFileStorageTests : IDisposable
{
    private readonly string _root;
    private readonly LocalFileStorage _storage;

    public LocalFileStorageTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "nhatro-storage-tests", Guid.NewGuid().ToString("N"));
        _storage = new LocalFileStorage(
            Options.Create(new StorageOptions { RootPath = _root }),
            new FakeWebHostEnvironment { ContentRootPath = _root, WebRootPath = Path.Combine(_root, "wwwroot") },
            NullLogger<LocalFileStorage>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task Luu_tep_tao_ten_do_server_sinh_va_nam_ngoai_wwwroot()
    {
        var content = "noi dung"u8.ToArray();

        var written = await _storage.SaveAsync(
            content,
            ".pdf",
            new DateTimeOffset(2026, 10, 16, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal("2026/10", Path.GetDirectoryName(written.RelativePath)!.Replace('\\', '/'));
        Assert.EndsWith(".pdf", written.StoredFileName);
        Assert.Equal((long)content.Length, written.SizeBytes);
        Assert.True(_storage.Exists(written.RelativePath));

        var physical = Path.Combine(_root, written.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physical));
        Assert.False(physical.StartsWith(Path.Combine(_root, "wwwroot"), StringComparison.OrdinalIgnoreCase));
        Assert.Equal(content, await File.ReadAllBytesAsync(physical));
    }

    [Fact]
    public async Task Doc_lai_dung_noi_dung_da_luu()
    {
        var content = "hop dong thue phong"u8.ToArray();
        var written = await _storage.SaveAsync(content, ".pdf", DateTimeOffset.UtcNow);

        await using var stream = _storage.TryOpenRead(written.RelativePath);

        Assert.NotNull(stream);
        using var reader = new MemoryStream();
        await stream!.CopyToAsync(reader);
        Assert.Equal(content, reader.ToArray());
    }

    [Fact]
    public void Doc_tep_khong_ton_tai_tra_null()
    {
        Assert.Null(_storage.TryOpenRead("2026/10/khong-ton-tai.pdf"));
        Assert.False(_storage.Exists("2026/10/khong-ton-tai.pdf"));
    }

    [Fact]
    public async Task Xoa_tep_vat_ly()
    {
        var written = await _storage.SaveAsync("abc"u8.ToArray(), ".jpg", DateTimeOffset.UtcNow);

        _storage.Delete(written.RelativePath);

        Assert.False(_storage.Exists(written.RelativePath));
        // Xoa lai lan hai khong nem loi.
        _storage.Delete(written.RelativePath);
    }

    [Theory]
    [InlineData("../ngoai-root.jpg")]
    [InlineData("../../ngoai.jpg")]
    [InlineData(@"C:\windows\system32\config\SAM")]
    [InlineData("")]
    public void Duong_dan_thoat_khoi_root_bi_chan(string relativePath)
    {
        Assert.Throws<StoragePathViolationException>(() => _storage.TryOpenRead(relativePath));
        Assert.Throws<StoragePathViolationException>(() => _storage.Delete(relativePath));
    }
}
