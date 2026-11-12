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

    // PLAN-028: tai khoan nhan tien cua chu tro (dung cho QR VietQR).
    public string? BankCode { get; init; }

    /// <summary>Ten ngan hang suy ra tu <see cref="BankCode"/>; rong khi chua cau hinh.</summary>
    public string? BankName { get; init; }

    public string? BankAccountNumber { get; init; }

    public string? BankAccountName { get; init; }

    /// <summary>Du 3 truong de sinh QR chuyen khoan.</summary>
    public bool HasBankAccount =>
        !string.IsNullOrWhiteSpace(BankCode)
        && !string.IsNullOrWhiteSpace(BankAccountNumber)
        && !string.IsNullOrWhiteSpace(BankAccountName);
}
