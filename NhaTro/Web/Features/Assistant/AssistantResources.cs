using Web.Features.Assistant.Enums;

namespace Web.Features.Assistant;

/// <summary>Marker class cho chuỗi giao diện Trợ lý AI (Resources/Features/Assistant).</summary>
public sealed class AssistantResources
{
}

public static class AiErrorCodes
{
    public const string Forbidden = nameof(Forbidden);
    public const string AiDisabled = nameof(AiDisabled);
    public const string QuotaExceeded = nameof(QuotaExceeded);
    public const string ProviderFailed = nameof(ProviderFailed);
    public const string InvalidOutput = nameof(InvalidOutput);
    public const string InvalidRequest = nameof(InvalidRequest);
    public const string NotFound = nameof(NotFound);
    public const string AlreadyDecided = nameof(AlreadyDecided);
    public const string Conflict = nameof(Conflict);
    public const string ApplyFailed = nameof(ApplyFailed);
    public const string ReasonRequired = nameof(ReasonRequired);
    public const string ReasonTooLong = nameof(ReasonTooLong);
    public const string UnknownUseCase = nameof(UnknownUseCase);
}

public static class AiUseCaseCatalog
{
    public static readonly AiUseCase[] All =
    [
        AiUseCase.ReportSummary,
        AiUseCase.ReminderMessage,
        AiUseCase.RoomDraft,
        AiUseCase.IncidentTriage,
        AiUseCase.DailySummary,
        AiUseCase.TenantDraft,
        AiUseCase.ServiceDraft,
        AiUseCase.ContractDraft,
    ];

    public static bool IsKnown(AiUseCase useCase) => All.Contains(useCase);

    /// <summary>Resource key tên use case, dùng cả trang trợ lý và hàng đợi duyệt.</summary>
    public static string NameKey(AiUseCase useCase) => useCase switch
    {
        AiUseCase.ReportSummary => "UseCaseReportSummary",
        AiUseCase.ReminderMessage => "UseCaseReminderMessage",
        AiUseCase.RoomDraft => "UseCaseRoomDraft",
        AiUseCase.IncidentTriage => "UseCaseIncidentTriage",
        AiUseCase.DailySummary => "UseCaseDailySummary",
        AiUseCase.TenantDraft => "UseCaseTenantDraft",
        AiUseCase.ServiceDraft => "UseCaseServiceDraft",
        AiUseCase.ContractDraft => "UseCaseContractDraft",
        _ => "UseCaseUnknown",
    };

    public static string DescriptionKey(AiUseCase useCase) => useCase switch
    {
        AiUseCase.ReportSummary => "UseCaseReportSummaryDesc",
        AiUseCase.ReminderMessage => "UseCaseReminderMessageDesc",
        AiUseCase.RoomDraft => "UseCaseRoomDraftDesc",
        AiUseCase.IncidentTriage => "UseCaseIncidentTriageDesc",
        AiUseCase.DailySummary => "UseCaseDailySummaryDesc",
        AiUseCase.TenantDraft => "UseCaseTenantDraftDesc",
        AiUseCase.ServiceDraft => "UseCaseServiceDraftDesc",
        AiUseCase.ContractDraft => "UseCaseContractDraftDesc",
        _ => "UseCaseUnknown",
    };

    public static string Icon(AiUseCase useCase) => useCase switch
    {
        AiUseCase.ReportSummary => "chart",
        AiUseCase.ReminderMessage => "bell",
        AiUseCase.RoomDraft => "door",
        AiUseCase.IncidentTriage => "wrench",
        AiUseCase.DailySummary => "check",
        AiUseCase.TenantDraft => "user",
        AiUseCase.ServiceDraft => "receipt",
        AiUseCase.ContractDraft => "file-text",
        _ => "sparkles",
    };

    /// <summary>Use case nào khi duyệt thì thực thi nghiệp vụ (có applier thật).</summary>
    public static bool RequiresApproval(AiUseCase useCase) =>
        useCase is AiUseCase.ReminderMessage or AiUseCase.RoomDraft
            or AiUseCase.TenantDraft or AiUseCase.ServiceDraft or AiUseCase.ContractDraft;
}
