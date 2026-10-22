namespace Web.Features.Settings.DTOs;

/// <summary>Kết quả đọc danh sách công tắc. Cùng khuôn Succeeded / ErrorCode / ErrorMessage.</summary>
public sealed class ManagerToggleListResult
{
    private ManagerToggleListResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<ManagerToggleItem> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static ManagerToggleListResult Success(IReadOnlyList<ManagerToggleItem> items) =>
        new() { Succeeded = true, Items = items };

    public static ManagerToggleListResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Kết quả đổi một công tắc.</summary>
public sealed class ManagerToggleResult
{
    private ManagerToggleResult()
    {
    }

    public required bool Succeeded { get; init; }

    /// <summary>null khi thất bại.</summary>
    public ManagerToggleItem? Toggle { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static ManagerToggleResult Success(ManagerToggleItem toggle) =>
        new() { Succeeded = true, Toggle = toggle };

    public static ManagerToggleResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
