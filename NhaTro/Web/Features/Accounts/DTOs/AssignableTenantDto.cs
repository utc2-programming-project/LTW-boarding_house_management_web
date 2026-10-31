namespace Web.Features.Accounts.DTOs;

/// <summary>
/// VHT-95: ho so khach chua co tai khoan, nam trong pham vi nguoi goi.
/// Dung cho o chon ho so khi tao tai khoan Khach thue.
/// </summary>
public sealed class AssignableTenantDto
{
    public required int TenantId { get; init; }

    public required string FullName { get; init; }

    public string? PhoneNumber { get; init; }

    /// <summary>Cac toa nha co hop dong cua ho so (chi de nhan biet, khong lo du lieu khac).</summary>
    public string? PropertyNames { get; init; }
}
