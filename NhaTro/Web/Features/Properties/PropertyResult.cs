using Web.Features.Properties.DTOs;

namespace Web.Features.Properties;

/// <summary>
/// Kết quả thao tác nhà trọ. Cùng khuôn Succeeded/ErrorCode/ErrorMessage như P3 (TenantResult)
/// để UI xử lý lỗi thống nhất.
/// </summary>
public sealed class PropertyResult
{
    public bool Succeeded { get; init; }
    public PropertyDetail? Property { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static PropertyResult Success(PropertyDetail property) =>
        new() { Succeeded = true, Property = property };

    public static PropertyResult Success() => new() { Succeeded = true };

    public static PropertyResult Failure(string code, string message) =>
        new() { Succeeded = false, ErrorCode = code, ErrorMessage = message };
}
