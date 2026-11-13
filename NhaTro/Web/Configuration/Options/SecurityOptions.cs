namespace Web.Configuration.Options;

/// <summary>
/// Cấu hình bảo mật nền tảng, bind từ section "Security" trong appsettings.
/// Khi yêu cầu DPAPI, phải cung cấp đường dẫn lưu keys.
/// Không chứa secret.
/// </summary>
public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>
    /// Thư mục lưu khoá Data Protection (ngoài thư mục publish). Hỗ trợ biến môi trường
    /// (ví dụ <c>%LOCALAPPDATA%\NhaTro\keys</c>). Chỉ được dùng mặc định của nền tảng
    /// khi đường dẫn trống và không yêu cầu <see cref="ProtectKeysWithDpapi"/>.
    /// </summary>
    public string DataProtectionKeysPath { get; set; } = string.Empty;

    /// <summary>
    /// Bật mã hoá keys mới bằng DPAPI theo identity Windows cho IIS một máy.
    /// Giữ nguyên identity/profile và thư mục keys khi publish lại. Dev mặc định không bật.
    /// </summary>
    public bool ProtectKeysWithDpapi { get; set; }

    public CaptchaOptions Captcha { get; set; } = new();

    public LoginGuardOptions LoginGuard { get; set; } = new();

    public RateLimitOptions RateLimit { get; set; } = new();
}

/// <summary>Tham số CAPTCHA. Dùng thử thách cục bộ.</summary>
public sealed class CaptchaOptions
{
    /// <summary>Số lần đăng nhập sai tối thiểu trước khi bắt buộc giải CAPTCHA.</summary>
    public int RequiredAfterFailedAttempts { get; set; } = 3;

    /// <summary>Thời gian sống (phút) của một thử thách CAPTCHA cục bộ.</summary>
    public int ChallengeLifetimeMinutes { get; set; } = 5;
}

/// <summary>
/// Chống dò mật khẩu bằng cách chặn theo IP thay vì khoá tài khoản.
/// Khoá tài khoản tự động sẽ bị lợi dụng để khoá người dùng hợp lệ (DoS), nên chỉ
/// quản trị viên mới được khoá tài khoản.
/// </summary>
public sealed class LoginGuardOptions
{
    /// <summary>Số lần đăng nhập sai từ cùng một IP trước khi chặn IP đó.</summary>
    public int IpBlockAfterFailedAttempts { get; set; } = 10;

    /// <summary>Thời gian chặn IP (phút).</summary>
    public int IpBlockMinutes { get; set; } = 15;

    /// <summary>Cửa sổ đếm số lần sai (phút); quá thời gian này bộ đếm tự đặt lại.</summary>
    public int AttemptWindowMinutes { get; set; } = 15;
}

/// <summary>Ngưỡng rate limiting theo cửa sổ cố định.</summary>
public sealed class RateLimitOptions
{
    public int LoginPermitLimit { get; set; } = 10;

    public int LoginWindowSeconds { get; set; } = 60;

    public int FileUploadPermitLimit { get; set; } = 20;

    public int FileUploadWindowSeconds { get; set; } = 60;

    public int CultureSetPermitLimit { get; set; } = 20;

    public int CultureSetWindowSeconds { get; set; } = 60;

    /// <summary>Chặn spam đăng ký chủ trọ (PLAN-008).</summary>
    public int RegisterPermitLimit { get; set; } = 5;

    public int RegisterWindowSeconds { get; set; } = 3600;

    /// <summary>Chặn dò mã OTP (PLAN-022).</summary>
    public int OtpVerifyPermitLimit { get; set; } = 20;

    public int OtpVerifyWindowSeconds { get; set; } = 900;

    /// <summary>Chặn spam form quên mật khẩu (PLAN-038).</summary>
    public int ForgotPasswordPermitLimit { get; set; } = 5;

    public int ForgotPasswordWindowSeconds { get; set; } = 900;

    /// <summary>Chặn spam gửi lại thư xác nhận email (PLAN-038).</summary>
    public int ResendEmailPermitLimit { get; set; } = 5;

    public int ResendEmailWindowSeconds { get; set; } = 900;
}
