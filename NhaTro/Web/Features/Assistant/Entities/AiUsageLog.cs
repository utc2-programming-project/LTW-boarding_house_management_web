using Web.Features.Assistant.Enums;

namespace Web.Features.Assistant.Entities;

/// <summary>Một lần gọi model (kể cả thất bại) để tính hạn mức và chi phí.</summary>
public sealed class AiUsageLog
{
    public long Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public AiUseCase UseCase { get; set; }

    public string Model { get; set; } = string.Empty;

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public decimal Cost { get; set; }

    public bool Succeeded { get; set; }

    public string? ErrorCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
