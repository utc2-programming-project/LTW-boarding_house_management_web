using System.Text;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;

namespace NhaTro.UnitTests.Features.Storage;

// VHT-13: kiem noi dung that (magic bytes), khong tin Content-Type/duoi do client gui.
public class FileContentSnifferTests
{
    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private static byte[] Webp()
    {
        var bytes = new byte[12];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(bytes, 0);
        Encoding.ASCII.GetBytes("WEBP").CopyTo(bytes, 8);
        return bytes;
    }

    private static byte[] Pdf() =>
        [.. Encoding.ASCII.GetBytes("%PDF-1.7"), 0x0A];

    [Fact]
    public void Nhan_dien_jpeg_png_webp_pdf()
    {
        Assert.Equal("image/jpeg", FileContentSniffer.DetectContentType(Jpeg()));
        Assert.Equal("image/png", FileContentSniffer.DetectContentType(Png()));
        Assert.Equal("image/webp", FileContentSniffer.DetectContentType(Webp()));
        Assert.Equal("application/pdf", FileContentSniffer.DetectContentType(Pdf()));
    }

    [Theory]
    [InlineData("MZ")]
    [InlineData("PK")]
    [InlineData("<svg xmlns")]
    [InlineData("<html>")]
    [InlineData("GIF89a")]
    public void Tu_choi_dinh_dang_khong_nam_trong_danh_sach(string content)
    {
        var detected = FileContentSniffer.DetectContentType(Encoding.ASCII.GetBytes(content));
        Assert.Null(detected);
    }

    [Fact]
    public void Noi_dung_qua_ngan_khong_nhan_dien_duoc()
    {
        Assert.Null(FileContentSniffer.DetectContentType([0xFF, 0xD8]));
        Assert.Null(FileContentSniffer.DetectContentType([]));
    }

    [Fact]
    public void Doi_duoi_so_voi_noi_dung_that()
    {
        Assert.True(FileContentSniffer.IsConsistentWithExtension(".jpg", "image/jpeg"));
        Assert.True(FileContentSniffer.IsConsistentWithExtension(".jpeg", "image/jpeg"));
        Assert.False(FileContentSniffer.IsConsistentWithExtension(".pdf", "image/jpeg"));
        Assert.False(FileContentSniffer.IsConsistentWithExtension(".jpg", null));
    }

    [Fact]
    public void Doi_ten_tep_de_lua_khong_qua_duoc_kiem_noi_dung()
    {
        // Noi dung JPEG nhung dat duoi .pdf: khong khop MIME that.
        Assert.False(FileContentSniffer.IsAllowedContent(FileCategory.Invoice, ".pdf", Jpeg()));
    }

    [Fact]
    public void Webp_khong_duoc_chap_nhan_cho_minh_chung_du_noi_dung_dung()
    {
        Assert.False(FileContentSniffer.IsAllowedContent(FileCategory.PaymentProof, ".webp", Webp()));
        Assert.True(FileContentSniffer.IsAllowedContent(FileCategory.RoomPhoto, ".webp", Webp()));
    }

    [Fact]
    public void Noi_dung_dung_va_duoi_dung_thi_duoc_chap_nhan()
    {
        Assert.True(FileContentSniffer.IsAllowedContent(FileCategory.IncidentPhoto, ".jpg", Jpeg()));
        Assert.True(FileContentSniffer.IsAllowedContent(FileCategory.Contract, ".pdf", Pdf()));
        Assert.True(FileContentSniffer.IsAllowedContent(FileCategory.IdentityDocument, ".png", Png()));
    }
}
