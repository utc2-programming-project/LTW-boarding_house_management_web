// Option cấu hình gửi thư, bind từ section "Email" trong appsettings.
namespace Web.Configuration.Options;

/// <summary>
/// Cấu hình gửi thư, bind từ section "Email" trong appsettings.
/// Không chứa secret: mật khẩu SMTP đặt trong User Secrets hoặc biến môi trường
/// (<c>Email__Smtp__Password</c>), không commit vào repo.
/// </summary>
/// <remarks>
/// Hệ thống dùng SMTP của Gmail (App Password 16 ký tự). Khi chưa cấu hình đủ,
/// ứng dụng tự lùi về <c>IdentityNoOpEmailSender</c> để dev vẫn chạy được.
/// </remarks>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public SmtpOptions Smtp { get; set; } = new();

    /// <summary>
    /// Chế độ trình diễn: mọi thư đều gửi về <see cref="FallbackRecipient"/> thay vì địa chỉ
    /// của người dùng. Bật khi dữ liệu mẫu dùng email không tồn tại (admin@admin.com,
    /// chutro@nhatro.local...) để không mất thư. Tắt khi chạy thật với người dùng thật.
    /// </summary>
    public bool DemoMode { get; set; }

    /// <summary>Hộp thư thật nhận thay khi <see cref="DemoMode"/> bật.</summary>
    public string FallbackRecipient { get; set; } = string.Empty;

    /// <summary>
    /// Danh sách chuyển hướng theo từng địa chỉ: khoá là email người dùng (có thể không tồn tại),
    /// giá trị là hộp thư thật. Áp dụng trước <see cref="DemoMode"/> và không phân biệt hoa thường.
    /// </summary>
    public Dictionary<string, string> RecipientOverrides { get; set; } = new();

    /// <summary>Tra cứu chuyển hướng theo địa chỉ, trả <c>null</c> khi không có trong danh sách.</summary>
    public string? FindRecipientOverride(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        foreach (var (key, value) in RecipientOverrides)
        {
            if (string.Equals(key?.Trim(), email.Trim(), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

/// <summary>Tham số SMTP. Mật khẩu chỉ đến từ cấu hình riêng, không đặt trong appsettings.</summary>
public sealed class SmtpOptions
{
    /// <summary>Bật gửi thư thật. Khi thiếu tài khoản/mật khẩu thì vẫn lùi về chế độ ghi log.</summary>
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "smtp.gmail.com";

    /// <summary>Gmail dùng 587 + STARTTLS. Cổng 465 (SSL ngầm) không dùng được với System.Net.Mail.</summary>
    public int Port { get; set; } = 587;

    public bool UseStartTls { get; set; } = true;

    /// <summary>Tài khoản Gmail dùng để gửi (địa chỉ đầy đủ).</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>App Password 16 ký tự do Google sinh. Đặt trong User Secrets, không commit.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Mật khẩu dùng thật khi đăng nhập SMTP: bỏ khoảng trắng vì Google hiển thị App Password
    /// theo nhóm 4 ký tự, dán nguyên vẫn phải gửi đi dạng liền.
    /// </summary>
    public string EffectivePassword => new string((Password ?? string.Empty).Where(c => !char.IsWhiteSpace(c)).ToArray());

    /// <summary>Địa chỉ hiển thị ở mục "From"; để trống thì dùng <see cref="UserName"/>.</summary>
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "Nhà trọ Hòa Bình";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Địa chỉ gửi thực tế, mặc định lấy theo tài khoản đăng nhập SMTP.</summary>
    public string EffectiveFromAddress =>
        string.IsNullOrWhiteSpace(FromAddress) ? UserName : FromAddress;

    /// <summary>Đủ thông tin để gửi thư thật hay chưa.</summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && Port > 0
        && !string.IsNullOrWhiteSpace(UserName)
        && !string.IsNullOrWhiteSpace(EffectivePassword)
        && !string.IsNullOrWhiteSpace(EffectiveFromAddress);
}
