using Microsoft.Extensions.Localization;

namespace Web.Features.Utilities.Services;

/// <summary>
/// Chuyển nhãn trường trong nhật ký điều chỉnh chỉ số sang bản dịch hiện hành.
/// Dữ liệu mẫu lưu tên trường bằng tiếng Việt nên phải ánh xạ sang resource key
/// trước khi tra cứu, tránh hiển thị key thô "Audit...".
/// </summary>
public static class UtilityAuditLogDisplay
{
    private static readonly Dictionary<string, string> FieldKeyMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Chỉ số điện cuối"] = "AuditElectricMeterReading",
        ["Chỉ số điện cuối kỳ"] = "AuditElectricMeterReading",
        ["Electric meter end reading"] = "AuditElectricMeterReading",
        ["ElectricMeterReading"] = "AuditElectricMeterReading",
        ["Chỉ số nước cuối"] = "AuditWaterMeterReading",
        ["Chỉ số nước cuối kỳ"] = "AuditWaterMeterReading",
        ["Water meter end reading"] = "AuditWaterMeterReading",
        ["WaterMeterReading"] = "AuditWaterMeterReading",
    };

    /// <summary>Trả về nhãn đã dịch cho trường bị điều chỉnh.</summary>
    public static string FieldLabel(IStringLocalizer<UtilityResources> localizer, string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return localizer["NoData"].Value;
        }

        var trimmed = field.Trim();
        if (FieldKeyMap.TryGetValue(trimmed, out var key))
        {
            return localizer[key].Value;
        }

        var direct = localizer[$"Audit{trimmed}"];
        return direct.ResourceNotFound ? trimmed : direct.Value;
    }
}
