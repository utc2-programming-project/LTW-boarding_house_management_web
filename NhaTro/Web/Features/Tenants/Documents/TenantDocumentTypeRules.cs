namespace Web.Features.Tenants.Documents;

/// <summary>Stable document type keys used to prevent duplicate active files per tenant.</summary>
public static class TenantDocumentTypeRules
{
    public static readonly string[] TypeCodes =
    [
        "CCCD_Front",
        "CCCD_Back",
        "Contract_Scan",
        "Temporary_Residence",
        "Other",
    ];

    public static string? NormalizeTypeCode(string? value) =>
        TypeCodes.FirstOrDefault(code => string.Equals(code, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static bool DescriptionMatches(string? description, string typeCode)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return false;
        }

        var label = description.Trim();
        if (label.StartsWith('['))
        {
            var end = label.IndexOf(']');
            if (end > 1)
            {
                label = label[1..end].Trim();
            }
        }

        if (string.Equals(NormalizeTypeCode(label), typeCode, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Existing records stored the localized label in the description prefix.
        var aliases = typeCode switch
        {
            "CCCD_Front" => new[] { "CCCD mặt trước", "Citizen ID (Front)" },
            "CCCD_Back" => new[] { "CCCD mặt sau", "Citizen ID (Back)" },
            "Contract_Scan" => new[] { "Hợp đồng scan", "Scanned contract" },
            "Temporary_Residence" => new[] { "Tạm trú (CT07)", "Residency permit (CT07)" },
            "Other" => new[] { "Giấy tờ khác", "Other document" },
            _ => [],
        };

        return aliases.Contains(label, StringComparer.OrdinalIgnoreCase);
    }

    public static string BuildDescription(string typeCode, string? note) =>
        string.IsNullOrWhiteSpace(note)
            ? $"[{typeCode}]"
            : $"[{typeCode}] {note.Trim()}";
}
