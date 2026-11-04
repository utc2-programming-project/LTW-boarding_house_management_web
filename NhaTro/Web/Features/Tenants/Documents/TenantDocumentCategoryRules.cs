namespace Web.Features.Tenants.Documents;

/// <summary>
/// Nhận diện nhóm giấy tờ từ prefix "[CODE]" trong Description — dùng chung cho danh sách,
/// preview và hộp thoại xóa để ba nơi không thể lệch nhãn với nhau.
/// </summary>
public static class TenantDocumentCategoryRules
{
    public const string Identity = "doc-type-pill-identity";
    public const string Contract = "doc-type-pill-contract";
    public const string Residence = "doc-type-pill-residence";
    public const string Other = "doc-type-pill-other";

    /// <summary>Bỏ prefix "[CODE]" khỏi Description; trả về "CODE" hoặc chuỗi rỗng.</summary>
    public static string ExtractLabel(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return string.Empty;
        if (description.StartsWith('['))
        {
            var endIndex = description.IndexOf(']');
            if (endIndex > 1) return description[1..endIndex];
        }
        return description;
    }

    public static string PillClass(string? description)
    {
        var normalized = ExtractNormalized(description);

        if (normalized.Contains("citizen id") || normalized.Contains("identity")
            || normalized.Contains("cccd") || normalized.Contains("định danh"))
        {
            return Identity;
        }

        if (normalized.Contains("contract") || normalized.Contains("hợp đồng"))
        {
            return Contract;
        }

        if (normalized.Contains("residen") || normalized.Contains("tạm trú")
            || normalized.Contains("ct07") || normalized.Contains("ct08"))
        {
            return Residence;
        }

        return Other;
    }

    /// <summary>
    /// Key tài nguyên cho nhãn hiển thị (caller tự localizer hóa để hàm test được không cần UI).
    /// Nhãn cụ thể (mặt trước/sau) kiểm trước, nhóm kiểm sau, còn lại là "Giấy tờ khác".
    /// </summary>
    public static string LabelResourceKey(string? description)
    {
        var normalized = ExtractNormalized(description);

        if (normalized.Contains("front") || normalized.Contains("mặt trước")) return "DocTypeCccdFront";
        if (normalized.Contains("back") || normalized.Contains("mặt sau")) return "DocTypeCccdBack";
        if (normalized.Contains("contract") || normalized.Contains("hợp đồng")) return "DocTypeContractScan";
        if (normalized.Contains("residen") || normalized.Contains("tạm trú")
            || normalized.Contains("ct07") || normalized.Contains("ct08"))
        {
            return "DocTypeResidence";
        }

        if (normalized.Contains("citizen id") || normalized.Contains("identity")
            || normalized.Contains("cccd") || normalized.Contains("định danh"))
        {
            return "DocTypeIdentity";
        }

        return "DocTypeOther";
    }

    private static string ExtractNormalized(string? description) =>
        ExtractLabel(description).Trim().ToLowerInvariant();
}
