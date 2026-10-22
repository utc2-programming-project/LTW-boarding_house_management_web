namespace Web.Features.Tenants.DTOs;

/// <summary>Tài khoản Khách thuê chưa gắn với hồ sơ nào, chỉ dùng trong màn hình liên kết P3.</summary>
public sealed class TenantAccountOptionDto
{
    public required string UserId { get; init; }

    public required string DisplayName { get; init; }

    public string? Email { get; init; }
}
