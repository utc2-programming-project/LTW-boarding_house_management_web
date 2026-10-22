namespace Web.Features.Accounts.DTOs;

/// <summary>
/// VHT-8: ho so ca nhan cua nguoi dang dang nhap (/profile).
/// Khong bao gio chua vai tro cua nguoi khac hay truong nhay cam.
/// </summary>
public sealed class MyProfileDto
{
    public required string Id { get; init; }

    public required string UserName { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    public string? FullName { get; init; }

    public required string Role { get; init; }

    public bool HasPassword { get; init; }
}
