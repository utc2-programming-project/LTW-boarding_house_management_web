namespace Web.Features.Accounts.DTOs;

/// <summary>PLAN-008: chủ trọ tự đăng ký từ trang công khai.</summary>
public sealed class RegisterOwnerRequest
{
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string Password { get; set; } = string.Empty;
}
