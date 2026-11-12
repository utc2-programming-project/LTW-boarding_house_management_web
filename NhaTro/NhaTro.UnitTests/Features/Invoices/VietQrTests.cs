using Web.Features.Invoices;

namespace NhaTro.UnitTests.Features.Invoices;

public class VietQrTests
{
    // Vector chuẩn EMV QR/VietQR cho TECHCOMBANK (BIN 970407) · 2145181026 · 500.000 · "HD1".
    // Thuật toán TLV + CRC16-CCITT đã đối chiếu khớp vector chính thức của API VietQR
    // trước khi đổi sang dữ liệu mẫu hiện tại.
    private const string OfficialPayload =
        "00020101021238540010A00000072701240006970407011021451810260208QRIBFTTA530370454065000005802VN62070803HD16304207B";

    [Fact]
    public void Payload_khop_chinh_xac_mau_vietqr_cong_bo()
    {
        var payload = VietQr.GeneratePayload("TECHCOMBANK", "2145181026", 500_000m, "HD1");

        Assert.Equal(OfficialPayload, payload);
    }

    [Fact]
    public void Payload_co_tag_53_dung_do_dai_va_so_tien_o_tag_54()
    {
        var payload = VietQr.GeneratePayload("TECHCOMBANK", "2145181026", 500_000m, "HD1");

        Assert.Contains("5303704", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("530303704", payload, StringComparison.Ordinal);
        Assert.Contains("5406500000", payload, StringComparison.Ordinal);
    }

    [Fact]
    public void Payload_bo_khoang_trang_trong_so_tai_khoan_va_lam_tron_so_tien()
    {
        var payload = VietQr.GeneratePayload("BIDV", " 8800 0849 09 ", 1_000.4m, "HD-9");

        Assert.Contains("01108800084909", payload, StringComparison.Ordinal);
        Assert.Contains("54041000", payload, StringComparison.Ordinal);
        Assert.Equal("6304", payload[^8..^4]);
    }

    [Fact]
    public void Ma_ngan_hang_khong_ho_tro_thi_bao_loi()
    {
        Assert.Throws<ArgumentException>(
            () => VietQr.GeneratePayload("KHONGCO", "123456", 1_000m, "HD1"));
    }

    [Theory]
    [InlineData("ICB", "970415")]
    [InlineData("vcb", "970436")]
    [InlineData("MBBANK", "970422")]
    public void Danh_muc_ngan_hang_tra_dung_bin(string code, string expectedBin)
    {
        Assert.Equal(expectedBin, VietQrBankCatalog.GetBin(code));
    }
}
