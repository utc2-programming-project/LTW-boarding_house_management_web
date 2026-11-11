namespace Web.Features.Accounts.Otp;

public sealed class OtpChallenge
{
    public int Id { get; set; }

    public string? UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public OtpPurpose Purpose { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 5;

    public DateTime? LockedUntil { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastSentAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public string? IpAddress { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
