using Web.Features.Assistant.Rules;

namespace NhaTro.UnitTests.Features.Assistant;

public class AiPiiMaskerTests
{
    [Fact]
    public void Che_truong_so_cccd_va_tai_khoan()
    {
        var masked = AiPiiMasker.Mask("""
            { "tenants": [{ "fullName": "Nguyễn Văn A", "citizenId": "079203001234", "bankAccountNumber": "0123456789" }] }
            """);

        Assert.DoesNotContain("079203001234", masked);
        Assert.DoesNotContain("0123456789", masked);
        Assert.Contains("\"***\"", masked);
        Assert.Contains("Nguyễn Văn A", masked);
    }

    [Fact]
    public void Che_day_so_dai_ngoai_truong_hop_chuoi_thuong()
    {
        var masked = AiPiiMasker.Mask("""{ "note": "CCCD 079203001234 đã nộp" }""");

        Assert.DoesNotContain("079203001234", masked);
        Assert.Contains("***234", masked);
    }

    [Fact]
    public void Giu_nguyen_so_tien_va_ngay()
    {
        var masked = AiPiiMasker.Mask("""{ "amount": 12000000, "dueDate": "2026-11-25", "soDienThoaiNoiBo": "12345" }""");

        Assert.Contains("12000000", masked);
        Assert.Contains("2026-11-25", masked);
        Assert.Contains("12345", masked);
    }

    [Fact]
    public void Giu_so_dien_thoai_di_dong_va_email_de_lien_he()
    {
        var masked = AiPiiMasker.Mask(
            """{ "phoneNumber": "0901234567", "email": "a@example.com", "note": "gọi 0321234567" }""");

        Assert.Contains("0901234567", masked);
        Assert.Contains("a@example.com", masked);
        Assert.Contains("0321234567", masked);
    }

    [Fact]
    public void Chuoi_rong_tra_ve_object_rong()
    {
        Assert.Equal("{}", AiPiiMasker.Mask(null));
        Assert.Equal("{}", AiPiiMasker.Mask("  "));
    }
}
