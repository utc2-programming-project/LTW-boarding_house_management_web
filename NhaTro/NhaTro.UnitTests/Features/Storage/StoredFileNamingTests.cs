using Web.Infrastructure.Storage;

namespace NhaTro.UnitTests.Features.Storage;

// VHT-13: ten tep do server sinh, khong dung ten client gui len (VHT-12 muc 6).
public class StoredFileNamingTests
{
    [Fact]
    public void Ten_luu_la_uuid_va_duoi_khong_dung_ten_goc()
    {
        var name = StoredFileNaming.NewStoredFileName(Guid.NewGuid(), ".pdf");

        Assert.EndsWith(".pdf", name);
        Assert.Equal(32 + 4, name.Length);
        Assert.DoesNotContain("..", name);
        Assert.DoesNotContain("/", name);
    }

    [Fact]
    public void Hai_lan_luu_khac_nhau_cho_ten_khac_nhau()
    {
        var first = StoredFileNaming.NewStoredFileName(Guid.NewGuid(), ".jpg");
        var second = StoredFileNaming.NewStoredFileName(Guid.NewGuid(), ".jpg");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Duong_dan_theo_nam_thang_gio_Viet_Nam()
    {
        // 2026-12-31 18:00 UTC = 2027-01-01 01:00 gio VN -> thu muc 2027/01.
        var relative = StoredFileNaming.BuildRelativePath(
            new DateTimeOffset(2026, 12, 31, 18, 0, 0, TimeSpan.Zero),
            "abc.jpg");

        Assert.Equal("2027/01/abc.jpg", relative);
    }

    [Fact]
    public void Duong_dan_tuong_doi_khong_chua_o_dia()
    {
        var relative = StoredFileNaming.BuildRelativePath(
            new DateTimeOffset(2026, 10, 16, 3, 0, 0, TimeSpan.Zero),
            "abc.png");

        Assert.Equal("2026/10/abc.png", relative);
        Assert.False(Path.IsPathRooted(relative));
    }
}

// VHT-13: doc stream co gioi han ngay khi doc, tu choi tep rong.
public class LimitedStreamReaderTests
{
    [Fact]
    public async Task Doc_du_noi_dung_trong_gioi_han()
    {
        var payload = new byte[1024];
        Random.Shared.NextBytes(payload);

        var result = await LimitedStreamReader.ReadAllBytesAsync(new MemoryStream(payload), maxBytes: 2048);

        Assert.Equal(payload, result);
    }

    [Fact]
    public async Task Vuot_gioi_han_thi_nem_loi_va_khong_doc_tiep()
    {
        var payload = new byte[4096];
        var ex = await Assert.ThrowsAsync<StorageFileTooLargeException>(
            () => LimitedStreamReader.ReadAllBytesAsync(new MemoryStream(payload), maxBytes: 1024));

        Assert.Equal(1024, ex.MaxBytes);
    }

    [Fact]
    public async Task Tep_rong_bi_tu_choi() =>
        await Assert.ThrowsAsync<StorageEmptyFileException>(
            () => LimitedStreamReader.ReadAllBytesAsync(new MemoryStream(), maxBytes: 1024));
}
