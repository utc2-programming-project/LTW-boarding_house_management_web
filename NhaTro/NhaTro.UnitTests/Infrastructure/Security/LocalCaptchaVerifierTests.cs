using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Web.Configuration.Options;
using Web.Infrastructure.Security;

namespace NhaTro.UnitTests.Infrastructure.Security;

/// <summary>CAPTCHA cục bộ — không lộ đáp án, token giả/hết hạn bị từ chối.</summary>
public sealed class LocalCaptchaVerifierTests
{
    private static LocalCaptchaVerifier CreateVerifier(Action<SecurityOptions>? configure = null)
    {
        var options = new SecurityOptions();
        configure?.Invoke(options);
        return new LocalCaptchaVerifier(new EphemeralDataProtectionProvider(), Options.Create(options));
    }

    [Fact]
    public void Bat_buoc_tu_nguong_3_lan_sai()
    {
        var verifier = CreateVerifier();

        Assert.False(verifier.IsChallengeRequired(0));
        Assert.False(verifier.IsChallengeRequired(2));
        Assert.True(verifier.IsChallengeRequired(3));
        Assert.True(verifier.IsChallengeRequired(4));
    }

    [Fact]
    public void Chi_chap_nhan_dung_tong_va_token_hop_le()
    {
        var verifier = CreateVerifier();
        var challenge = verifier.CreateChallenge();
        var expected = ExpectedAnswer(challenge.Question);

        Assert.True(verifier.Verify(challenge.Token, expected.ToString()));
        Assert.True(verifier.Verify(challenge.Token, $" {expected} "));
        Assert.False(verifier.Verify(challenge.Token, (expected + 1).ToString()));

        Assert.False(verifier.Verify(null, "5"));
        Assert.False(verifier.Verify("", "5"));
        Assert.False(verifier.Verify(challenge.Token, ""));
        Assert.False(verifier.Verify("khong-phai-token", "5"));

        // Đảo một ký tự trong token (giữ nguyên độ dài) -> mã hoá/MAC không còn hợp lệ.
        var chars = challenge.Token.ToCharArray();
        chars[^2] = chars[^2] == 'A' ? 'B' : 'A';
        var tampered = new string(chars);

        Assert.False(verifier.Verify(tampered, expected.ToString()));
    }

    private static int ExpectedAnswer(string question)
    {
        var parts = question.Split(" + ", StringSplitOptions.TrimEntries);
        Assert.Equal(2, parts.Length);
        return int.Parse(parts[0]) + int.Parse(parts[1]);
    }
}
