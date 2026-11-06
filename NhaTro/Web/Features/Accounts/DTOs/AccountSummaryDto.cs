namespace Web.Features.Accounts.DTOs;

/// <summary>Số liệu nhanh cho Admin trên trang chủ; kèm vài tài khoản chờ duyệt mới nhất.</summary>
public sealed record AccountSummaryDto(
    int Active,
    int Pending,
    int Locked,
    IReadOnlyList<AccountListItemDto> PendingAccounts)
{
    public int Total => Active + Pending + Locked;

    public static AccountSummaryDto Empty { get; } = new(0, 0, 0, []);
}
