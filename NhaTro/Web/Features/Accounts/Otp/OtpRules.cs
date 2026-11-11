namespace Web.Features.Accounts.Otp;

public static class OtpRules
{
    public static bool IsExpired(DateTime nowUtc, DateTime expiresAt) => nowUtc >= expiresAt;

    public static bool IsLocked(DateTime nowUtc, DateTime? lockedUntil) =>
        lockedUntil is { } until && until > nowUtc;

    public static DateTime NextExpiry(DateTime nowUtc, int lifetimeMinutes) =>
        nowUtc.AddMinutes(lifetimeMinutes);

    public static bool CanResend(DateTime nowUtc, DateTime lastSentAt, int cooldownSeconds) =>
        nowUtc >= lastSentAt.AddSeconds(cooldownSeconds);
}
