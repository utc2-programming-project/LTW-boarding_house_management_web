using Web.Features.Assistant.Entities;
using Web.Features.Assistant.Enums;
using Web.Features.Properties;

namespace Web.Features.Assistant.Services;

public sealed record AiApplyResult(
    bool Succeeded,
    string? AppliedRefId,
    string? ErrorCode,
    string? ErrorMessage)
{
    public static AiApplyResult Success(string? appliedRefId = null) =>
        new(true, appliedRefId, null, null);

    public static AiApplyResult Failure(string errorCode, string errorMessage) =>
        new(false, null, errorCode, errorMessage);
}

/// <summary>
/// Thực thi đề xuất khi Chủ trọ duyệt. Chỉ use case có tác động nghiệp vụ (nhắc thanh toán,
/// nháp nhà/phòng) mới cần applier; các use case chỉ đọc dùng applier thông tin.
/// Applier gọi lại service nghiệp vụ đã có (giữ nguyên kiểm tra quyền và kiểm tra dữ liệu).
/// </summary>
public interface IAiSuggestionApplier
{
    AiUseCase UseCase { get; }

    Task<AiApplyResult> ApplyAsync(
        AiSuggestion suggestion,
        string outputJson,
        PropertyCaller approver,
        CancellationToken ct = default);
}

/// <summary>Use case chỉ đọc (U1/U4/U5): duyệt chỉ đánh dấu đã áp dụng, không ghi nghiệp vụ.</summary>
public sealed class InformationalAiSuggestionApplier(AiUseCase useCase) : IAiSuggestionApplier
{
    public AiUseCase UseCase { get; } = useCase;

    public Task<AiApplyResult> ApplyAsync(
        AiSuggestion suggestion,
        string outputJson,
        PropertyCaller approver,
        CancellationToken ct = default) =>
        Task.FromResult(AiApplyResult.Success());
}
