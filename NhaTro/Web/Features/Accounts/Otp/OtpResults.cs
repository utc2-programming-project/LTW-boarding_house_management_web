namespace Web.Features.Accounts.Otp;

public enum OtpIssueStatus
{
    Success,
    Cooldown,
    TooManyRequests,
    Locked,
    Failed,
}

public sealed record OtpIssueResult(
    OtpIssueStatus Status,
    DateTime? ExpiresAt = null,
    int RetryAfterSeconds = 0);

public enum OtpVerifyStatus
{
    Success,
    NotFound,
    Expired,
    Invalid,
    Locked,
}

public sealed record OtpVerifyResult(
    OtpVerifyStatus Status,
    int RemainingAttempts = 0,
    DateTime? LockedUntil = null);
