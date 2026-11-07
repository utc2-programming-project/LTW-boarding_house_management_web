namespace Web.Infrastructure.Security;

/// <summary>
/// Đếm số lần đăng nhập sai theo IP và theo tài khoản.
/// Mục đích: chống dò mật khẩu bằng cách <b>chặn IP</b>, không khoá tài khoản —
/// nếu khoá tài khoản thì kẻ tấn công chỉ cần gõ sai vài lần là khoá được chủ trọ (DoS).
/// Khoá tài khoản chỉ do quản trị viên chủ động qua <c>AccountService.LockAsync</c>.
/// </summary>
public interface ILoginAttemptGuard
{
    /// <summary>Thời gian còn lại của lệnh chặn IP; <c>null</c> nếu IP không bị chặn.</summary>
    TimeSpan? GetIpBlockRemaining(string ip);

    /// <summary>Số lần đăng nhập sai gần đây của tài khoản (dùng để quyết định bắt giải CAPTCHA).</summary>
    int GetAccountFailures(string accountKey);

    /// <summary>Ghi nhận một lần đăng nhập sai; trả về số lần sai hiện tại của tài khoản.</summary>
    int RegisterFailure(string ip, string accountKey);

    /// <summary>Đăng nhập thành công: xoá bộ đếm sai của tài khoản.</summary>
    void RegisterSuccess(string ip, string accountKey);
}
