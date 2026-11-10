using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Web.Configuration.Options;
using Web.Infrastructure.Security;

namespace NhaTro.UnitTests.Features.Security;

/// <summary>
/// Chống dò mật khẩu bằng chặn IP: không được khoá tài khoản, vì kẻ tấn công chỉ cần
/// gõ sai vài lần là khoá được tài khoản của chủ trọ (tấn công từ chối dịch vụ).
/// </summary>
public sealed class LoginAttemptGuardTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 6, 10, 0, 0, TimeSpan.Zero);

    private static (LoginAttemptGuard Guard, FakeTimeProvider Clock) Create(
        int attempts = 10,
        int blockMinutes = 15,
        int windowMinutes = 15)
    {
        var clock = new FakeTimeProvider(Start);
        var options = Options.Create(new SecurityOptions
        {
            LoginGuard = new LoginGuardOptions
            {
                IpBlockAfterFailedAttempts = attempts,
                IpBlockMinutes = blockMinutes,
                AttemptWindowMinutes = windowMinutes,
            },
        });

        return (new LoginAttemptGuard(options, clock), clock);
    }

    [Fact]
    public void Chan_ip_sau_nguong_va_bo_dem_tai_khoan_chi_dung_cho_captcha()
    {
        var (guard, _) = Create(attempts: 3);

        guard.RegisterFailure("1.2.3.4", "a@x.vn");
        guard.RegisterFailure("1.2.3.4", "a@x.vn");
        Assert.Null(guard.GetIpBlockRemaining("1.2.3.4"));

        guard.RegisterFailure("1.2.3.4", "a@x.vn");

        Assert.NotNull(guard.GetIpBlockRemaining("1.2.3.4"));
        Assert.Equal(3, guard.GetAccountFailures("a@x.vn"));
    }

    [Fact]
    public void Ip_khac_khong_bi_chan()
    {
        var (guard, _) = Create(attempts: 2);

        guard.RegisterFailure("1.1.1.1", "a@x.vn");
        guard.RegisterFailure("1.1.1.1", "a@x.vn");

        Assert.NotNull(guard.GetIpBlockRemaining("1.1.1.1"));
        Assert.Null(guard.GetIpBlockRemaining("2.2.2.2"));
    }

    [Fact]
    public void Doi_tai_khoan_lien_tuc_van_bi_chan_theo_ip()
    {
        var (guard, _) = Create(attempts: 3);

        guard.RegisterFailure("9.9.9.9", "a@x.vn");
        guard.RegisterFailure("9.9.9.9", "b@x.vn");
        guard.RegisterFailure("9.9.9.9", "c@x.vn");

        Assert.NotNull(guard.GetIpBlockRemaining("9.9.9.9"));
        Assert.Equal(0, guard.GetAccountFailures("d@x.vn"));
    }

    [Fact]
    public void Het_thoi_gian_chan_thi_mo_lai_ip()
    {
        var (guard, clock) = Create(attempts: 1, blockMinutes: 10);

        guard.RegisterFailure("1.1.1.1", "a@x.vn");
        Assert.NotNull(guard.GetIpBlockRemaining("1.1.1.1"));

        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Null(guard.GetIpBlockRemaining("1.1.1.1"));
    }

    [Fact]
    public void Dang_nhap_thanh_cong_xoa_bo_dem_cua_tai_khoan()
    {
        var (guard, _) = Create();

        guard.RegisterFailure("1.1.1.1", "a@x.vn");
        guard.RegisterFailure("1.1.1.1", "a@x.vn");
        Assert.Equal(2, guard.GetAccountFailures("a@x.vn"));

        guard.RegisterSuccess("1.1.1.1", "a@x.vn");

        Assert.Equal(0, guard.GetAccountFailures("a@x.vn"));
    }

    [Fact]
    public void Qua_cua_so_dem_thi_bo_dem_tu_dat_lai()
    {
        var (guard, clock) = Create(attempts: 5, windowMinutes: 5);

        guard.RegisterFailure("1.1.1.1", "a@x.vn");
        guard.RegisterFailure("1.1.1.1", "a@x.vn");

        clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(0, guard.GetAccountFailures("a@x.vn"));
        Assert.Null(guard.GetIpBlockRemaining("1.1.1.1"));
    }
}
