using Web.Features.Tenants.Documents;

namespace NhaTro.UnitTests.Features.Tenants;

public class TenantDocumentCategoryRulesTests
{
    [Theory]
    [InlineData("[CCCD_Front] bản chụp", "DocTypeCccdFront")]
    [InlineData("[CCCD_Back] quét lại", "DocTypeCccdBack")]
    [InlineData("[Citizen ID (Front)]", "DocTypeCccdFront")]
    [InlineData("[Contract_Scan]", "DocTypeContractScan")]
    [InlineData("[Temporary_Residence] CT07", "DocTypeResidence")]
    [InlineData("Hợp đồng thuê nhà", "DocTypeContractScan")]
    [InlineData("[Other] giấy ủy quyền", "DocTypeOther")]
    [InlineData("Giấy tờ khác", "DocTypeOther")]
    [InlineData("Citizen ID", "DocTypeIdentity")]
    [InlineData(null, "DocTypeOther")]
    [InlineData("", "DocTypeOther")]
    public void LabelResourceKey_khong_rơi_vao_nhan_giay_to_tuy_than(string? description, string expected) =>
        Assert.Equal(expected, TenantDocumentCategoryRules.LabelResourceKey(description));

    [Theory]
    [InlineData("[CCCD_Front]", TenantDocumentCategoryRules.Identity)]
    [InlineData("[Citizen ID (Back)]", TenantDocumentCategoryRules.Identity)]
    [InlineData("[Contract_Scan]", TenantDocumentCategoryRules.Contract)]
    [InlineData("[Temporary_Residence]", TenantDocumentCategoryRules.Residence)]
    [InlineData("[Other]", TenantDocumentCategoryRules.Other)]
    [InlineData(null, TenantDocumentCategoryRules.Other)]
    public void PillClass_khop_nhom_cua_nhan(string? description, string expected) =>
        Assert.Equal(expected, TenantDocumentCategoryRules.PillClass(description));

    [Theory]
    [InlineData("[CCCD_Front] note", "CCCD_Front")]
    [InlineData("không có prefix", "không có prefix")]
    [InlineData("[]", "[]")]
    [InlineData(null, "")]
    public void ExtractLabel_tra_ve_code_trong_prefix(string? description, string expected) =>
        Assert.Equal(expected, TenantDocumentCategoryRules.ExtractLabel(description));

    [Fact]
    public void Nhan_va_pill_khong_lech_nhau()
    {
        // Mỗi loại giấy tờ phải có nhãn và màu nhóm cùng nhất quán (P2-07/P3-05).
        foreach (var description in new[] { "[CCCD_Front]", "[CCCD_Back]", "[Contract_Scan]", "[Temporary_Residence]", "[Other]" })
        {
            var pill = TenantDocumentCategoryRules.PillClass(description);
            var label = TenantDocumentCategoryRules.LabelResourceKey(description);
            var identityLabel = label is "DocTypeCccdFront" or "DocTypeCccdBack" or "DocTypeIdentity";
            Assert.Equal(identityLabel, pill == TenantDocumentCategoryRules.Identity);
        }
    }
}
