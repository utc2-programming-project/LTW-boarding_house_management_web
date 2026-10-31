namespace Web.Features.Accounts;

/// <summary>VHT-95: ba trang thai hien thi cua tai khoan.</summary>
public enum AccountStatus
{
    /// <summary>Da tao nhung chua duoc duyet (ApprovedAt == null).</summary>
    Pending,

    /// <summary>Da duyet va khong bi khoa.</summary>
    Active,

    /// <summary>Bi quan tri khoa (LockoutEnd = MaxValue).</summary>
    Locked,
}

/// <summary>
/// VHT-95: suy trang thai tai khoan bang ham thuan (khong DB) de unit test duoc.
/// Uu tien: Khoa &gt; Cho duyet &gt; Hoat dong.
/// </summary>
public static class AccountStatusRules
{
    /// <summary>LockoutEnd = MaxValue la quy uoc "bi quan tri khoa", khong tu mo.</summary>
    public static readonly DateTimeOffset PermanentLockout = DateTimeOffset.MaxValue;

    public static AccountStatus Derive(DateTime? approvedAt, DateTimeOffset? lockoutEnd)
    {
        if (IsPermanentlyLocked(lockoutEnd))
        {
            return AccountStatus.Locked;
        }

        return approvedAt is null ? AccountStatus.Pending : AccountStatus.Active;
    }

    /// <summary>Khoa do quan tri dat, khac khoa tam thoi do sai mat khau (15 phut).</summary>
    public static bool IsPermanentlyLocked(DateTimeOffset? lockoutEnd) =>
        lockoutEnd is { } end && end >= PermanentLockout - TimeSpan.FromMinutes(1);

    /// <summary>Key .resx, khong tra chuoi da dich o day.</summary>
    public static string ResourceKey(AccountStatus status) => status switch
    {
        AccountStatus.Pending => "AccountStatusPendingApproval",
        AccountStatus.Locked => "AccountStatusLocked",
        _ => "AccountStatusActive",
    };
}
