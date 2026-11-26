using Web.Features.Assistant.Enums;

namespace Web.Features.Assistant.DTOs;

/// <summary>Yêu cầu tạo đề xuất từ trang Trợ lý AI hoặc nút theo ngữ cảnh.</summary>
public sealed class AiGenerateRequest
{
    public AiUseCase UseCase { get; set; } = AiUseCase.DailySummary;

    public int? PropertyId { get; set; }

    public Guid? ContractId { get; set; }

    public int? InvoiceId { get; set; }

    public int? IncidentId { get; set; }

    public int? RoomId { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }

    /// <summary>Câu mô tả tự nhiên (bắt buộc với U3, tùy chọn với các use case khác).</summary>
    public string? Instruction { get; set; }
}

// ── Đầu ra có cấu trúc của từng use case (lưu nguyên JSON trong AiSuggestion.OutputJson) ──

public sealed record AiInsightDto(string Title, string Detail, string Severity);

public sealed record AiActionSuggestionDto(string Title, string Detail, string? Link);

public sealed record AiMetricDto(string Label, string Value);

public sealed record AiReportSummaryOutput(
    string Headline,
    string Summary,
    IReadOnlyList<AiInsightDto> Insights,
    IReadOnlyList<AiActionSuggestionDto> Actions,
    IReadOnlyList<AiMetricDto> Metrics);

public sealed record AiNotificationOutput(string Title, string Body, string? Notes);

public sealed record AiPropertyDraft(string Name, string Address);

public sealed record AiRoomDraft(string Code, int? Floor, decimal? AreaM2, decimal StandardPrice);

public sealed record AiRoomDraftOutput(
    AiPropertyDraft? Property,
    IReadOnlyList<AiRoomDraft> Rooms,
    string? Notes);

public sealed record AiIncidentSuggestionOutput(
    string Category,
    string Urgency,
    string? Assignee,
    string Reason,
    string? SuggestedTitle,
    string? SuggestedDescription);

public sealed record AiWorkItemDto(string Label, int Count, string? Link, string Severity, string? Detail);

public sealed record AiWorkSummaryOutput(
    string Headline,
    string Summary,
    IReadOnlyList<AiWorkItemDto> Items);

/// <summary>
/// U6 — nháp hồ sơ khách thuê. Không chứa CCCD/số tài khoản: AI không được đề xuất
/// định danh nhạy cảm, người duyệt tự bổ sung ở hồ sơ nếu cần.
/// </summary>
public sealed record AiTenantDraftOutput(
    string FullName,
    string PhoneNumber,
    string? Email,
    string? DateOfBirth,
    string? Gender,
    string? PermanentAddress,
    string? Note);

/// <summary>U7 — nháp danh mục dịch vụ và giá ban đầu (một dòng danh mục + một phiên bản giá).</summary>
public sealed record AiServiceDraftOutput(
    string Name,
    string Unit,
    string CalculationMethod,
    decimal UnitPrice,
    string EffectiveDate,
    string? EffectiveTo,
    string? Note,
    string? Description);

/// <summary>
/// U8 — nháp hợp đồng thuê. Chỉ được chọn phòng/khách có trong ngữ cảnh;
/// hệ thống tạo hợp đồng ở trạng thái nháp để đi tiếp luồng gửi/duyệt hiện có.
/// </summary>
public sealed record AiContractDraftOutput(
    int? RoomId,
    string? RoomCode,
    IReadOnlyList<int> TenantIds,
    string StartDate,
    string EndDate,
    decimal? DepositAmount,
    decimal? MonthlyRent,
    int PaymentCycleMonths,
    int? MeterReadingDay,
    string? Terms,
    IReadOnlyList<string>? TenantNames = null);

/// <summary>Bản đã bóc theo use case để giao diện hiển thị; JSON gốc vẫn giữ để sửa/duyệt.</summary>
public sealed record AiDraftView(
    AiReportSummaryOutput? ReportSummary = null,
    AiNotificationOutput? Notification = null,
    AiRoomDraftOutput? RoomDraft = null,
    AiIncidentSuggestionOutput? Incident = null,
    AiWorkSummaryOutput? WorkSummary = null,
    AiTenantDraftOutput? TenantDraft = null,
    AiServiceDraftOutput? ServiceDraft = null,
    AiContractDraftOutput? ContractDraft = null);

public sealed record AiSuggestionDto(
    Guid Id,
    AiUseCase UseCase,
    int? PropertyId,
    string RequestedByUserId,
    string RequesterName,
    DateTimeOffset CreatedAt,
    AiSuggestionStatus Status,
    string Model,
    string OutputJson,
    AiDraftView Draft,
    string? DecisionReason,
    DateTimeOffset? DecidedAt,
    string? AppliedRefId)
{
    public bool CanEditOutput => UseCase is AiUseCase.ReminderMessage or AiUseCase.RoomDraft;
}

public sealed class AiSuggestionResult
{
    public required bool Succeeded { get; init; }

    public AiSuggestionDto? Suggestion { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static AiSuggestionResult Success(AiSuggestionDto suggestion) =>
        new() { Succeeded = true, Suggestion = suggestion };

    public static AiSuggestionResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

public sealed class AiSuggestionListResult
{
    public required bool Succeeded { get; init; }

    public IReadOnlyList<AiSuggestionDto> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static AiSuggestionListResult Success(IReadOnlyList<AiSuggestionDto> items) =>
        new() { Succeeded = true, Items = items };

    public static AiSuggestionListResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

public sealed record AiPropertyOption(int Id, string Name);

public sealed record AiInvoiceOption(int Id, string Label, decimal OutstandingAmount, DateOnly? DueDate);

public sealed record AiIncidentOption(int Id, string Label);

/// <summary>Dữ liệu trang Trợ lý AI cần để vẽ form.</summary>
public sealed class AiAssistantSnapshot
{
    public required IReadOnlyList<AiPropertyOption> Properties { get; init; }

    public required IReadOnlyList<AiInvoiceOption> Invoices { get; init; }

    public required IReadOnlyList<AiIncidentOption> Incidents { get; init; }

    public required AiSettingsDto Settings { get; init; }
}

public sealed record AiSettingsDto(
    bool Enabled,
    int DailyRequestLimit,
    int UsedToday,
    int DefaultDailyLimit,
    string Provider,
    string Model,
    bool IsDemo);

public sealed class AiSettingsResult
{
    public required bool Succeeded { get; init; }

    public AiSettingsDto? Settings { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static AiSettingsResult Success(AiSettingsDto settings) =>
        new() { Succeeded = true, Settings = settings };

    public static AiSettingsResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

public sealed class AiSnapshotResult
{
    public required bool Succeeded { get; init; }

    public AiAssistantSnapshot? Snapshot { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static AiSnapshotResult Success(AiAssistantSnapshot snapshot) =>
        new() { Succeeded = true, Snapshot = snapshot };

    public static AiSnapshotResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
