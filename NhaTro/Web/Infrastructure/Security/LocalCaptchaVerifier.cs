using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;

namespace Web.Infrastructure.Security;

/// <summary>
/// CAPTCHA cục bộ: câu hỏi cộng hai số, đáp án mã hoá bằng Data Protection
/// nên không cần lưu trạng thái phía server và không lộ đáp án trong form.
/// Không thêm phụ thuộc bên thứ ba; có thể thay bằng <see cref="ICaptchaVerifier"/> thật sau này.
/// </summary>
public sealed class LocalCaptchaVerifier : ICaptchaVerifier
{
    private const string ProtectorPurpose = "VHT27.Captcha";

    private readonly ITimeLimitedDataProtector _protector;
    private readonly CaptchaOptions _options;

    public LocalCaptchaVerifier(IDataProtectionProvider provider, IOptions<SecurityOptions> options)
    {
        _protector = provider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        _options = options.Value.Captcha;
    }

    public bool IsChallengeRequired(int failedAttempts) =>
        failedAttempts >= _options.RequiredAfterFailedAttempts;

    public CaptchaChallenge CreateChallenge()
    {
        var left = Random.Shared.Next(1, 10);
        var right = Random.Shared.Next(1, 10);
        var answer = (left + right).ToString(CultureInfo.InvariantCulture);

        var lifetime = TimeSpan.FromMinutes(Math.Max(1, _options.ChallengeLifetimeMinutes));
        var token = _protector.Protect(answer, lifetime);

        return new CaptchaChallenge($"{left} + {right}", token);
    }

    public bool Verify(string? token, string? answer)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(answer))
        {
            return false;
        }

        try
        {
            var expected = _protector.Unprotect(token);
            return string.Equals(expected, answer.Trim(), StringComparison.Ordinal);
        }
        catch (CryptographicException)
        {
            // Token giả/đã hết hạn: coi như sai.
            return false;
        }
        catch (FormatException)
        {
            // Token không đúng định dạng base64.
            return false;
        }
    }
}
