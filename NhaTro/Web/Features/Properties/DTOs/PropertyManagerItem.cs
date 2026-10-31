namespace Web.Features.Properties.DTOs;

/// <summary>Một dòng phân công Quản lý — tòa nhà (VHT-24).</summary>
public sealed record PropertyManagerItem(
    int PropertyId,
    string ManagerUserId,
    string? ManagerEmail,
    DateTime AssignedAt,
    string AssignedByUserId,
    DateTime? RevokedAt,
    string? RevokedByUserId)
{
    public bool IsActive => RevokedAt is null;
}

/// <summary>Kết quả đọc danh sách phân công. Cùng khuôn Succeeded/ErrorCode/ErrorMessage.</summary>
public sealed class PropertyManagerListResult
{
    private PropertyManagerListResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<PropertyManagerItem> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static PropertyManagerListResult Success(IReadOnlyList<PropertyManagerItem> items) =>
        new() { Succeeded = true, Items = items };

    public static PropertyManagerListResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Kết quả gán/bỏ gán một phân công.</summary>
public sealed class PropertyManagerResult
{
    private PropertyManagerResult()
    {
    }

    public required bool Succeeded { get; init; }

    public PropertyManagerItem? Assignment { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static PropertyManagerResult Success(PropertyManagerItem assignment) =>
        new() { Succeeded = true, Assignment = assignment };

    public static PropertyManagerResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
