using Web.Features.Assistant.Enums;

namespace Web.Features.Assistant.Rules;

/// <summary>
/// JSON Schema dùng cho structured outputs (gửi kèm response_format) và tên schema.
/// Schema là nguồn chuẩn: model trả sai cấu trúc thì đề xuất bị từ chối (fail closed).
/// </summary>
public static class AiOutputSchemas
{
    public static string Name(AiUseCase useCase) => useCase switch
    {
        AiUseCase.ReportSummary => "report_summary",
        AiUseCase.ReminderMessage => "reminder_message",
        AiUseCase.RoomDraft => "room_draft",
        AiUseCase.IncidentTriage => "incident_triage",
        AiUseCase.DailySummary => "daily_summary",
        AiUseCase.TenantDraft => "tenant_draft",
        AiUseCase.ServiceDraft => "service_draft",
        AiUseCase.ContractDraft => "contract_draft",
        _ => "unknown",
    };

    public static string Json(AiUseCase useCase) => useCase switch
    {
        AiUseCase.ReportSummary => ReportSummary,
        AiUseCase.ReminderMessage => ReminderMessage,
        AiUseCase.RoomDraft => RoomDraft,
        AiUseCase.IncidentTriage => IncidentTriage,
        AiUseCase.DailySummary => DailySummary,
        AiUseCase.TenantDraft => TenantDraft,
        AiUseCase.ServiceDraft => ServiceDraft,
        AiUseCase.ContractDraft => ContractDraft,
        _ => "{}",
    };

    public static string SystemPrompt(AiUseCase useCase)
    {
        var task = useCase switch
        {
            AiUseCase.ReportSummary =>
                "Tóm tắt tình hình vận hành nhà trọ theo số liệu được cung cấp và đề xuất hành động ưu tiên.",
            AiUseCase.ReminderMessage =>
                "Soạn tin nhắc thanh toán gửi khách thuê dựa trên danh sách hóa đơn/hợp đồng được cung cấp.",
            AiUseCase.RoomDraft =>
                "Chuyển câu mô tả tự nhiên của chủ trọ thành bản nháp nhà trọ và danh sách phòng.",
            AiUseCase.IncidentTriage =>
                "Phân loại sự cố bảo trì và gợi ý mức ưu tiên dựa trên nội dung và lịch sử được cung cấp.",
            AiUseCase.DailySummary =>
                "Tóm tắt việc cần làm trong ngày từ danh sách việc đến hạn được cung cấp.",
            AiUseCase.TenantDraft =>
                "Chuyển câu mô tả thành bản nháp hồ sơ khách thuê: họ tên, số điện thoại, email, ngày sinh, giới tính, địa chỉ thường trú. Không điền số CCCD hay tài khoản ngân hàng.",
            AiUseCase.ServiceDraft =>
                "Chuyển câu mô tả thành bản nháp danh mục dịch vụ kèm giá ban đầu: tên, đơn vị, cách tính, đơn giá, ngày hiệu lực.",
            AiUseCase.ContractDraft =>
                "Lập bản nháp hợp đồng thuê từ danh sách phòng và khách được cung cấp: chỉ chọn roomId/tenantIds có thật trong ngữ cảnh, kèm ngày bắt đầu, ngày kết thúc, giá thuê, tiền cọc và kỳ thanh toán.",
            _ => "Hỗ trợ vận hành nhà trọ.",
        };

        return $$"""
            Bạn là trợ lý vận hành cho phần mềm quản lý nhà trọ tại Việt Nam.
            Nhiệm vụ: {{task}}

            Quy tắc bắt buộc:
            - Chỉ dùng dữ liệu JSON được cung cấp; không bịa số liệu, tên hay id.
            - Không nhắc lại số CCCD, số tài khoản ngân hàng hay thông tin định danh đầy đủ.
            - Trả về DUY NHẤT một đối tượng JSON đúng schema, không thêm chữ giải thích ngoài JSON.
            - Viết tiếng Việt tự nhiên, ngắn gọn, xưng hô trung tính (không phân biệt giới tính).
            - Không tuyên bố đã gửi thông báo hay đã thay đổi dữ liệu; chỉ soạn bản nháp.
            - Ngày luôn theo dạng yyyy-MM-dd; id (phòng, khách) chỉ được lấy từ ngữ cảnh JSON.
            """;
    }

    private const string ReportSummary = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "headline": { "type": "string" },
            "summary": { "type": "string" },
            "insights": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "title": { "type": "string" },
                  "detail": { "type": "string" },
                  "severity": { "type": "string", "enum": ["info", "warning", "critical"] }
                },
                "required": ["title", "detail", "severity"]
              }
            },
            "actions": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "title": { "type": "string" },
                  "detail": { "type": "string" },
                  "link": { "type": ["string", "null"] }
                },
                "required": ["title", "detail", "link"]
              }
            },
            "metrics": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "label": { "type": "string" },
                  "value": { "type": "string" }
                },
                "required": ["label", "value"]
              }
            }
          },
          "required": ["headline", "summary", "insights", "actions", "metrics"]
        }
        """;

    private const string ReminderMessage = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "title": { "type": "string" },
            "body": { "type": "string" },
            "notes": { "type": ["string", "null"] }
          },
          "required": ["title", "body", "notes"]
        }
        """;

    private const string RoomDraft = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "property": {
              "type": ["object", "null"],
              "additionalProperties": false,
              "properties": {
                "name": { "type": "string" },
                "address": { "type": "string" }
              },
              "required": ["name", "address"]
            },
            "rooms": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "code": { "type": "string" },
                  "floor": { "type": ["integer", "null"] },
                  "areaM2": { "type": ["number", "null"] },
                  "standardPrice": { "type": "number" }
                },
                "required": ["code", "floor", "areaM2", "standardPrice"]
              }
            },
            "notes": { "type": ["string", "null"] }
          },
          "required": ["property", "rooms", "notes"]
        }
        """;

    private const string IncidentTriage = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "category": { "type": "string", "enum": ["Electrical", "Water", "Furniture", "Equipment", "Other"] },
            "urgency": { "type": "string", "enum": ["Normal", "Urgent", "Emergency"] },
            "assignee": { "type": ["string", "null"] },
            "reason": { "type": "string" },
            "suggestedTitle": { "type": ["string", "null"] },
            "suggestedDescription": { "type": ["string", "null"] }
          },
          "required": ["category", "urgency", "assignee", "reason", "suggestedTitle", "suggestedDescription"]
        }
        """;

    private const string DailySummary = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "headline": { "type": "string" },
            "summary": { "type": "string" },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "label": { "type": "string" },
                  "count": { "type": "integer" },
                  "link": { "type": ["string", "null"] },
                  "severity": { "type": "string", "enum": ["info", "warning", "critical"] },
                  "detail": { "type": ["string", "null"] }
                },
                "required": ["label", "count", "link", "severity", "detail"]
              }
            }
          },
          "required": ["headline", "summary", "items"]
        }
        """;

    private const string TenantDraft = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "fullName": { "type": "string" },
            "phoneNumber": { "type": "string" },
            "email": { "type": ["string", "null"] },
            "dateOfBirth": { "type": ["string", "null"] },
            "gender": { "type": ["string", "null"], "enum": ["Male", "Female", "Other", null] },
            "permanentAddress": { "type": ["string", "null"] },
            "note": { "type": ["string", "null"] }
          },
          "required": ["fullName", "phoneNumber", "email", "dateOfBirth", "gender", "permanentAddress", "note"]
        }
        """;

    private const string ServiceDraft = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "name": { "type": "string" },
            "unit": { "type": "string" },
            "calculationMethod": { "type": "string", "enum": ["fixed-monthly", "meter", "tiered", "custom"] },
            "unitPrice": { "type": "number" },
            "effectiveDate": { "type": "string" },
            "effectiveTo": { "type": ["string", "null"] },
            "note": { "type": ["string", "null"] },
            "description": { "type": ["string", "null"] }
          },
          "required": ["name", "unit", "calculationMethod", "unitPrice", "effectiveDate", "effectiveTo", "note", "description"]
        }
        """;

    private const string ContractDraft = """
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "roomId": { "type": ["integer", "null"] },
            "roomCode": { "type": ["string", "null"] },
            "tenantIds": {
              "type": "array",
              "minItems": 1,
              "items": { "type": "integer" }
            },
            "startDate": { "type": "string" },
            "endDate": { "type": "string" },
            "depositAmount": { "type": ["number", "null"] },
            "monthlyRent": { "type": ["number", "null"] },
            "paymentCycleMonths": { "type": "integer", "enum": [1, 2, 3, 6, 12] },
            "meterReadingDay": { "type": ["integer", "null"] },
            "terms": { "type": ["string", "null"] }
          },
          "required": ["roomId", "roomCode", "tenantIds", "startDate", "endDate", "depositAmount", "monthlyRent", "paymentCycleMonths", "meterReadingDay", "terms"]
        }
        """;
}
