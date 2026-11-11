using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Web.Data;
using Web.Infrastructure.Email;

namespace Web.Features.Accounts.Otp;

/// <summary>
/// Phát/kiểm mã OTP một lần. Mã chỉ lưu dạng HMAC-SHA256 + salt, không lưu plaintext
/// và không bao giờ trả mã ra ngoài service.
/// </summary>
public sealed class OtpService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IOptions<OtpOptions> options,
    TimeProvider clock,
    IOutboundEmailSender emailSender,
    IHttpContextAccessor httpContextAccessor,
    IHostEnvironment environment,
    ILogger<OtpService> logger) : IOtpService
{
    private readonly OtpOptions _options = options.Value;
    private byte[]? _hmacKey;

    public async Task<OtpIssueResult> IssueAsync(
        string? userId, string email, OtpPurpose purpose, CancellationToken ct = default)
    {
        email = NormalizeEmail(email);
        if (email.Length == 0)
        {
            return new OtpIssueResult(OtpIssueStatus.Failed);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var active = await db.OtpChallenges
            .Where(item => item.Email == email && item.Purpose == purpose && item.ConsumedAt == null)
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);

        if (active is not null && OtpRules.IsLocked(now, active.LockedUntil))
        {
            return new OtpIssueResult(
                OtpIssueStatus.Locked,
                active.ExpiresAt,
                SecondsUntil(now, active.LockedUntil!.Value));
        }

        if (active is not null && !OtpRules.CanResend(now, active.LastSentAt, _options.ResendCooldownSeconds))
        {
            return new OtpIssueResult(
                OtpIssueStatus.Cooldown,
                active.ExpiresAt,
                SecondsUntil(now, active.LastSentAt.AddSeconds(_options.ResendCooldownSeconds)));
        }

        var hourAgo = now.AddHours(-1);
        var sendsInLastHour = await db.OtpChallenges.CountAsync(
            item => item.Email == email && item.Purpose == purpose && item.CreatedAt >= hourAgo, ct);
        if (sendsInLastHour >= Math.Max(1, _options.MaxSendsPerHour))
        {
            return new OtpIssueResult(OtpIssueStatus.TooManyRequests);
        }

        // Mã mới vô hiệu mã cũ cùng mục đích/email.
        if (active is not null)
        {
            active.ConsumedAt = now;
        }

        var code = GenerateCode();
        var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var challenge = new OtpChallenge
        {
            UserId = userId,
            Email = email,
            Purpose = purpose,
            Salt = salt,
            CodeHash = ComputeHash(salt, code),
            ExpiresAt = OtpRules.NextExpiry(now, _options.LifetimeMinutes),
            MaxAttempts = Math.Max(1, _options.MaxAttempts),
            CreatedAt = now,
            LastSentAt = now,
            IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
        };

        db.OtpChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);

        try
        {
            var content = EmailTemplates.Otp(email, code, PurposeLabel(purpose), _options.LifetimeMinutes);
            await emailSender.SendAsync(email, content.Subject, content.Html, ct);
        }
        catch (Exception ex)
        {
            // Mã đã lưu; người dùng có thể gửi lại sau cooldown. Không để lộ mã trong log.
            logger.LogError(ex, "[OTP] Gửi mã {OtpId} thất bại cho {Email}.", challenge.Id, MaskEmail(email));
            return new OtpIssueResult(OtpIssueStatus.Failed, challenge.ExpiresAt);
        }

        logger.LogInformation(
            "[OTP] Đã phát mã {OtpId} ({Purpose}) cho {Email}.",
            challenge.Id, purpose, MaskEmail(email));

        return new OtpIssueResult(OtpIssueStatus.Success, challenge.ExpiresAt);
    }

    public async Task<OtpVerifyResult> VerifyAsync(
        string? userId, string email, OtpPurpose purpose, string code, CancellationToken ct = default)
    {
        email = NormalizeEmail(email);
        code = (code ?? string.Empty).Trim();
        if (email.Length == 0 || code.Length is 0 or > 16)
        {
            return new OtpVerifyResult(OtpVerifyStatus.Invalid);
        }

        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var query = db.OtpChallenges
            .Where(item => item.Purpose == purpose && item.ConsumedAt == null);
        query = string.IsNullOrWhiteSpace(userId)
            ? query.Where(item => item.Email == email)
            : query.Where(item => item.UserId == userId);

        var challenge = await query
            .OrderByDescending(item => item.Id)
            .FirstOrDefaultAsync(ct);

        if (challenge is null)
        {
            return new OtpVerifyResult(OtpVerifyStatus.NotFound);
        }

        if (OtpRules.IsLocked(now, challenge.LockedUntil))
        {
            return new OtpVerifyResult(OtpVerifyStatus.Locked, 0, challenge.LockedUntil);
        }

        if (OtpRules.IsExpired(now, challenge.ExpiresAt))
        {
            return new OtpVerifyResult(OtpVerifyStatus.Expired);
        }

        if (!Matches(challenge, code))
        {
            challenge.AttemptCount++;
            if (challenge.AttemptCount >= Math.Max(1, challenge.MaxAttempts))
            {
                challenge.LockedUntil = now.AddMinutes(Math.Max(1, _options.LockMinutes));
                await db.SaveChangesAsync(ct);
                logger.LogWarning(
                    "[OTP] Mã {OtpId} bị khoá tạm sau {Attempts} lần sai.",
                    challenge.Id, challenge.AttemptCount);
                return new OtpVerifyResult(OtpVerifyStatus.Locked, 0, challenge.LockedUntil);
            }

            await db.SaveChangesAsync(ct);
            return new OtpVerifyResult(
                OtpVerifyStatus.Invalid,
                Math.Max(0, challenge.MaxAttempts - challenge.AttemptCount));
        }

        challenge.ConsumedAt = now;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Hai tab cùng xác thực: chỉ một request thắng.
            return new OtpVerifyResult(OtpVerifyStatus.Invalid);
        }

        logger.LogInformation("[OTP] Mã {OtpId} đã dùng thành công.", challenge.Id);
        return new OtpVerifyResult(OtpVerifyStatus.Success);
    }

    public async Task InvalidateAsync(string email, OtpPurpose purpose, CancellationToken ct = default)
    {
        email = NormalizeEmail(email);
        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        await db.OtpChallenges
            .Where(item => item.Email == email && item.Purpose == purpose && item.ConsumedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.ConsumedAt, (DateTime?)now), ct);
    }

    private string GenerateCode()
    {
        var length = _options.Length == 8 ? 8 : 6;
        var max = length == 8 ? 100_000_000 : 1_000_000;
        var value = RandomNumberGenerator.GetInt32(0, max);
        return value.ToString(length == 8 ? "D8" : "D6", CultureInfo.InvariantCulture);
    }

    private bool Matches(OtpChallenge challenge, string code)
    {
        var expected = Encoding.UTF8.GetBytes(challenge.CodeHash);
        var actual = Encoding.UTF8.GetBytes(ComputeHash(challenge.Salt, code));
        return expected.Length == actual.Length
            && CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    private string ComputeHash(string salt, string code)
    {
        var payload = Encoding.UTF8.GetBytes($"{salt}:{code}");
        return Convert.ToBase64String(HMACSHA256.HashData(GetHmacKey(), payload));
    }

    private byte[] GetHmacKey()
    {
        if (_hmacKey is not null)
        {
            return _hmacKey;
        }

        if (!string.IsNullOrWhiteSpace(_options.HmacKey))
        {
            try
            {
                _hmacKey = Convert.FromBase64String(_options.HmacKey);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("Otp:HmacKey phải là chuỗi Base64.");
            }

            return _hmacKey;
        }

        if (environment.IsDevelopment())
        {
            // Khóa cố định chỉ dành cho dev; production bắt buộc cấu hình Otp:HmacKey.
            _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes("NhaTro.Development.Otp.HmacKey"));
            return _hmacKey;
        }

        throw new InvalidOperationException("Thiếu cấu hình Otp:HmacKey.");
    }

    private static int SecondsUntil(DateTime nowUtc, DateTime untilUtc) =>
        Math.Max(1, (int)Math.Ceiling((untilUtc - nowUtc).TotalSeconds));

    private static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return "***";
        }

        var name = email[..at];
        var domain = email[at..];
        return name.Length <= 2 ? "***" + domain : name[0] + "***" + domain;
    }

    private static string PurposeLabel(OtpPurpose purpose)
    {
        var english = string.Equals(
            CultureInfo.CurrentUICulture.TwoLetterISOLanguageName,
            "en",
            StringComparison.OrdinalIgnoreCase);

        return purpose switch
        {
            OtpPurpose.OwnerRegistration => english ? "owner registration" : "đăng ký chủ trọ",
            OtpPurpose.ManagerFirstLogin => english ? "manager verification" : "xác thực quản lý",
            OtpPurpose.TenantFirstLogin => english ? "tenant verification" : "xác thực khách thuê",
            OtpPurpose.TenantRegistration => english ? "tenant registration" : "đăng ký khách thuê",
            OtpPurpose.PasswordReset => english ? "password reset" : "đặt lại mật khẩu",
            _ => english ? "account verification" : "xác thực tài khoản",
        };
    }
}
