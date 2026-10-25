namespace Web.Infrastructure.Security;

/// <summary>
/// Trừu tượng hoá CAPTCHA để có thể thay thử thách cục bộ bằng CAPTCHA thật
/// (reCAPTCHA/hCaptcha) sau này mà không phải sửa luồng đăng nhập.
/// </summary>
public interface ICaptchaVerifier
{
    /// <summary>Có bắt buộc giải CAPTCHA với số lần đăng nhập sai đã cho hay không.</summary>
    bool IsChallengeRequired(int failedAttempts);

    /// <summary>Tạo thử thách mới (câu hỏi hiển thị + token để xác minh).</summary>
    CaptchaChallenge CreateChallenge();

    /// <summary>Xác minh câu trả lời với token đã phát hành. Sai/hết hạn/thiếu đều trả <c>false</c>.</summary>
    bool Verify(string? token, string? answer);
}
