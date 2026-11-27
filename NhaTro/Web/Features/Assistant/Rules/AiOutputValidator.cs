using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Web.Features.Assistant.Enums;

namespace Web.Features.Assistant.Rules;

public sealed record AiValidationResult(
    bool Succeeded,
    string? NormalizedJson,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AiValidationResult Success(JsonObject node) => new(
        true,
        node.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)),
        null,
        null);

    public static AiValidationResult Failure(string message) => new(
        false,
        null,
        Assistant.AiErrorCodes.InvalidOutput,
        message);
}

/// <summary>
/// Kiểm tra thủ công cấu trúc đầu ra theo schema của use case. Không phụ thuộc thư viện
/// JSON Schema để giữ nhẹ; mọi giới hạn độ dài/số phần tử đều chặn dữ liệu quá lớn.
/// </summary>
public static class AiOutputValidator
{
    private const int MaxJsonLength = 20_000;

    public static AiValidationResult Validate(AiUseCase useCase, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AiValidationResult.Failure("Model trả về nội dung rỗng.");
        }

        if (json.Length > MaxJsonLength)
        {
            return AiValidationResult.Failure("Nội dung đề xuất vượt quá giới hạn cho phép.");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(json);
        }
        catch (JsonException)
        {
            return AiValidationResult.Failure("Nội dung trả về không phải JSON hợp lệ.");
        }

        if (node is not JsonObject root)
        {
            return AiValidationResult.Failure("Đề xuất phải là một đối tượng JSON.");
        }

        return useCase switch
        {
            AiUseCase.ReportSummary => ValidateReportSummary(root),
            AiUseCase.ReminderMessage => ValidateReminder(root),
            AiUseCase.RoomDraft => ValidateRoomDraft(root),
            AiUseCase.IncidentTriage => ValidateIncident(root),
            AiUseCase.DailySummary => ValidateDailySummary(root),
            AiUseCase.TenantDraft => ValidateTenantDraft(root),
            AiUseCase.ServiceDraft => ValidateServiceDraft(root),
            AiUseCase.ContractDraft => ValidateContractDraft(root),
            _ => AiValidationResult.Failure("Use case không hợp lệ."),
        };
    }

    private static AiValidationResult ValidateReportSummary(JsonObject root)
    {
        if (!TryString(root, "headline", 200, out var error)
            || !TryString(root, "summary", 3000, out error)
            || !TryArray(root, "insights", 10, out var insights, out error)
            || !TryArray(root, "actions", 10, out var actions, out error)
            || !TryArray(root, "metrics", 12, out var metrics, out error))
        {
            return AiValidationResult.Failure(error!);
        }

        foreach (var item in insights)
        {
            if (item is not JsonObject insight
                || !TryString(insight, "title", 150, out error)
                || !TryString(insight, "detail", 800, out error)
                || !TryEnum(insight, "severity", ["info", "warning", "critical"], out error))
            {
                return AiValidationResult.Failure(error ?? "Mục nhận định không hợp lệ.");
            }
        }

        foreach (var item in actions)
        {
            if (item is not JsonObject action
                || !TryString(action, "title", 200, out error)
                || !TryString(action, "detail", 800, out error)
                || !TryOptionalString(action, "link", 300, out error)
                || !IsSafeLink(action["link"], out error))
            {
                return AiValidationResult.Failure(error ?? "Gợi ý hành động không hợp lệ.");
            }
        }

        foreach (var item in metrics)
        {
            if (item is not JsonObject metric
                || !TryString(metric, "label", 100, out error)
                || !TryString(metric, "value", 100, out error))
            {
                return AiValidationResult.Failure(error ?? "Chỉ số không hợp lệ.");
            }
        }

        return AiValidationResult.Success(root);
    }

    private static AiValidationResult ValidateReminder(JsonObject root)
    {
        if (!TryString(root, "title", 200, out var error)
            || !TryString(root, "body", 2000, out error)
            || !TryOptionalString(root, "notes", 500, out error))
        {
            return AiValidationResult.Failure(error!);
        }

        return AiValidationResult.Success(root);
    }

    private static AiValidationResult ValidateRoomDraft(JsonObject root)
    {
        if (!TryArray(root, "rooms", 200, out var rooms, out var error)
            || !TryOptionalString(root, "notes", 500, out error))
        {
            return AiValidationResult.Failure(error!);
        }

        if (rooms.Count == 0)
        {
            return AiValidationResult.Failure("Bản nháp phải có ít nhất một phòng.");
        }

        var property = root["property"];
        if (property is not null and not JsonObject)
        {
            return AiValidationResult.Failure("Thông tin nhà không hợp lệ.");
        }

        if (property is JsonObject propertyObject
            && (!TryString(propertyObject, "name", 200, out error)
                || !TryString(propertyObject, "address", 500, out error)))
        {
            return AiValidationResult.Failure(error!);
        }

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in rooms)
        {
            if (item is not JsonObject room
                || !TryString(room, "code", 50, out error)
                || !TryOptionalInt(room, "floor", 0, 1000, out error)
                || !TryOptionalNumber(room, "areaM2", 0m, 100_000m, out error)
                || !TryNumber(room, "standardPrice", 0m, 1_000_000_000m, out error))
            {
                return AiValidationResult.Failure(error ?? "Phòng trong bản nháp không hợp lệ.");
            }

            var code = room["code"]!.GetValue<string>().Trim();
            if (!codes.Add(code))
            {
                return AiValidationResult.Failure($"Mã phòng trùng trong bản nháp: {code}.");
            }

            room["code"] = code;
        }

        return AiValidationResult.Success(root);
    }

    private static AiValidationResult ValidateIncident(JsonObject root)
    {
        if (!TryEnum(root, "category", ["Electrical", "Water", "Furniture", "Equipment", "Other"], out var error)
            || !TryEnum(root, "urgency", ["Normal", "Urgent", "Emergency"], out error)
            || !TryOptionalString(root, "assignee", 200, out error)
            || !TryString(root, "reason", 1000, out error)
            || !TryOptionalString(root, "suggestedTitle", 200, out error)
            || !TryOptionalString(root, "suggestedDescription", 2000, out error))
        {
            return AiValidationResult.Failure(error!);
        }

        return AiValidationResult.Success(root);
    }

    private static AiValidationResult ValidateDailySummary(JsonObject root)
    {
        if (!TryString(root, "headline", 200, out var error)
            || !TryString(root, "summary", 2000, out error)
            || !TryArray(root, "items", 15, out var items, out error))
        {
            return AiValidationResult.Failure(error!);
        }

        foreach (var item in items)
        {
            if (item is not JsonObject workItem
                || !TryString(workItem, "label", 200, out error)
                || !TryInt(workItem, "count", 0, 1_000_000, out error)
                || !TryOptionalString(workItem, "link", 300, out error)
                || !IsSafeLink(workItem["link"], out error)
                || !TryEnum(workItem, "severity", ["info", "warning", "critical"], out error)
                || !TryOptionalString(workItem, "detail", 500, out error))
            {
                return AiValidationResult.Failure(error ?? "Mục việc cần làm không hợp lệ.");
            }
        }

        return AiValidationResult.Success(root);
    }

    private static AiValidationResult ValidateTenantDraft(JsonObject root)
    {
        if (!TryString(root, "fullName", 200, out var error)
            || !TryString(root, "phoneNumber", 30, out error)
            || !TryOptionalString(root, "email", 256, out error)
            || !TryOptionalDate(root, "dateOfBirth", out error)
            || !TryOptionalEnum(root, "gender", ["Male", "Female", "Other"], out error)
            || !TryOptionalString(root, "permanentAddress", 300, out error)
            || !TryOptionalString(root, "note", 500, out error))
        {
            return AiValidationResult.Failure(error!);
        }

        var phone = root["phoneNumber"]!.GetValue<string>();
        var digits = phone.Count(char.IsDigit);
        if (digits < 8 || digits > 15)
        {
            return AiValidationResult.Failure("Số điện thoại phải có 8–15 chữ số.");
        }

        if (root["email"] is JsonValue emailValue
            && emailValue.TryGetValue<string>(out var email)
            && (!email.Contains('@') || email.StartsWith('@') || email.EndsWith('@')))
        {
            return AiValidationResult.Failure("Email trong bản nháp không hợp lệ.");
        }

        return AiValidationResult.Success(root);
    }

    private static AiValidationResult ValidateServiceDraft(JsonObject root)
    {
        if (!TryString(root, "name", 200, out var error)
            || !TryString(root, "unit", 30, out error)
            || !TryEnum(root, "calculationMethod", ["fixed-monthly", "meter", "tiered", "custom"], out error)
            || !TryNumber(root, "unitPrice", 1m, 1_000_000_000_000m, out error)
            || !TryDate(root, "effectiveDate", out error)
            || !TryOptionalDate(root, "effectiveTo", out error)
            || !TryOptionalString(root, "note", 500, out error)
            || !TryOptionalString(root, "description", 500, out error))
        {
            return AiValidationResult.Failure(error!);
        }

        if (root["effectiveTo"] is JsonValue toValue
            && toValue.TryGetValue<string>(out var toText)
            && DateOnly.TryParseExact(toText, "yyyy-MM-dd", out var toDate)
            && DateOnly.TryParseExact(root["effectiveDate"]!.GetValue<string>(), "yyyy-MM-dd", out var fromDate)
            && toDate <= fromDate)
        {
            return AiValidationResult.Failure("Ngày kết thúc giá phải sau ngày hiệu lực.");
        }

        return AiValidationResult.Success(root);
    }

    private static AiValidationResult ValidateContractDraft(JsonObject root)
    {
        if (!TryOptionalInt(root, "roomId", 1, int.MaxValue, out var error)
            || !TryOptionalString(root, "roomCode", 50, out error)
            || !TryArray(root, "tenantIds", 10, out var tenantIds, out error)
            || !TryDate(root, "startDate", out error)
            || !TryDate(root, "endDate", out error)
            || !TryOptionalNumber(root, "depositAmount", 0m, 9_999_999_999_999_999.99m, out error)
            || !TryOptionalNumber(root, "monthlyRent", 0m, 9_999_999_999_999_999.99m, out error)
            || !TryInt(root, "paymentCycleMonths", 1, 12, out error)
            || !TryOptionalInt(root, "meterReadingDay", 1, 28, out error)
            || !TryOptionalString(root, "terms", 4000, out error))
        {
            return AiValidationResult.Failure(error!);
        }

        if (root["roomId"] is not JsonValue)
        {
            return AiValidationResult.Failure(
                "Chưa xác định được phòng — hãy ghi rõ mã phòng còn trống.");
        }

        if (root["paymentCycleMonths"]!.GetValue<int>() is not (1 or 2 or 3 or 6 or 12))
        {
            return AiValidationResult.Failure("Kỳ thanh toán chỉ nhận 1, 2, 3, 6 hoặc 12 tháng.");
        }

        if (tenantIds.Count == 0)
        {
            return AiValidationResult.Failure(
                "Chưa xác định được khách thuê — hãy ghi rõ họ tên đầy đủ hoặc số điện thoại của khách.");
        }

        var ids = new HashSet<int>();
        foreach (var item in tenantIds)
        {
            if (item is not JsonValue value || !value.TryGetValue<int>(out var tenantId) || tenantId <= 0)
            {
                return AiValidationResult.Failure("Danh sách khách thuê không hợp lệ.");
            }

            if (!ids.Add(tenantId))
            {
                return AiValidationResult.Failure("Danh sách khách thuê bị trùng.");
            }
        }

        var start = DateOnly.ParseExact(root["startDate"]!.GetValue<string>(), "yyyy-MM-dd");
        var end = DateOnly.ParseExact(root["endDate"]!.GetValue<string>(), "yyyy-MM-dd");
        if (end <= start)
        {
            return AiValidationResult.Failure("Ngày kết thúc hợp đồng phải sau ngày bắt đầu.");
        }

        return AiValidationResult.Success(root);
    }

    /// <summary>Tên trường theo tiếng Việt để thông báo cho Chủ trọ dễ hiểu, không lộ tên JSON.</summary>
    private static string FieldLabel(string name) => name switch
    {
        "headline" => "tiêu đề",
        "summary" => "tóm tắt",
        "title" => "tiêu đề",
        "detail" => "chi tiết",
        "label" => "nhãn",
        "value" => "giá trị",
        "link" => "liên kết",
        "severity" => "mức độ",
        "count" => "số lượng",
        "body" => "nội dung tin nhắn",
        "fullName" => "họ tên khách thuê",
        "phoneNumber" => "số điện thoại",
        "email" => "email",
        "dateOfBirth" => "ngày sinh",
        "gender" => "giới tính",
        "permanentAddress" => "địa chỉ thường trú",
        "note" => "ghi chú",
        "notes" => "ghi chú",
        "name" => "tên",
        "unit" => "đơn vị",
        "calculationMethod" => "cách tính",
        "unitPrice" => "đơn giá",
        "effectiveDate" => "ngày hiệu lực",
        "effectiveTo" => "ngày kết thúc giá",
        "description" => "mô tả",
        "category" => "nhóm sự cố",
        "urgency" => "mức ưu tiên",
        "reason" => "lý do",
        "assignee" => "người xử lý",
        "suggestedTitle" => "tiêu đề gợi ý",
        "suggestedDescription" => "mô tả gợi ý",
        "code" => "mã phòng",
        "roomCode" => "mã phòng",
        "address" => "địa chỉ",
        "floor" => "tầng",
        "standardPrice" => "giá chuẩn",
        "rooms" => "danh sách phòng",
        "startDate" => "ngày bắt đầu",
        "endDate" => "ngày kết thúc",
        "depositAmount" => "tiền cọc",
        "monthlyRent" => "giá thuê",
        "paymentCycleMonths" => "kỳ thanh toán",
        "meterReadingDay" => "ngày chốt chỉ số",
        "tenantIds" => "khách thuê",
        "terms" => "điều khoản",
        _ => name,
    };

    private static string Capitalize(string text) =>
        text.Length == 0 ? text : char.ToUpper(text[0], CultureInfo.CurrentCulture) + text[1..];

    private static bool TryString(JsonObject node, string name, int maxLength, out string? error)
    {
        error = null;
        if (node[name] is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            error = $"Thiếu {FieldLabel(name)}.";
            return false;
        }

        text = text.Trim();
        if (text.Length == 0)
        {
            error = $"Thiếu {FieldLabel(name)}.";
            return false;
        }

        if (text.Length > maxLength)
        {
            error = $"{Capitalize(FieldLabel(name))} quá dài (tối đa {maxLength} ký tự).";
            return false;
        }

        node[name] = text;
        return true;
    }

    private static bool TryOptionalString(JsonObject node, string name, int maxLength, out string? error)
    {
        error = null;
        var value = node[name];
        if (value is null)
        {
            return true;
        }

        if (value is not JsonValue scalar || !scalar.TryGetValue<string>(out var text))
        {
            error = $"{Capitalize(FieldLabel(name))} không hợp lệ.";
            return false;
        }

        text = text.Trim();
        if (text.Length > maxLength)
        {
            error = $"{Capitalize(FieldLabel(name))} quá dài (tối đa {maxLength} ký tự).";
            return false;
        }

        node[name] = text.Length == 0 ? null : text;
        return true;
    }

    private static bool TryArray(
        JsonObject node, string name, int maxItems, out JsonArray items, out string? error)
    {
        error = null;
        items = new JsonArray();
        if (node[name] is not JsonArray array)
        {
            error = $"Thiếu {FieldLabel(name)}.";
            return false;
        }

        if (array.Count > maxItems)
        {
            error = $"{Capitalize(FieldLabel(name))} có quá nhiều mục (tối đa {maxItems}).";
            return false;
        }

        items = array;
        return true;
    }

    private static bool TryNumber(JsonObject node, string name, decimal min, decimal max, out string? error)
    {
        error = null;
        if (node[name] is not JsonValue value || !value.TryGetValue<decimal>(out var number))
        {
            error = $"{Capitalize(FieldLabel(name))} phải là số.";
            return false;
        }

        if (number < min || number > max)
        {
            error = $"{Capitalize(FieldLabel(name))} không hợp lệ.";
            return false;
        }

        return true;
    }

    private static bool TryOptionalNumber(
        JsonObject node, string name, decimal min, decimal max, out string? error)
    {
        error = null;
        var value = node[name];
        if (value is null)
        {
            return true;
        }

        if (value is not JsonValue scalar || !scalar.TryGetValue<decimal>(out var number))
        {
            error = $"{Capitalize(FieldLabel(name))} phải là số hoặc để trống.";
            return false;
        }

        if (number < min || number > max)
        {
            error = $"{Capitalize(FieldLabel(name))} không hợp lệ.";
            return false;
        }

        return true;
    }

    private static bool TryInt(JsonObject node, string name, int min, int max, out string? error)
    {
        error = null;
        if (node[name] is not JsonValue value || !value.TryGetValue<int>(out var number))
        {
            error = $"{Capitalize(FieldLabel(name))} phải là số nguyên.";
            return false;
        }

        if (number < min || number > max)
        {
            error = $"{Capitalize(FieldLabel(name))} không hợp lệ.";
            return false;
        }

        return true;
    }

    private static bool TryOptionalInt(
        JsonObject node, string name, int min, int max, out string? error)
    {
        error = null;
        var value = node[name];
        if (value is null)
        {
            return true;
        }

        if (value is not JsonValue scalar || !scalar.TryGetValue<int>(out var number))
        {
            error = $"{Capitalize(FieldLabel(name))} phải là số nguyên hoặc để trống.";
            return false;
        }

        if (number < min || number > max)
        {
            error = $"{Capitalize(FieldLabel(name))} không hợp lệ.";
            return false;
        }

        return true;
    }

    private static bool TryDate(JsonObject node, string name, out string? error)
    {
        error = null;
        if (node[name] is not JsonValue value
            || !value.TryGetValue<string>(out var text)
            || !DateOnly.TryParseExact(text, "yyyy-MM-dd", out _))
        {
            error = $"{Capitalize(FieldLabel(name))} không đúng định dạng ngày.";
            return false;
        }

        return true;
    }

    private static bool TryOptionalDate(JsonObject node, string name, out string? error)
    {
        error = null;
        var value = node[name];
        if (value is null)
        {
            return true;
        }

        if (value is not JsonValue scalar
            || !scalar.TryGetValue<string>(out var text))
        {
            error = $"{Capitalize(FieldLabel(name))} không đúng định dạng ngày hoặc để trống.";
            return false;
        }

        text = text.Trim();
        if (text.Length == 0)
        {
            node[name] = null;
            return true;
        }

        if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", out _))
        {
            error = $"{Capitalize(FieldLabel(name))} không đúng định dạng ngày hoặc để trống.";
            return false;
        }

        node[name] = text;
        return true;
    }

    private static bool TryOptionalEnum(JsonObject node, string name, string[] allowed, out string? error)
    {
        error = null;
        if (node[name] is null)
        {
            return true;
        }

        return TryEnum(node, name, allowed, out error);
    }

    private static bool TryEnum(JsonObject node, string name, string[] allowed, out string? error)
    {
        error = null;
        if (node[name] is not JsonValue value || !value.TryGetValue<string>(out var text))
        {
            error = $"Thiếu {FieldLabel(name)}.";
            return false;
        }

        if (!allowed.Contains(text, StringComparer.Ordinal))
        {
            error = $"{Capitalize(FieldLabel(name))} không hợp lệ.";
            return false;
        }

        return true;
    }

    private static bool IsSafeLink(JsonNode? node, out string? error)
    {
        error = null;
        if (node is null)
        {
            return true;
        }

        if (node is not JsonValue value || !value.TryGetValue<string>(out var link))
        {
            error = "Liên kết phải là chuỗi hoặc null.";
            return false;
        }

        if (!link.StartsWith('/') || link.StartsWith("//", StringComparison.Ordinal))
        {
            error = "Liên kết chỉ được là đường dẫn nội bộ bắt đầu bằng '/'.";
            return false;
        }

        return true;
    }
}
