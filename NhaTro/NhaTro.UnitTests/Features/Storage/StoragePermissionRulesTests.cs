using Web.Features.Accounts;
using Web.Features.Storage.Enums;
using Web.Features.Storage.Rules;

namespace NhaTro.UnitTests.Features.Storage;

// VHT-13: ma tran quyen VHT-12 muc 4, dac biet Admin khong co quyen nghiep vu (D1-D17 12/10/2026).
public class StoragePermissionRulesTests
{
    public static TheoryData<FileCategory> AllCategories() =>
    [
        FileCategory.RoomPhoto,
        FileCategory.Contract,
        FileCategory.Invoice,
        FileCategory.IdentityDocument,
        FileCategory.IncidentPhoto,
        FileCategory.PaymentProof,
    ];

    [Theory]
    [MemberData(nameof(AllCategories))]
    public void Admin_khong_duoc_upload_bat_ky_loai_nao(FileCategory category)
    {
        Assert.False(StoragePermissionRules.CanUpload(Roles.Admin, category));
        Assert.False(StoragePermissionRules.CanDownload(Roles.Admin, isUploader: true, relatedEntityInScope: true));
        Assert.False(StoragePermissionRules.CanDelete(Roles.Admin));
    }

    [Theory]
    [InlineData(FileCategory.RoomPhoto)]
    [InlineData(FileCategory.Contract)]
    [InlineData(FileCategory.Invoice)]
    [InlineData(FileCategory.IdentityDocument)]
    [InlineData(FileCategory.IncidentPhoto)]
    public void ChuTro_va_QuanLy_duoc_upload_cac_loai_da_chot(FileCategory category)
    {
        Assert.True(StoragePermissionRules.CanUpload(Roles.ChuTro, category));
        Assert.True(StoragePermissionRules.CanUpload(Roles.QuanLy, category));
    }

    [Theory]
    [InlineData(Roles.ChuTro)]
    [InlineData(Roles.QuanLy)]
    public void ChuTro_va_QuanLy_bi_chan_upload_PaymentProof_vi_chua_chot(string role)
    {
        // VHT-12 mục 4.1: PaymentProof cho Chủ trọ/Quản lý "Cần chốt" -> deny-by-default.
        Assert.False(StoragePermissionRules.CanUpload(role, FileCategory.PaymentProof));
    }

    [Theory]
    [InlineData(FileCategory.IdentityDocument, true)]
    [InlineData(FileCategory.IncidentPhoto, true)]
    [InlineData(FileCategory.PaymentProof, true)]
    [InlineData(FileCategory.RoomPhoto, false)]
    [InlineData(FileCategory.Contract, false)]
    [InlineData(FileCategory.Invoice, false)]
    public void KhachThue_chi_upload_giay_to_anh_su_co_va_minh_chung(FileCategory category, bool expected) =>
        Assert.Equal(expected, StoragePermissionRules.CanUpload(Roles.KhachThue, category));

    [Fact]
    public void KhachThue_khong_tai_duoc_tep_ngoai_pham_vi()
    {
        Assert.False(StoragePermissionRules.CanDownload(
            Roles.KhachThue, isUploader: false, relatedEntityInScope: false));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void KhachThue_tai_duoc_tep_cua_minh_hoac_trong_pham_vi(bool isUploader, bool inScope) =>
        Assert.True(StoragePermissionRules.CanDownload(Roles.KhachThue, isUploader, inScope));

    [Fact]
    public void KhachThue_bi_chan_xoa_mac_dinh_cho_toi_khi_chot()
    {
        // VHT-12 mục 4.3: khách xóa tệp của mình "Cần chốt" -> deny-by-default.
        Assert.False(StoragePermissionRules.CanDelete(Roles.KhachThue));
    }

    [Fact]
    public void Vai_tro_khong_xac_dinh_bi_tu_choi()
    {
        Assert.False(StoragePermissionRules.CanUpload("Guest", FileCategory.RoomPhoto));
        Assert.False(StoragePermissionRules.CanDownload("Guest", isUploader: true, relatedEntityInScope: true));
        Assert.False(StoragePermissionRules.CanDelete("Guest"));
    }
}
