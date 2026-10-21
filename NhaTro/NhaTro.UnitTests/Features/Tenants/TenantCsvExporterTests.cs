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
            "\uFEFF\"TenantColumnName\",\"TenantColumnPhone\",\"TenantColumnIdNumber\",\"TenantColumnStatus\"",
            csv);
    }

    private static string Localize(string key) => key;
}
