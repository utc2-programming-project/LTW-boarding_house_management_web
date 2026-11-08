namespace Web.Features.Properties.DTOs;

/// <summary>Một dòng phân công Quản lý — tòa nhà (VHT-24).</summary>
public sealed record PropertyManagerItem(
    int PropertyId,
    string ManagerUserId,
    string? ManagerEmail,
    DateTime AssignedAt,
    string AssignedByUserId,
    DateTime? RevokedAt,
    string? RevokedByUserId,
    string? PropertyName = null,
    string? PropertyAddress = null)
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

/// <summary>Một tài khoản Quản lý có thể gán vào tòa nhà.</summary>
public sealed record PropertyManagerCandidate(
    string UserId,
    string? Email,
    string? FullName);

/// <summary>Kết quả đọc danh sách Quản lý có thể gán.</summary>
public sealed class PropertyManagerCandidateListResult
{
    private PropertyManagerCandidateListResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<PropertyManagerCandidate> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static PropertyManagerCandidateListResult Success(IReadOnlyList<PropertyManagerCandidate> items) =>
        new() { Succeeded = true, Items = items };

    public static PropertyManagerCandidateListResult Failure(string errorCode, string errorMessage) =>
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

/// <summary>Một tòa nhà của Chủ trọ có thể phân công cho Quản lý.</summary>
public sealed record PropertyManagerPropertyOption(
    int Id,
    string Name,
    string? Address);

/// <summary>Kết quả đọc danh sách tòa nhà có thể phân công.</summary>
public sealed class PropertyManagerPropertyListResult
{
    private PropertyManagerPropertyListResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<PropertyManagerPropertyOption> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static PropertyManagerPropertyListResult Success(IReadOnlyList<PropertyManagerPropertyOption> items) =>
        new() { Succeeded = true, Items = items };

    public static PropertyManagerPropertyListResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Chủ trọ mà một Quản lý đang trực thuộc (null = chưa được phân công).</summary>
public sealed record ManagerOwnerInfo(
    string OwnerUserId,
    string? OwnerName,
    string? OwnerEmail,
    int PropertyCount);

/// <summary>Kết quả đọc Chủ trọ của Quản lý đang đăng nhập.</summary>
public sealed class ManagerOwnerResult
{
    private ManagerOwnerResult()
    {
    }

    public required bool Succeeded { get; init; }

    public ManagerOwnerInfo? Owner { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static ManagerOwnerResult Success(ManagerOwnerInfo? owner) =>
        new() { Succeeded = true, Owner = owner };

    public static ManagerOwnerResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}

/// <summary>Tài khoản Quản lý đủ điều kiện nhận phân công (đã duyệt, chưa bị khóa).</summary>
public sealed record ManagerCandidateDto(
    string UserId,
    string Email,
    string? FullName,
    bool IsAssigned)
{
    public string Display => string.IsNullOrWhiteSpace(FullName) ? Email : $"{FullName} ({Email})";
}

/// <summary>Kết quả đọc danh sách tài khoản Quản lý có thể phân công cho một tòa nhà.</summary>
public sealed class PropertyManagerCandidateResult
{
    private PropertyManagerCandidateResult()
    {
    }

    public required bool Succeeded { get; init; }

    public IReadOnlyList<ManagerCandidateDto> Items { get; init; } = [];

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public static PropertyManagerCandidateResult Success(IReadOnlyList<ManagerCandidateDto> items) =>
        new() { Succeeded = true, Items = items };

    public static PropertyManagerCandidateResult Failure(string errorCode, string errorMessage) =>
        new() { Succeeded = false, ErrorCode = errorCode, ErrorMessage = errorMessage };
}
