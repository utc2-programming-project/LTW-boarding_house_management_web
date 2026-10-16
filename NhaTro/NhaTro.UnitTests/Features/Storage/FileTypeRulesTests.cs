using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;

namespace NhaTro.UnitTests.Features.Storage;

// VHT-13: kiem tra loai/duoi/doi tuong lien quan theo hop dong VHT-12 muc 1-2.
public class FileTypeRulesTests
{
    [Theory]
    [InlineData(FileCategory.RoomPhoto, ".jpg,.jpeg,.png,.webp")]
    [InlineData(FileCategory.IncidentPhoto, ".jpg,.jpeg,.png,.webp")]
    [InlineData(FileCategory.Contract, ".pdf")]
    [InlineData(FileCategory.Invoice, ".pdf,.jpg,.jpeg,.png")]
    [InlineData(FileCategory.IdentityDocument, ".pdf,.jpg,.jpeg,.png")]
    [InlineData(FileCategory.PaymentProof, ".pdf,.jpg,.jpeg,.png")]
    public void Danh_sach_duoi_theo_loai_dung_voi_hop_dong(FileCategory category, string expected) =>
        Assert.Equal(expected, string.Join(",", FileTypeRules.AllowedExtensions(category)));

    [Theory]
    [InlineData(FileCategory.RoomPhoto, RelatedEntityType.Room, true)]
    [InlineData(FileCategory.RoomPhoto, RelatedEntityType.Tenant, false)]
    [InlineData(FileCategory.Contract, RelatedEntityType.Contract, true)]
    [InlineData(FileCategory.Invoice, RelatedEntityType.Invoice, true)]
    [InlineData(FileCategory.IdentityDocument, RelatedEntityType.Tenant, true)]
    [InlineData(FileCategory.IncidentPhoto, RelatedEntityType.MaintenanceIncident, true)]
    [InlineData(FileCategory.PaymentProof, RelatedEntityType.Payment, true)]
    [InlineData(FileCategory.PaymentProof, RelatedEntityType.Invoice, false)]
    public void Cap_loai_tep_va_doi_tuong_phai_khop(
        FileCategory category,
        RelatedEntityType entityType,
        bool expected) =>
        Assert.Equal(expected, FileTypeRules.IsPairValid(category, entityType));

    [Theory]
    [InlineData("vong-nuoc-ro.JPG", ".jpg")]
    [InlineData("hop-dong.Pdf", ".pdf")]
    [InlineData("C:\\Users\\a\\anh.png", ".png")]
    [InlineData("../../etc/passwd.jpg", ".jpg")]
    [InlineData("no-extension", "")]
    [InlineData("trailing.", "")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("archive.tar.gz", ".gz")]
    public void GetExtension_lay_dung_duoi_va_bo_ky_tu_duong_dan(string? fileName, string expected) =>
        Assert.Equal(expected, FileTypeRules.GetExtension(fileName));

    [Fact]
    public void Webp_chi_danh_cho_anh()
    {
        Assert.True(FileTypeRules.IsExtensionAllowed(FileCategory.RoomPhoto, ".webp"));
        Assert.True(FileTypeRules.IsExtensionAllowed(FileCategory.IncidentPhoto, ".webp"));
        Assert.False(FileTypeRules.IsExtensionAllowed(FileCategory.PaymentProof, ".webp"));
        Assert.False(FileTypeRules.IsExtensionAllowed(FileCategory.Contract, ".webp"));
    }

    [Theory]
    [InlineData(FileCategory.Contract, ".pdf", true)]
    [InlineData(FileCategory.Contract, ".jpg", false)]
    [InlineData(FileCategory.Invoice, ".png", true)]
    [InlineData(FileCategory.PaymentProof, ".jpg", true)]
    public void Chi_cho_duoi_trong_danh_sach_cua_loai(
        FileCategory category,
        string extension,
        bool expected) =>
        Assert.Equal(expected, FileTypeRules.IsExtensionAllowed(category, extension));

    [Fact]
    public void ContentType_do_server_xac_dinh_tu_duoi()
    {
        Assert.Equal("image/jpeg", FileTypeRules.ContentTypeForExtension(".jpg"));
        Assert.Equal("image/jpeg", FileTypeRules.ContentTypeForExtension(".JPeG"));
        Assert.Equal("image/png", FileTypeRules.ContentTypeForExtension(".png"));
        Assert.Equal("image/webp", FileTypeRules.ContentTypeForExtension(".webp"));
        Assert.Equal("application/pdf", FileTypeRules.ContentTypeForExtension(".pdf"));
        Assert.Equal("application/octet-stream", FileTypeRules.ContentTypeForExtension(".zip"));
    }

    [Fact]
    public void Gioi_han_mac_dinh_dung_theo_hop_dong()
    {
        Assert.Equal(5L * 1024 * 1024, FileTypeRules.DefaultMaxBytes(FileCategory.RoomPhoto));
        Assert.Equal(5L * 1024 * 1024, FileTypeRules.DefaultMaxBytes(FileCategory.PaymentProof));
        Assert.Equal(10L * 1024 * 1024, FileTypeRules.DefaultMaxBytes(FileCategory.Contract));
        Assert.Equal(10L * 1024 * 1024, FileTypeRules.DefaultMaxBytes(FileCategory.IdentityDocument));
    }

    [Fact]
    public void Mo_ta_duoi_dung_cho_thong_bao_loi() =>
        Assert.Equal(".jpg, .jpeg, .png, .webp", FileTypeRules.DescribeAllowedExtensions(FileCategory.IncidentPhoto));
}
