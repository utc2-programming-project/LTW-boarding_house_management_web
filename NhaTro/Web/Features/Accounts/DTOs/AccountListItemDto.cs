namespace Web.Features.Accounts.DTOs;

/// <summary>
/// VHT-8: mot dong trong danh sach tai khoan (/admin/users).
/// Khong tra PasswordHash, SecurityStamp hay bat ky truong nhay cam nao.
/// </summary>
public sealed class AccountListItemDto
{
    public required string Id { get; init; }

    /// <summary>Ten dang nhap (hien tai = email).</summary>
    public required string UserName { get; init; }

    public string? Email { get; init; }

    public string? PhoneNumber { get; init; }

    /// <summary>Ho ten hien thi; null voi tai khoan cu chua cap nhat.</summary>
    public string? FullName { get; init; }

    /// <summary>Vai tro duy nhat (he thong chi cho 1 vai tro / tai khoan).</summary>
    public required string Role { get; init; }

    /// <summary>Tai khoan dang bi khoa vinh vien boi quan tri.</summary>
    public required bool IsLocked { get; init; }

    public DateTimeOffset? LockoutEnd { get; init; }

    /// <summary>VHT-95: Cho duyet / Hoat dong / Khoa (Khoa uu tien cao nhat).</summary>
    public required AccountStatus Status { get; init; }

    /// <summary>Null = Cho duyet.</summary>
    public DateTime? ApprovedAt { get; init; }
}
