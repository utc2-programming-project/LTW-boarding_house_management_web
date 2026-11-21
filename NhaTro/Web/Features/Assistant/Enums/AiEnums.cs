namespace Web.Features.Assistant.Enums;

/// <summary>
/// Use case của Trợ lý AI v1 (spec "Trợ lý AI" mục 2). Giá trị số cố định vì được lưu DB.
/// </summary>
public enum AiUseCase
{
    ReportSummary = 1,
    ReminderMessage = 2,
    RoomDraft = 3,
    IncidentTriage = 4,
    DailySummary = 5,
    TenantDraft = 6,
    ServiceDraft = 7,
    ContractDraft = 8,
}

/// <summary>Vòng đời một đề xuất AI; chỉ Chủ trọ chốt được Pending.</summary>
public enum AiSuggestionStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Applied = 3,
}
