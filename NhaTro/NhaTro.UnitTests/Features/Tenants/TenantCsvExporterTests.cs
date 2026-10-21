using Web.Features.Tenants;
using Web.Features.Tenants.DTOs;

namespace NhaTro.UnitTests.Features.Tenants;

public class TenantCsvExporterTests
{
    [Fact]
    public void Build_includes_localized_headers_and_escapes_cells()
    {
        var tenant = new TenantListItemDto
        {
            Id = 1,
            FullName = "=SUM(\"A\")",
            PhoneNumber = "0912345678",
            IdNumberMasked = null,
            IsActive = true,
            HasAccount = true,
            HasActiveContract = false,
        };

        var csv = TenantCsvExporter.Build([tenant], includeAccount: true, Localize);

        Assert.StartsWith("\uFEFF", csv);
        Assert.Contains("\"TenantColumnAccount\"", csv);
        Assert.Contains("\"'=SUM(\"\"A\"\")\"", csv);
        Assert.Contains("\"TenantAccountYes\"", csv);
    }

    [Fact]
    public void Build_omits_account_column_when_not_requested()
    {
        var csv = TenantCsvExporter.Build([], includeAccount: false, Localize);

        Assert.DoesNotContain("TenantColumnAccount", csv);
        Assert.Equal(
            "\uFEFF\"TenantColumnName\",\"TenantColumnPhone\",\"TenantColumnStatus\"",
            csv);
    }

    // B17-05: file xuất không được chứa số CCCD, kể cả bản che, với mọi vai trò.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Build_never_exports_id_number_column_or_value(bool includeAccount)
    {
        var tenant = new TenantListItemDto
        {
            Id = 2,
            FullName = "Nguyen Van A",
            PhoneNumber = "0912345678",
            IdNumberMasked = "********4821",
            IsActive = true,
            HasAccount = false,
            HasActiveContract = false,
        };

        var csv = TenantCsvExporter.Build([tenant], includeAccount, Localize);

        Assert.DoesNotContain("TenantColumnIdNumber", csv);
        Assert.DoesNotContain("4821", csv);
        Assert.DoesNotContain("********", csv);
    }

    private static string Localize(string key) => key;
}
