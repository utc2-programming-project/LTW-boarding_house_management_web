using Web.Features.Tenants.DTOs;

namespace Web.Features.Tenants;

public static class TenantCsvExporter
{
    public static string Build(
        IEnumerable<TenantListItemDto> tenants,
        bool includeAccount,
        Func<string, string> localize)
    {
        var headers = new List<string>
        {
            localize("TenantColumnName"),
            localize("TenantColumnPhone"),
            localize("TenantColumnIdNumber"),
            localize("TenantColumnStatus"),
        };
        if (includeAccount) headers.Add(localize("TenantColumnAccount"));

        var rows = tenants.Select(tenant =>
        {
            var values = new List<string>
            {
                Csv(tenant.FullName),
                Csv(tenant.PhoneNumber),
                Csv(tenant.IdNumberMasked),
                Csv(localize(tenant.IsActive ? "TenantStatusActive" : "TenantStatusInactive")),
            };
            if (includeAccount)
            {
                values.Add(Csv(localize(tenant.HasAccount == true ? "TenantAccountYes" : "TenantAccountNo")));
            }

            return string.Join(',', values);
        });

        return "\uFEFF" + string.Join(
            Environment.NewLine,
            new[] { string.Join(',', headers.Select(Csv)) }.Concat(rows));
    }

    private static string Csv(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && text[0] is '=' or '+' or '-' or '@') text = "'" + text;
        return $"\"{text.Replace("\"", "\"\"")}\"";
    }
}
