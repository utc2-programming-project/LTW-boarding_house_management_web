using Web.Features.Assistant.Enums;

namespace Web.Features.Assistant.Services;

public sealed record AiChatRequest(
    AiUseCase UseCase,
    string SystemPrompt,
    string UserPrompt,
    string ContextJson,
    string? Instruction);

public sealed record AiChatResult(
    bool Succeeded,
    string? Json,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    decimal Cost,
    string? ErrorCode,
    string? ErrorMessage,
    bool FromCache = false)
{
    public static AiChatResult Success(
        string json, string model, int promptTokens = 0, int completionTokens = 0, decimal cost = 0m,
        bool fromCache = false) =>
        new(true, json, model, promptTokens, completionTokens, cost, null, null, fromCache);

    public static AiChatResult Failure(string errorCode, string errorMessage, string model) =>
        new(false, null, model, 0, 0, 0m, errorCode, errorMessage);
}

/// <summary>
/// Cổng duy nhất tới model. Không có tool ghi, không truy cập DB: chỉ nhận prompt + ngữ cảnh
/// đã che PII và trả JSON. Lỗi mạng/timeout trả thất bại để luồng nghiệp vụ fail closed.
/// </summary>
public interface IAiChatClient
{
    Task<AiChatResult> CompleteAsync(AiChatRequest request, CancellationToken ct = default);
}
