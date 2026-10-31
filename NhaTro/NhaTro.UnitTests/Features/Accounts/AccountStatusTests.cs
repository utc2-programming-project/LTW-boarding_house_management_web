using Web.Features.Accounts;

namespace NhaTro.UnitTests.Features.Accounts;

/// <summary>VHT-95: suy trang thai tai khoan (ham thuan, khong DB).</summary>
public class AccountStatusTests
{
    private static readonly DateTime Approved = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Chua_duyet_la_Pending()
    {
        Assert.Equal(AccountStatus.Pending, AccountStatusRules.Derive(null, null));
    }

    [Fact]
    public void Da_duyet_va_khong_khoa_la_Active()
    {
        Assert.Equal(AccountStatus.Active, AccountStatusRules.Derive(Approved, null));
    }

    [Fact]
    public void Khoa_quan_tri_thang_moi_trang_thai_khac()
    {
        Assert.Equal(AccountStatus.Locked,
            AccountStatusRules.Derive(Approved, DateTimeOffset.MaxValue));
        Assert.Equal(AccountStatus.Locked,
            AccountStatusRules.Derive(null, DateTimeOffset.MaxValue));
    }

    [Fact]
    public void Khoa_tam_thoi_do_sai_mat_khau_khong_phai_Khoa()
    {
        var temporary = DateTimeOffset.UtcNow.AddMinutes(15);

        Assert.Equal(AccountStatus.Active, AccountStatusRules.Derive(Approved, temporary));
    }

    [Theory]
    [InlineData(AccountStatus.Pending, "AccountStatusPendingApproval")]
    [InlineData(AccountStatus.Active, "AccountStatusActive")]
    [InlineData(AccountStatus.Locked, "AccountStatusLocked")]
    public void Moi_trang_thai_co_key_dich_rieng(AccountStatus status, string expectedKey)
    {
        Assert.Equal(expectedKey, AccountStatusRules.ResourceKey(status));
    }
}
