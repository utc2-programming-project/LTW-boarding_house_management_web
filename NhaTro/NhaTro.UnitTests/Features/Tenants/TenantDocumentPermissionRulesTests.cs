using Web.Features.Storage.Entities;
using Web.Features.Tenants.Documents;

namespace NhaTro.UnitTests.Features.Tenants;

// VHT-26: D9 + VHT-12 muc 4 — quyen giay to P3 va du lieu audit khong chua noi dung.
public class TenantDocumentPermissionRulesTests
{
    [Fact]
    public void Admin_khong_co_quyen_nao()
    {
        var access = TenantDocumentAccessLevel.None;

        Assert.False(TenantDocumentPermissionRules.CanView(access));
        Assert.False(TenantDocumentPermissionRules.CanUpload(access));
        Assert.False(TenantDocumentPermissionRules.CanDelete(access));
        Assert.False(TenantDocumentPermissionRules.CanReplace(access));
        Assert.False(TenantDocumentPermissionRules.CanExport(access));
    }

    [Fact]
    public async Task Mac_dinh_quan_ly_chua_duoc_giao_toa_nha_thi_khong_thay_giay_to()
    {
        var scope = new NoManagerTenantScope();

        Assert.False(await scope.IsTenantInScopeAsync("ql-1", 1));
    }

    [Fact]
    public void Khach_thue_xem_upload_va_export_ho_so_cua_minh_nhung_khong_xoa_thay()
    {
        var access = TenantDocumentAccessLevel.Own;

        Assert.True(TenantDocumentPermissionRules.CanView(access));
        Assert.True(TenantDocumentPermissionRules.CanUpload(access));
        Assert.False(TenantDocumentPermissionRules.CanDelete(access));
        Assert.False(TenantDocumentPermissionRules.CanReplace(access));
        Assert.True(TenantDocumentPermissionRules.CanExport(access));
    }

    [Fact]
    public void Chu_tro_duoc_day_du_trong_pham_vi()
    {
        var access = TenantDocumentAccessLevel.Owner;

        Assert.True(TenantDocumentPermissionRules.CanView(access));
        Assert.True(TenantDocumentPermissionRules.CanUpload(access));
        Assert.True(TenantDocumentPermissionRules.CanDelete(access));
        Assert.True(TenantDocumentPermissionRules.CanReplace(access));
        Assert.True(TenantDocumentPermissionRules.CanExport(access));
    }

    [Fact]
    public void Quan_ly_xem_sua_trong_toa_nha_duoc_giao_nhung_khong_xuat()
    {
        var access = TenantDocumentAccessLevel.Manager;

        Assert.True(TenantDocumentPermissionRules.CanView(access));
        Assert.True(TenantDocumentPermissionRules.CanUpload(access));
        Assert.True(TenantDocumentPermissionRules.CanDelete(access));
        Assert.True(TenantDocumentPermissionRules.CanReplace(access));
        Assert.False(TenantDocumentPermissionRules.CanExport(access));
    }

    [Fact]
    public void Ngoai_pham_vi_khong_co_quyen_nao()
    {
        var access = TenantDocumentAccessLevel.None;

        Assert.False(TenantDocumentPermissionRules.CanView(access));
        Assert.False(TenantDocumentPermissionRules.CanUpload(access));
        Assert.False(TenantDocumentPermissionRules.CanDelete(access));
        Assert.False(TenantDocumentPermissionRules.CanReplace(access));
        Assert.False(TenantDocumentPermissionRules.CanExport(access));
    }

    [Fact]
    public void Dto_metadata_khong_lo_duong_dan_noi_bo()
    {
        var names = typeof(TenantDocumentMetadataDto)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("StoredFileName", names);
        Assert.DoesNotContain("RelativePath", names);
        Assert.DoesNotContain("IsDeleted", names);
    }

    [Fact]
    public void Audit_khong_luu_ten_hay_noi_dung_giay_to()
    {
        var names = typeof(FileAccessAuditLog)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.DoesNotContain("FileName", names);
        Assert.DoesNotContain("StoredFileName", names);
        Assert.DoesNotContain("RelativePath", names);
        Assert.DoesNotContain("Content", names);
        Assert.DoesNotContain("ContentType", names);
    }
}
