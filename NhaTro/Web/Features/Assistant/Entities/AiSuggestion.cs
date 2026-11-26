using Web.Features.Assistant.Enums;

namespace Web.Features.Assistant.Entities;

/// <summary>
/// Đề xuất do AI tạo. Bản thân nó không thay đổi dữ liệu nghiệp vụ; chỉ khi Chủ trọ
/// duyệt thì applier của use case mới gọi service nghiệp vụ tương ứng.
/// </summary>
public sealed class AiSuggestion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public AiUseCase UseCase { get; set; }

    /// <summary>Tòa nhà liên quan khi đề xuất gắn phạm vi; null với nháp tạo nhà mới.</summary>
    public int? PropertyId { get; set; }

    public string RequestedByUserId { get; set; } = string.Empty;

    /// <summary>Phạm vi đã dùng để đọc dữ liệu (tòa, kỳ lọc, bản ghi liên quan).</summary>
    public string ScopeJson { get; set; } = "{}";

    /// <summary>Id bản ghi đã đưa vào ngữ cảnh (hóa đơn, hợp đồng, sự cố…).</summary>
    public string InputRefsJson { get; set; } = "{}";

    /// <summary>Kết quả có cấu trúc theo JSON Schema của use case.</summary>
    public string OutputJson { get; set; } = "{}";

    /// <summary>Tên model đã tạo đề xuất (hoặc "demo"/"cache").</summary>
    public string Model { get; set; } = string.Empty;

    public AiSuggestionStatus Status { get; set; } = AiSuggestionStatus.Pending;

    public string? DecidedByUserId { get; set; }

    public DateTimeOffset? DecidedAt { get; set; }

    public string? DecisionReason { get; set; }

    /// <summary>Id bản ghi được tạo sau khi áp dụng (ví dụ PropertyId của nhà vừa tạo).</summary>
    public string? AppliedRefId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
