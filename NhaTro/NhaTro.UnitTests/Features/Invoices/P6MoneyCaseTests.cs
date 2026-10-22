using NhaTro.UnitTests.TestSupport;

namespace NhaTro.UnitTests.Features.Invoices;

/// <summary>
/// VHT-14 (đợt 1A): case nền P6 được chốt bằng unit test trước khi có service hóa đơn thật.
/// Đủ 3 loại: thành công, sai dữ liệu, sai quyền.
/// </summary>
public sealed class P6MoneyCaseTests
{
    [Fact]
    public void Case_thanh_cong_co_tong_tien_dung()
    {
        var testCase = P6MoneyCaseCatalog.Success;

        Assert.Equal(P6CaseKind.Success, testCase.Kind);
        Assert.True(testCase.HasValidAmounts);
        Assert.True(testCase.IsAuthorized);
        Assert.Equal(3_870_000m, testCase.ExpectedTotal);
        Assert.Null(testCase.ExpectedError);
    }

    [Fact]
    public void Case_sai_du_lieu_bi_danh_dau_khong_hop_le()
    {
        var testCase = P6MoneyCaseCatalog.BadData;

        Assert.Equal(P6CaseKind.BadData, testCase.Kind);
        Assert.False(testCase.HasValidAmounts);
        Assert.False(string.IsNullOrWhiteSpace(testCase.ExpectedError));
    }

    [Fact]
    public void Case_sai_quyen_bi_danh_dau_la_khong_duoc_phep()
    {
        var testCase = P6MoneyCaseCatalog.Unauthorized;

        Assert.Equal(P6CaseKind.Unauthorized, testCase.Kind);
        Assert.False(testCase.IsAuthorized);
        Assert.False(string.IsNullOrWhiteSpace(testCase.ExpectedError));
    }

    [Fact]
    public void Catalog_du_ba_loai_case_va_ma_khong_trung()
    {
        var cases = P6MoneyCaseCatalog.All;

        Assert.Equal(3, cases.Count);
        Assert.Equal(3, cases.Select(c => c.Kind).Distinct().Count());
        Assert.Equal(3, cases.Select(c => c.Code).Distinct().Count());
        Assert.Contains(cases, c => c.Kind == P6CaseKind.Success);
        Assert.Contains(cases, c => c.Kind == P6CaseKind.BadData);
        Assert.Contains(cases, c => c.Kind == P6CaseKind.Unauthorized);
    }
}
