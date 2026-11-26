using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace Web.Features.AccessControl.Services;

public interface IGateTokenService
{
    string NewToken();
    string NewCode();
    string Hash(string value);
    bool FixedTimeEquals(string a, string b);
    int CurrentWindow(DateTimeOffset at);
    string IssueResidentToken(string userId, int propertyId, DateTimeOffset at);
    bool TryVerifyResidentToken(string token, DateTimeOffset at, out string userId, out int propertyId);
}

/// <summary>
/// Token QR một lần (random 32 byte), mã 6 số, và QR động cho cư dân.
/// Server chỉ lưu SHA-256 của token/mã; QR động ký HMAC theo cửa sổ 60 giây.
/// </summary>
public sealed class GateTokenService : IGateTokenService
{
    public const int ResidentWindowSeconds = 60;
    private const string ResidentPrefix = "AC1";
    private readonly byte[] _signingKey;

    public GateTokenService(IConfiguration configuration)
    {
        var configured = configuration["AccessControl:QrSigningKey"];
        _signingKey = string.IsNullOrWhiteSpace(configured)
            ? Encoding.UTF8.GetBytes("nhatro-dev-access-control-signing-key-change-in-production")
            : Encoding.UTF8.GetBytes(configured);
    }

    public string NewToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public string NewCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    public string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty))).ToLowerInvariant();

    public bool FixedTimeEquals(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a ?? string.Empty);
        var right = Encoding.UTF8.GetBytes(b ?? string.Empty);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    public int CurrentWindow(DateTimeOffset at) =>
        (int)(at.ToUnixTimeSeconds() / ResidentWindowSeconds);

    public string IssueResidentToken(string userId, int propertyId, DateTimeOffset at)
    {
        var window = CurrentWindow(at);
        var payload = $"{ResidentPrefix}.{window.ToString(CultureInfo.InvariantCulture)}.{propertyId.ToString(CultureInfo.InvariantCulture)}.{userId}";
        return $"{payload}.{Sign(payload)}";
    }

    public bool TryVerifyResidentToken(string token, DateTimeOffset at, out string userId, out int propertyId)
    {
        userId = string.Empty;
        propertyId = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.');
        if (parts.Length != 5 || parts[0] != ResidentPrefix) return false;
        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var window)) return false;
        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out propertyId)) return false;

        var current = CurrentWindow(at);
        if (Math.Abs(current - window) > 1) return false;

        var payload = $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}";
        if (!FixedTimeEquals(Sign(payload), parts[4])) return false;

        userId = parts[3];
        return !string.IsNullOrWhiteSpace(userId);
    }

    /// <summary>TOTP RFC 6238 (HMAC-SHA1, 60 giây, 6 số) — dùng cho kiểm thử và tích hợp thiết bị.</summary>
    public static string ComputeTotp(byte[] secret, DateTimeOffset at, int digits = 6, int stepSeconds = ResidentWindowSeconds)
    {
        var counter = at.ToUnixTimeSeconds() / stepSeconds;
        Span<byte> counterBytes = stackalloc byte[8];
        for (var i = 7; i >= 0; i--)
        {
            counterBytes[i] = (byte)(counter & 0xff);
            counter >>= 8;
        }

        using var hmac = new HMACSHA1(secret);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0f;
        var binary = ((hash[offset] & 0x7f) << 24) | (hash[offset + 1] << 16) |
                     (hash[offset + 2] << 8) | hash[offset + 3];
        var modulo = (int)Math.Pow(10, digits);
        return (binary % modulo).ToString($"D{digits}", CultureInfo.InvariantCulture);
    }

    public static bool VerifyTotp(byte[] secret, string code, DateTimeOffset at, int window = 1, int stepSeconds = ResidentWindowSeconds)
    {
        if (string.IsNullOrWhiteSpace(code)) return false;
        for (var offset = -window; offset <= window; offset++)
        {
            var candidate = ComputeTotp(secret, at.AddSeconds(offset * stepSeconds));
            if (FixedTimeEqualsStatic(candidate, code.Trim())) return true;
        }

        return false;
    }

    private string Sign(string payload)
    {
        using var hmac = new HMACSHA256(_signingKey);
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool FixedTimeEqualsStatic(string a, string b)
    {
        var left = Encoding.UTF8.GetBytes(a);
        var right = Encoding.UTF8.GetBytes(b);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
